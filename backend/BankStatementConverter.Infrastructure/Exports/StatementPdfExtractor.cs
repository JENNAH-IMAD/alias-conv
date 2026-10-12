using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace BankStatementConverter.Infrastructure.Exports;

// Stage 2 is independent of how stage 1 obtained its text boxes (native PDF or OCR).
public sealed class StatementPdfExtractor : IStatementPdfExtractor
{
    private sealed record Token(string Text, double Left, double Right, double Y);
    private sealed record Line(double Y, IReadOnlyList<Token> Words)
    {
        public string Text => string.Join(" ", Words.Select(w => w.Text));
    }
    private sealed record Header(double Y, Token Date, Token? ValueDate, Token? Reference, Token Description, Token Debit, Token Credit)
    {
        public double MoneyStart => Debit.Left - 55;
        // Column captions may be left-aligned (e.g. "Débit Dirhams").
        // The credit caption gives the start of that column, not its centre.
        public double MoneySplit => Credit.Left - 6;
    }
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private const string FullDate = @"\d{2}(?:[/.-]|[ \t]+)\d{2}(?:[/.-]|[ \t]+)\d{4}";
    private static string Fold(string text) => new(text.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).Select(char.ToUpperInvariant).ToArray());
    private static bool Has(string text, string pattern) => Regex.IsMatch(Fold(text), pattern, RegexOptions.CultureInvariant, RegexTimeout);
    private static List<Line> Lines(IEnumerable<Token> words)
    {
        var lines = new List<List<Token>>();
        foreach (var word in words.OrderByDescending(w => w.Y).ThenBy(w => w.Left))
        {
            if (lines.Count == 0 || Math.Abs(lines[^1][0].Y - word.Y) > 2.6) lines.Add([]);
            lines[^1].Add(word);
        }
        return lines.Select(l => new Line(l.Average(w => w.Y), l.OrderBy(w => w.Left).ToList())).ToList();
    }
    private static Header? FindHeader(IReadOnlyList<Token> words)
    {
        foreach (var line in Lines(words))
        {
            var fields = new Dictionary<string, List<Token>>();
            for (var i = 0; i < line.Words.Count; i++)
            {
                string? field = null; var last = i;
                for (var j = i; j < Math.Min(i + 5, line.Words.Count); j++)
                {
                    if (j > i && line.Words[j].Left - line.Words[j - 1].Right > 25) break;
                    var candidate = StatementFieldNames.Column(string.Join(" ", line.Words.Skip(i).Take(j-i+1).Select(w=>w.Text)));
                    if (candidate != null) { field = candidate; last = j; }
                }
                if (field == null) continue;
                if (!fields.TryGetValue(field, out var tokens)) fields[field] = tokens = [];
                tokens.Add(line.Words[i]); i = last;
            }
            Token? One(string field) => fields.TryGetValue(field, out var tokens) && tokens.Count == 1 ? tokens[0] : null;
            var date = One("Date"); var description = One("Description");
            var debitColumn = One("Debit"); var creditColumn = One("Credit");
            if (date != null && description != null && debitColumn != null && creditColumn != null
                && date.Left < description.Left && description.Left < debitColumn.Left && debitColumn.Left < creditColumn.Left)
                return new(line.Y, date, One("ValueDate"), One("Reference"), description, debitColumn, creditColumn);
        }
        foreach (var debit in words.Where(w => StatementFieldNames.Column(w.Text)=="Debit").OrderByDescending(w => w.Y))
        {
            var nearby = words.Where(w => Math.Abs(w.Y - debit.Y) <= 12).OrderBy(w => w.Left).ToList();
            var credit = nearby.FirstOrDefault(w => w.Left > debit.Left && StatementFieldNames.Column(w.Text)=="Credit");
            var dates = nearby.Where(w => Has(w.Text, "^DATE$")).ToList();
            var description = nearby.FirstOrDefault(w => StatementFieldNames.Column(w.Text)=="Description" && !Has(w.Text,"^OP.?RATIONS?$"))
                ?? nearby.FirstOrDefault(w => Has(w.Text, "^NATURE$"))
                ?? nearby.FirstOrDefault(w => w.Left > (dates.LastOrDefault()?.Right ?? 0) && Has(w.Text, "^OP.?RATION$"));
            if (credit == null || dates.Count == 0 || description == null) continue;
            var value = dates.Skip(1).FirstOrDefault() ?? nearby.FirstOrDefault(w => Has(w.Text, "^VALEUR$") && w.Left > dates[0].Left + 30);
            var reference = nearby.FirstOrDefault(w => Has(w.Text, "^R.?F.?RENCE$"));
            return new(nearby.Where(w => Has(w.Text, "^(DATE|VALEUR|OP.?RATION|LIBELL.?E?|NATURE|D.?BIT|CR.?DIT)$")).Min(w => w.Y), dates[0], value, reference, description, debit, credit);
        }
        // Two-level captions: a shared "Date" sits above "Opération" and "Valeur".
        // The parent caption and distinct description/debit/credit columns disambiguate
        // the otherwise generic word "Opération" without guessing from movement text.
        foreach (var debit in words.Where(w => StatementFieldNames.Column(w.Text)=="Debit"))
        {
            var nearby=words.Where(w=>Math.Abs(w.Y-debit.Y)<=12).OrderBy(w=>w.Left).ToArray();
            var operation=nearby.FirstOrDefault(w=>Has(w.Text,@"^OP.?RATION$"));
            var value=nearby.FirstOrDefault(w=>Has(w.Text,@"^VALEUR+$"));
            var label=nearby.FirstOrDefault(w=>Has(w.Text,@"^LIBELL.?E?$|^DESCRIPTION$"));
            var credit=nearby.FirstOrDefault(w=>StatementFieldNames.Column(w.Text)=="Credit");
            if(operation==null || value==null || label==null || credit==null
                || !(operation.Left<value.Left && value.Left<label.Left && label.Left<debit.Left && debit.Left<credit.Left)) continue;
            if(!words.Any(w=>Has(w.Text,@"^DATE$") && w.Y>operation.Y+5 && w.Y<operation.Y+35
                && w.Left>=operation.Left-10 && w.Left<=value.Right)) continue;
            return new(nearby.Min(w=>w.Y),operation,value,null,label,debit,credit);
        }
        return null;
    }
    private static DateOnly? Date(string text, DateOnly? start, DateOnly? end, DateOnly? reference = null)
        => StatementValueParser.Date(text, start, end, reference);
    private static decimal? Money(string text) => StatementValueParser.Money(text);
    private static IReadOnlyList<decimal> MoneyValues(string text) => Regex.Matches(text, @"(?<![\w/])[-+]?\d+(?:(?:[ \u00a0\u202f]+|[.,])\d{3})*[,.]\d{2}(?!\d)", RegexOptions.None, RegexTimeout)
        .Select(m => Money(m.Value)).Where(v => v.HasValue).Select(v => v!.Value).ToList();
    public PdfStatementExtraction Extract(byte[] content, string bankName, string bankCode, string currency, CancellationToken ct)
    {
        try
        {
            var document=ReadDocument(content,ct);
            if(document.Pages.Any(p=>p.Boxes.Count<10)) throw new AppException(422,"Le PDF contient une page scannée ou sans texte exploitable. Un moteur OCR est nécessaire pour ce document ; aucun mouvement n’a été inventé.");
            return Normalize(document,bankName,bankCode,currency,ct);
        }
        catch (AppException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { throw new AppException(422, "Le PDF est illisible, protégé par un mot de passe ou endommagé. Choisissez un PDF déverrouillé avec du texte sélectionnable."); }
    }
    public PdfTextDocument ReadDocument(byte[] content,CancellationToken ct)
    {
        try
        {
            using var pdf=PdfDocument.Open(content);
            Validation.Require(pdf.NumberOfPages is >0 and <=200,"Le relevé doit contenir entre 1 et 200 pages.");
            var result=new List<PdfTextPage>();
            foreach(var page in pdf.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                var words=page.GetWords(NearestNeighbourWordExtractor.Instance).Select(w=>new PdfTextBox(w.Text,w.BoundingBox.Left,w.BoundingBox.Right,
                    page.Height-w.BoundingBox.Top,page.Height-w.BoundingBox.Bottom)).ToList();
                Validation.Require(words.Count<=100000 && result.Sum(p=>p.Boxes.Count)+words.Count<=500000,"Ce PDF contient trop de texte pour être traité.");
                result.Add(new(page.Number,page.Width,page.Height,words));
            }
            return new("NATIVE_PDF",result);
        }
        catch(AppException) { throw; }
        catch(OperationCanceledException) { throw; }
        catch(Exception ex) when(ex is not OutOfMemoryException) { throw new AppException(422,"Le PDF est illisible, protégé par un mot de passe ou endommagé."); }
    }
    public PdfStatementExtraction Normalize(PdfTextDocument document,string bankName,string bankCode,string currency,CancellationToken ct,PdfColumnMapping? mapping=null,BankStatement? metadata=null)
    {
        foreach(var page in document.NativePages ?? document.Pages)
        {
            ct.ThrowIfCancellationRequested();
            var pageText=string.Join("\n",Lines(page.Boxes.Select(b=>new Token(b.Text,b.Left,b.Right,page.Height-(b.Top+b.Bottom)/2))).Select(l=>l.Text));
            foreach(Match folio in Regex.Matches(Fold(pageText),@"\bPAGE\s+(\d{1,3})\s*(?:/|SUR|OF)\s*(\d{1,3})\b",RegexOptions.None,RegexTimeout))
                if(int.Parse(folio.Groups[1].Value,CultureInfo.InvariantCulture)!=page.Number
                    || int.Parse(folio.Groups[2].Value,CultureInfo.InvariantCulture)!=document.Pages.Count)
                    throw new AppException(422,"Le relevé est incomplet ou ses pages sont désordonnées. Vérifiez les numéros de page du PDF original.");
        }
        if(mapping==null && BmceNativeStatementNormalizer.TryNormalize(document,bankName,bankCode,currency,ct) is {} bmce) return PdfStatementMetadata.Complete(bmce,metadata);
        if(document.Pages.Any(p=>p.Tables is {Count:>0}) && (mapping==null || mapping.TableColumns!=null))
        {
            PdfStatementExtraction? structured = null;
            AppException? failure = null;
            try
            {
                structured = StructuredStatementNormalizer.Normalize(document,bankName,bankCode,currency,ct,mapping?.TableColumns,metadata);
                if (mapping != null || PdfExtractionQuality.Defects(structured) == 0) return structured;
            }
            catch(AppException e) when(mapping==null && e.Status==422 && !e.Message.Contains("devise",StringComparison.OrdinalIgnoreCase))
            { failure = e; }
            if(mapping==null && document.NativePages is {Count:>0} && document.NativePages.Count==document.Pages.Count && document.NativePages.All(p=>p.Boxes.Count>=10))
            {
                PdfStatementExtraction? recovered = null;
                try { recovered = NativeTableRecovery.TryNormalize(document,bankName,bankCode,currency,ct,metadata); }
                catch(AppException e) when(e.Status==422 && !e.Message.Contains("devise",StringComparison.OrdinalIgnoreCase)) { }
                if (recovered == null)
                {
                    try { recovered = Normalize(new("NATIVE_PDF",document.NativePages),bankName,bankCode,currency,ct,metadata:metadata); }
                    catch(AppException e) when(e.Status==422 && !e.Message.Contains("devise",StringComparison.OrdinalIgnoreCase)) { }
                }
                if(recovered != null && (structured == null || PdfExtractionQuality.Improves(recovered,structured)))
                    return recovered with {RawDocument=document,Warnings=recovered.Warnings.Concat(new[] {new ExportError("PDF_NATIVE_RECOVERY","Les tableaux OCR étaient incomplets. Les mouvements ont été relus dans le texte et les colonnes du PDF original. Vérifiez toutes les pages avant validation.")}).ToList()};
            }
            if (structured != null) return structured;
            throw failure!;
        }
        if(mapping!=null)
        {
            var columns=new[] {mapping.Date,mapping.Description,mapping.Debit,mapping.Credit};
            Validation.Require(columns.All(v=>double.IsFinite(v) && v>=0 && v<100) && columns.SequenceEqual(columns.Order()) && columns.Distinct().Count()==4
                && double.IsFinite(mapping.HeaderTop) && mapping.HeaderTop>=0 && mapping.HeaderTop<100
                && (!mapping.ValueDate.HasValue || double.IsFinite(mapping.ValueDate.Value) && mapping.ValueDate>=0 && mapping.ValueDate<100),"Positions des colonnes invalides. Utilisez des pourcentages croissants : date, libellé, débit, crédit.");
        }
        var pages = new List<List<Token>>();
        var allLines = new List<Line>();
        foreach (var page in document.Pages)
        {
            ct.ThrowIfCancellationRequested();
            var words=page.Boxes.SelectMany(box=>Regex.Matches(box.Text,@"\S+",RegexOptions.None,RegexTimeout)
                .Select(m=>new Token(m.Value,box.Left+(box.Right-box.Left)*m.Index/box.Text.Length,
                    box.Left+(box.Right-box.Left)*(m.Index+m.Length)/box.Text.Length,
                    page.Height-(document.Method=="NATIVE_PDF" ? box.Bottom : (box.Top+box.Bottom)/2)))).ToList();
            pages.Add(words); allLines.AddRange(Lines(words));
        }
        var warnings = new List<ExportError> { new("PDF_REVIEW_REQUIRED", "Données lues dans le PDF : vérifiez toutes les pages, les libellés, les colonnes débit/crédit et les soldes avec l’original avant validation.") };
        var text = string.Join("\n", allLines.Select(l => l.Text));
        var detectedCurrency = Regex.Match(Fold(text), @"DEVISE\s*:\s*[^\n]*?\b(MAD|EUR|USD|GBP|CHF)\b", RegexOptions.None, RegexTimeout);
        if (detectedCurrency.Success && detectedCurrency.Groups[1].Value != currency) throw new AppException(422, "La devise sélectionnée diffère de celle du PDF. Corrigez la devise d’importation.");
        var period = Regex.Match(Fold(text), $@"(?:PERIODE\s*:\s*|\bDU\s+)({FullDate})\s*(?:AU|-)\s*({FullDate})", RegexOptions.None, RegexTimeout);
        DateOnly? start = metadata?.PeriodStart ?? (period.Success ? Date(period.Groups[1].Value, null, null) : null);
        DateOnly? end = metadata?.PeriodEnd ?? (period.Success ? Date(period.Groups[2].Value, null, null) : null);
        start ??= allLines.Select(l => Regex.Match(Fold(l.Text), $@"^DU\s+({FullDate})", RegexOptions.None, RegexTimeout)).Where(m => m.Success).Select(m => Date(m.Groups[1].Value, null, null)).FirstOrDefault();
        end ??= allLines.Select(l => Regex.Match(Fold(l.Text), $@"^AU\s+({FullDate})", RegexOptions.None, RegexTimeout)).Where(m => m.Success).Select(m => Date(m.Groups[1].Value, null, null)).FirstOrDefault();
        if(!start.HasValue || !end.HasValue)
        {
            var printed=PdfStatementMetadata.Complete(new(new BankStatement {BankName=bankName,BankCode=bankCode,Currency=currency},[],document),metadata).Statement;
            start ??= printed.PeriodStart; end ??= printed.PeriodEnd;
        }
        var balanceLines = allLines.Select((l, i) => (Line: l, Index: i)).Where(x =>
            Has(x.Line.Text, $@"^\s*(?:{FullDate}\s+)?(?:(?:(?:ANCIEN|NOUVEAU)\s+)?SOLDE\b|(?:OPENING|CLOSING|PREVIOUS|ENDING)\s+BALANCE\b)")
            && !StatementFieldNames.IsAvailableBalance(x.Line.Text)
            && !Has(x.Line.Text, @"REPORT(?:E)?\s+(?:DE\s+)?(PAGE|INTERMEDIAIRE)")).ToList();
        var openingLine = balanceLines.FirstOrDefault(x => StatementFieldNames.Balance(x.Line.Text)=="OpeningBalance" || Has(x.Line.Text,@"\bSOLDE\s+REPORT\b"));
        if (openingLine.Line == null && balanceLines.Count >= 2) openingLine = balanceLines.First();
        var closingLine = balanceLines.LastOrDefault(x => x.Index > openingLine.Index && (StatementFieldNames.Balance(x.Line.Text)=="ClosingBalance" || Has(x.Line.Text, @"\bAU\b|\bSOLDE\s+NET\b")));
        if (closingLine.Line == null && balanceLines.Count >= 2) closingLine = balanceLines.Last();
        if (closingLine.Line == null && balanceLines.Count == 1 && openingLine.Line == null
            && allLines.Take(balanceLines[0].Index).Any(l => Has(l.Text,@"^\s*TOTAL\b"))) closingLine = balanceLines[0];
        decimal? Balance((Line? Line, int Index) line)
        {
            if (line.Line == null) return null;
            var values = MoneyValues(line.Line.Text);
            if (values.Count == 0 && line.Index + 1 < allLines.Count) values = MoneyValues(allLines[line.Index + 1].Text);
            if (values.Count == 0) return null;
            var value = values[0];
            if (Has(line.Line.Text, @"D.?BITEUR|\bDR\b")) return -Math.Abs(value);
            if (Has(line.Line.Text, @"CR.?DITEUR|\bCR\b") || value < 0) return value;
            // A balance printed in the debit column is negative, including an undated
            // closing balance. Never use an account number or a date to locate its cell.
            var caption = pages.Select(FindHeader).FirstOrDefault(h => h != null);
            var amountEnd = line.Line.Words.LastOrDefault(w => Has(w.Text,@"[,.]\d{2}$"));
            if (caption != null && amountEnd != null && amountEnd.Left > caption.MoneyStart
                && (amountEnd.Left + amountEnd.Right) / 2 < caption.MoneySplit) return -Math.Abs(value);
            return value;
        }
        var opening = Balance(openingLine); var closing = Balance(closingLine);
        DateOnly? BalanceDate(Line? line) => line == null ? null : Date(Regex.Match(line.Text, FullDate, RegexOptions.None, RegexTimeout).Value, null, null);
        DateOnly? BalanceValueDate(Line? line) => line == null ? null : Regex.Matches(line.Text, FullDate, RegexOptions.None, RegexTimeout)
            .Skip(1).Select(m => Date(m.Value, null, null)).FirstOrDefault();
        start ??= BalanceDate(openingLine.Line); end ??= BalanceDate(closingLine.Line);
        var transactions = new List<StatementTransaction>(); Header? previous = null;
        for(var pageIndex=0;pageIndex<pages.Count;pageIndex++)
        {
            var words=pages[pageIndex];
            ct.ThrowIfCancellationRequested();
            var header = FindHeader(words) ?? (previous == null ? null : previous with { Y = double.MaxValue });
            if(mapping!=null)
            {
                var page=document.Pages[pageIndex];var y=page.Height*(1-mapping.HeaderTop/100);
                Token Column(string label,double start)=>new(label,page.Width*start/100,page.Width*start/100+20,y);
                header=new(y,Column("Date",mapping.Date),mapping.ValueDate.HasValue ? Column("Valeur",mapping.ValueDate.Value) : null,null,
                    Column("Libellé",mapping.Description),Column("Débit",mapping.Debit),Column("Crédit",mapping.Credit));
            }
            if (header == null) throw new AppException(422, "Les colonnes du relevé ne sont pas reconnues. Consultez le texte extrait et réglez l’adaptation du tableau.");
            previous = header;
            var lines = Lines(words);
            var footer = lines.FirstOrDefault(l => l.Y < header.Y - 15 && Has(l.Text, @"^\s*TOTAL\b|\bTOTAL\s+D.?BIT\b"));
            bool IsDateStart(Line line, Token word) => Math.Abs(word.Left - header.Date.Left) < 28
                && (Regex.IsMatch(word.Text, @"^\d{2}[/.-]\d{2}", RegexOptions.None, RegexTimeout)
                    || Regex.IsMatch(word.Text, @"^\d{2}$", RegexOptions.None, RegexTimeout)
                        && line.Words.Any(next => next.Left >= word.Right && next.Left - word.Right < 15
                            && Regex.IsMatch(next.Text, @"^\d{2}$", RegexOptions.None, RegexTimeout)));
            var anchors = lines.Where(l => l.Y < header.Y - 3 && l.Y > (footer?.Y ?? double.MinValue) && l.Words.Any(w => IsDateStart(l,w))).ToList();
            if(anchors.Count==0) throw new AppException(422,"Une page du PDF ne contient aucun mouvement reconnu. Vérifiez son format ; le document n’a pas été importé partiellement.");
            for (var i = 0; i < anchors.Count; i++)
            {
                ct.ThrowIfCancellationRequested(); var anchor = anchors[i];
                var upper = anchor.Y + 3;
                var followingBalance = lines.FirstOrDefault(l=>l.Y<anchor.Y-3 && Has(l.Text,@"^\s*(NOUVEAU\s+)?SOLDE\b"));
                var lower = i == anchors.Count - 1 ? Math.Max(footer?.Y+3 ?? double.MinValue,followingBalance?.Y+3 ?? anchor.Y-40)
                    : anchors[i + 1].Y + 3;
                var band = words.Where(w => w.Y < upper && w.Y > lower).ToList();
                if (Has(anchor.Text, @"\bSOLDE\b|\bTOTAL\b")) continue;
                var dateStart = anchor.Words.First(w => IsDateStart(anchor,w));
                // Read only the actual date cell. Fixed-width windows used to swallow nearby
                // labels and could leave a split value-date year inside the money columns.
                List<Token> DateCell(Token first, Token caption)
                {
                    var nextColumn = new[] {header.Date, header.ValueDate, header.Reference, header.Description, header.Debit, header.Credit}
                        .Where(c => c != null && c.Left > caption.Left + 10).Select(c => c!.Left).DefaultIfEmpty(double.MaxValue).Min();
                    var candidates = band.Where(w => w.Left >= first.Left - 1 && w.Left < nextColumn - 3
                            && w.Left < first.Left + 68 && Math.Abs(w.Y - first.Y) <= 12
                            && Regex.IsMatch(w.Text, @"^(?=.*\d)[\d/.-]+$", RegexOptions.None, RegexTimeout))
                        .OrderBy(w => w == first ? 0 : 1).ThenByDescending(w => w.Y).ThenBy(w => w.Left).ToList();
                    // A cell may have a year on the following line. Prefer the complete printed
                    // form when available, but never take numbers from a neighboring column.
                    for (var count = Math.Min(4, candidates.Count); count > 0; count--)
                    {
                        var cell = candidates.Take(count).ToList();
                        if (Date(string.Join(" ", cell.Select(w => w.Text)), start, end).HasValue) return cell;
                    }
                    return candidates.Count > 0 ? candidates : [first];
                }
                var dateWords = DateCell(dateStart, header.Date);
                var operationDate = Date(string.Join(" ", dateWords.Select(w => w.Text)), start, end);
                var descriptionStart = dateWords.Max(w => w.Right) + 2;
                var valueStart = header.ValueDate == null ? null : anchor.Words.FirstOrDefault(w => Math.Abs(w.Left - header.ValueDate.Left) < 23 && Regex.IsMatch(w.Text, @"^\d{2}(?:[/.-]\d{2}|$)", RegexOptions.None, RegexTimeout));
                List<Token> valueWords = []; DateOnly? valueDate = null;
                if (valueStart != null)
                {
                    valueWords = DateCell(valueStart, header.ValueDate!);
                    valueDate = Date(string.Join(" ", valueWords.Select(w => w.Text)), start, end, operationDate);
                    if (header.ValueDate!.Left < header.Description.Left) descriptionStart = valueWords.Max(w => w.Right) + 2;
                }
                string? reference = null;
                if (header.Reference != null)
                {
                    var referenceWords = anchor.Words.Where(w => w.Left >= header.Reference.Left - 12 && w.Left < header.Reference.Left + 42 && w.Left >= descriptionStart).ToList();
                    if (referenceWords.Count > 0) { reference = string.Join(" ", referenceWords.Select(w => w.Text)); descriptionStart = referenceWords.Max(w => w.Right) + 2; }
                }
                if(mapping!=null) descriptionStart=document.Pages[pageIndex].Width*mapping.Description/100;
                var descriptionEnd = header.ValueDate != null && header.ValueDate.Left > header.Description.Left && header.ValueDate.Left < header.Debit.Left
                    ? (valueStart?.Left ?? header.ValueDate.Left) - 3 : header.Debit.Left - 12;
                var description = string.Join("\n", Lines(band.Where(w => w.Left >= descriptionStart && w.Left < descriptionEnd && w.Right <= descriptionEnd + 2))
                    .Where(l => !Has(l.Text,@"\bSOLDE\b|\bREPORT\s+PAGE\b|^\s*TOTAL\b"))
                    .Select(l => Regex.Replace(l.Text.Trim(), @"\s+", " ", RegexOptions.None, RegexTimeout)));
                var financial = anchor.Words.Where(w => !dateWords.Contains(w) && !valueWords.Contains(w) && w.Left > Math.Max(descriptionEnd, header.MoneyStart) && w.Left < header.Credit.Right + 85).ToList();
                decimal? Cell(bool debit) => Money(string.Join(" ", financial.Where(w => (w.Left + w.Right) / 2 < header.MoneySplit == debit).Select(w => w.Text)));
                var debit = Cell(true); var credit = Cell(false);
                debit = debit.HasValue ? Math.Abs(debit.Value) : null; credit = credit.HasValue ? Math.Abs(credit.Value) : null;
                var sequence = transactions.Count + 1;
                if (!debit.HasValue && !credit.HasValue) warnings.Add(new("PDF_AMOUNT_UNREADABLE", "Montant non reconnu dans le PDF. Renseignez-le après consultation de l’original.", sequence, SourceField: "Amount"));
                if (!operationDate.HasValue || (valueStart != null && !valueDate.HasValue)) warnings.Add(new("PDF_DATE_UNREADABLE", "Date non reconnue ou année absente sans période exploitable. Vérifiez la date dans l’original.", sequence, SourceField: "Date"));
                transactions.Add(new() { Sequence = sequence, Date = operationDate, ValueDate = valueDate, Description = description, Reference = reference,
                    Debit = debit, Credit = credit, Amount = debit.HasValue != credit.HasValue ? debit ?? credit : null,
                    Direction = debit.HasValue != credit.HasValue ? debit.HasValue ? "DEBIT" : "CREDIT" : "UNKNOWN" });
            }
        }
        if (transactions.Count == 0) throw new AppException(422, "Aucun mouvement bancaire n’a été reconnu dans ce PDF. Aucun relevé vide n’a été créé.");
        if (Has(text, "TOTAL")) warnings.Add(new("PDF_TOTALS_REVIEW", "Les totaux imprimés peuvent inclure le solde initial. Vérifiez leur convention avant de renseigner les totaux des mouvements dans la fiche."));
        return PdfStatementMetadata.Complete(new(new BankStatement { BankName = bankName, BankCode = bankCode, Currency = currency,
            PeriodStart = start, PeriodEnd = end, OpeningBalance = opening, ClosingBalance = closing,
            OpeningBalanceDate = BalanceDate(openingLine.Line), OpeningBalanceValueDate = BalanceValueDate(openingLine.Line),
            OpeningBalanceDescription = openingLine.Line?.Text, ClosingBalanceDescription = closingLine.Line?.Text,
            ClosingBalanceDate = BalanceDate(closingLine.Line), ClosingBalanceValueDate = BalanceValueDate(closingLine.Line), Transactions = transactions }, warnings,document),metadata);
    }
}
