using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Exports;

// Bank fields come from table cells, not guessed character positions inside an OCR block.
public static class StructuredStatementNormalizer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    private const string FullDate = @"\d{2}(?:[/.-]|[ \t]+)\d{2}(?:[/.-]|[ \t]+)(?:\d{4}|\d{2})(?!\d)";
    private static string Fold(string s) => new(s.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).Select(char.ToUpperInvariant).ToArray());
    private static Match Match(string s, string pattern) => Regex.Match(s, pattern, RegexOptions.CultureInvariant, Timeout);
    private static string Compact(string s) => Regex.Replace(s, @"\s+", "", RegexOptions.None, Timeout);
    private static DateOnly? Date(string text, DateOnly? start, DateOnly? end, DateOnly? reference = null)
        => StatementValueParser.Date(text, start, end, reference);
    private static decimal? Money(string text) => StatementValueParser.Money(text);
    internal static PdfTableColumnMapping? Header(IReadOnlyList<string> cells)
    {
        var labels = cells.Select(StatementFieldNames.Column).ToList();
        int Find(string field) => labels.Count(label => label == field) == 1 ? labels.IndexOf(field) : -1;
        var date = Find("Date");
        var value = Find("ValueDate");
        var description = Find("Description");
        var debit = Find("Debit");
        var credit = Find("Credit");
        var reference = Find("Reference");
        return (date >= 0 || value >= 0) && description >= 0 && debit >= 0 && credit >= 0
            ? new(date >= 0 ? date : value, date < 0 || value < 0 ? null : value, description, debit, credit, 0, reference < 0 ? null : reference) : null;
    }
    public static PdfStatementExtraction Normalize(PdfTextDocument document, string bankName, string bankCode, string currency, CancellationToken ct, PdfTableColumnMapping? mapping = null, BankStatement? metadata = null)
    {
        if (mapping != null)
        {
            var columns = new int?[] { mapping.Date, mapping.ValueDate, mapping.Description, mapping.Debit, mapping.Credit, mapping.Reference }.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            Validation.Require(columns.All(v => v is >= 0 and < 64) && columns.Distinct().Count() == columns.Count && mapping.StartRow is >= 0 and <= 200, "Indices des colonnes invalides : choisissez des colonnes différentes entre 1 et 64.");
        }
        var text = string.Join("\n", document.Pages.Select(p => string.Join("\n", p.Boxes.Select(b => b.Text))));
        var detected = Match(Fold(text), @"DEVISE\s*:\s*[^\n]*?\b(MAD|EUR|USD|GBP|CHF)\b");
        if (detected.Success && detected.Groups[1].Value != currency)
            throw new AppException(422, "La devise sélectionnée diffère de celle du PDF. Corrigez la devise d’importation.");
        var period = Match(Fold(text), $@"(?:PERIODE\s*(?::|DU)?\s*|\bDU\s+)({FullDate})\s*(?:AU|-)\s*({FullDate})");
        DateOnly? start = metadata?.PeriodStart ?? (period.Success ? Date(period.Groups[1].Value, null, null) : null);
        DateOnly? end = metadata?.PeriodEnd ?? (period.Success ? Date(period.Groups[2].Value, null, null) : null);
        // Header text may be split over several OCR boxes. Resolve the printed period
        // before reading short transaction dates, rather than completing it only afterwards.
        if (!start.HasValue || !end.HasValue)
        {
            var printed = PdfStatementMetadata.Complete(new(new BankStatement { BankName = bankName, BankCode = bankCode, Currency = currency }, [], document), metadata).Statement;
            start ??= printed.PeriodStart;
            end ??= printed.PeriodEnd;
        }
        var warnings = new List<ExportError> { new("PDF_REVIEW_REQUIRED", "Vérifiez les tableaux, mouvements et soldes extraits avec toutes les pages du PDF original avant validation.") };
        var transactions = new List<StatementTransaction>();
        var balances = new List<(decimal? Amount, DateOnly? Date, DateOnly? ValueDate, string Label)>();
        PdfTableColumnMapping? previous = null;
        foreach (var page in document.Pages)
        {
            ct.ThrowIfCancellationRequested();
            if (page.Tables is not { Count: > 0 })
                throw new AppException(422, "Une page ne contient aucun tableau reconnu. Vérifiez toutes les pages avant l’export ; aucun import partiel n’a été adapté.");
            var recognizedOnPage = false;
            foreach (var table in page.Tables ?? [])
            {
                PdfTableColumnMapping? columns = mapping;
                var recognizedIndex = table.Rows.ToList().FindIndex(row => Header(row) != null);
                var recognized = recognizedIndex < 0 ? null : Header(table.Rows[recognizedIndex]);
                if (recognized == null)
                {
                    // OCR expands rowspans and bilingual captions into separate header rows.
                    // Combine only adjacent caption rows; never infer columns from amounts.
                    for (var h = 0; h < Math.Min(5, table.Rows.Count - 1); h++)
                    {
                        var upper = table.Rows[h];
                        var lower = table.Rows[h + 1];
                        if (upper.Count != lower.Count)
                            continue;
                        var combined = upper.Select((cell, c) => cell == lower[c] ? cell : cell + " " + lower[c]).ToList();
                        recognized = Header(combined);
                        if (recognized != null)
                        {
                            recognizedIndex = h + 1;
                            break;
                        }
                    }
                }
                if (columns == null)
                    columns = recognized;
                if (columns == null && previous != null && table.Rows.Any(row => row.Count > previous.Date && Date(row[previous.Date], start, end).HasValue))
                    columns = previous with
                    {
                        StartRow = 0
                    };
                if (columns == null)
                {
                    if (table.Rows.Any(row => row.Any(c => Date(c, start, end).HasValue) && row.Any(c => Money(c).HasValue)))
                        throw new AppException(422, "Un tableau contenant des mouvements n’a pas été reconnu. Adaptez ses colonnes avant l’export.");
                    continue;
                }
                previous = columns;
                recognizedOnPage = true;
                var first = mapping?.StartRow ?? (recognizedIndex < 0 ? 0 : recognizedIndex + 1);
                // Repeated headers are independently recognized on every page.
                for (var i = first; i < table.Rows.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var row = table.Rows[i];
                    if (Header(row) != null)
                        continue;
                    string Cell(int? index) => index.HasValue && index >= 0 && index < row.Count ? row[index.Value].Trim() : "";
                    if (Regex.Matches(Cell(columns.Date), @"(?m)^\s*\d{2}(?:[/.-]|[ \t]+)\d{2}(?:[/.-]\d{2,4})?\s*$", RegexOptions.None, Timeout).Count > 1
                        || new[] { Cell(columns.Debit), Cell(columns.Credit) }.Any(s => Regex.Matches(s, @"[,.]\d{2}(?!\d)", RegexOptions.None, Timeout).Count > 1))
                        throw new AppException(422, "PaddleOCR a regroupé plusieurs mouvements dans une cellule. Reprenez l’adaptation automatique à partir du PDF original.");
                    var label = Cell(columns.Description);
                    var folded = Fold(label);
                    var debitText = Cell(columns.Debit);
                    var creditText = Cell(columns.Credit);
                    decimal? CellMoney(string amount)
                    {
                        var parsed = StatementValueParser.Money(amount, currency);
                        if (parsed.HasValue || !Regex.IsMatch(amount, @"^[+-]?\d{1,3}(?:,\d{3})+,\d{2}$", RegexOptions.None, Timeout))
                            return parsed;
                        var lastComma = amount.LastIndexOf(',');
                        parsed = Money(amount[..lastComma].Replace(",", "") + amount[lastComma..]);
                        if (parsed.HasValue)
                            warnings.Add(new("PDF_AMOUNT_GROUPING_REPAIRED", "Séparateurs de milliers OCR normalisés. Vérifiez le montant avec le PDF original.", transactions.Count + 1, SourceField: "Amount"));
                        return parsed;
                    }
                    var debit = CellMoney(debitText);
                    var credit = CellMoney(creditText);
                    var opDate = Date(Cell(columns.Date), start, end);
                    var valueDate = Date(Cell(columns.ValueDate), start, end, opDate);
                    if (StatementFieldNames.IsBalance(label))
                    {
                        if (StatementFieldNames.IsAvailableBalance(label))
                            continue;
                        if (folded.Contains("REPORT") && StatementFieldNames.Balance(label) == null)
                            continue;
                        var dateInLabel = Match(Compact(label), FullDate);
                        opDate ??= dateInLabel.Success ? Date(dateInLabel.Value, null, null) : null;
                        decimal? balance = debit.HasValue != credit.HasValue ? debit.HasValue ? -Math.Abs(debit.Value) : credit : null;
                        if (!balance.HasValue)
                        {
                            var amount = Match(Regex.Replace(label, FullDate, "", RegexOptions.None, Timeout), @"[-+]?\d+(?:[ \u00a0\u202f.]\d{3})*[,.]\d{2}(?!\d)");
                            balance = amount.Success ? Money(amount.Value) : null;
                        }
                        if (balance.HasValue && Match(folded, @"DEBITEUR|\bDR\b").Success)
                            balance = -Math.Abs(balance.Value);
                        balances.Add((balance, opDate, valueDate, label));
                        continue;
                    }
                    if (Match(folded, @"^\s*TOTAL\b|\bREPORT\b").Success || Match(Fold(string.Join(" ", row)), @"^\s*TOTAL\b").Success)
                        continue;
                    if (row.All(string.IsNullOrWhiteSpace))
                        continue;
                    if (string.IsNullOrWhiteSpace(Cell(columns.Date)) && string.IsNullOrWhiteSpace(Cell(columns.ValueDate)) && string.IsNullOrWhiteSpace(debitText) && string.IsNullOrWhiteSpace(creditText))
                    {
                        if (transactions.Count > 0 && !string.IsNullOrWhiteSpace(label))
                            transactions[^1] = transactions[^1] with
                            {
                                Description = transactions[^1].Description + "\n" + label,
                                SourcePages = (transactions[^1].SourcePages ?? []).Append(page.Number).Distinct().ToArray()
                            };
                        continue;
                    }
                    var sequence = transactions.Count + 1;
                    (opDate, valueDate) = StatementValueParser.CompleteDates(opDate, valueDate, Cell(columns.Date), Cell(columns.ValueDate), warnings);
                    if (!opDate.HasValue || (!string.IsNullOrWhiteSpace(Cell(columns.ValueDate)) && !valueDate.HasValue))
                        warnings.Add(new("PDF_DATE_UNREADABLE", "Date non reconnue dans une cellule du tableau. Corrigez-la avec l’original.", sequence, SourceField: "Date"));
                    if ((!string.IsNullOrWhiteSpace(debitText) && !debit.HasValue) || (!string.IsNullOrWhiteSpace(creditText) && !credit.HasValue) || !debit.HasValue && !credit.HasValue)
                        warnings.Add(new("PDF_AMOUNT_UNREADABLE", "Montant non reconnu dans une cellule du tableau. Corrigez-le avec l’original.", sequence, SourceField: "Amount"));
                    debit = debit.HasValue ? Math.Abs(debit.Value) : null;
                    credit = credit.HasValue ? Math.Abs(credit.Value) : null;
                    transactions.Add(new()
                    {
                        Sequence = sequence,
                        SourcePages = [page.Number],
                        Date = opDate,
                        ValueDate = valueDate,
                        Description = label,
                        Reference = string.IsNullOrWhiteSpace(Cell(columns.Reference)) ? null : Cell(columns.Reference),
                        Debit = debit,
                        Credit = credit,
                        Amount = debit.HasValue != credit.HasValue ? debit ?? credit : null,
                        Direction = debit.HasValue != credit.HasValue ? debit.HasValue ? "DEBIT" : "CREDIT" : "UNKNOWN"
                    });
                }
            }
            if (!recognizedOnPage)
                throw new AppException(422, "Les colonnes d’une page ne sont pas reconnues. Adaptez toutes les pages avant l’export.");
        }
        if (transactions.Count == 0)
            throw new AppException(422, "Aucun mouvement reconnu. Adaptez les colonnes des tableaux structurés.");
        // Some statements print balances outside their table, in header/footer blocks.
        if (balances.Count < 2)
        {
            var outside = document.Pages.SelectMany(p => p.Boxes).Where(b => StatementFieldNames.IsBalance(b.Text) && !StatementFieldNames.IsAvailableBalance(b.Text) && !b.Text.Contains('\n') && (!Fold(b.Text).Contains("REPORT") || StatementFieldNames.Balance(b.Text) != null));
            foreach (var box in outside)
            {
                var date = Match(box.Text, FullDate);
                var amount = Match(Regex.Replace(box.Text, FullDate, "", RegexOptions.None, Timeout), @"[-+]?\d+(?:[ \u00a0\u202f.]\d{3})*[,.]\d{2}(?!\d)");
                if (!amount.Success)
                    continue;
                var value = Money(amount.Value);
                if (value.HasValue && Fold(box.Text).Contains("DEBITEUR"))
                    value = -Math.Abs(value.Value);
                if (!balances.Any(b => b.Label == box.Text))
                    balances.Add((value, date.Success ? Date(date.Value, null, null) : null, null, box.Text));
            }
        }
        var opening = balances.FirstOrDefault(b => StatementFieldNames.Balance(b.Label) == "OpeningBalance");
        var closing = balances.LastOrDefault(b => StatementFieldNames.Balance(b.Label) == "ClosingBalance");
        if (opening.Label == null && balances.Count >= 2)
            opening = balances.OrderBy(b => b.Date).First();
        if (closing.Label == null && balances.Count >= 2)
            closing = balances.OrderBy(b => b.Date).Last();
        start ??= opening.Date;
        end ??= closing.Date;
        var account = Match(text, @"(?im)\bCOMPTE\s*(?:N[°ºo]?\s*|:\s*)([0-9][0-9 ]{5,35}[0-9])\b");
        return PdfStatementMetadata.Complete(new(new BankStatement
        {
            BankName = bankName,
            BankCode = bankCode,
            Currency = currency,
            AccountNumber = account.Success ? account.Groups[1].Value.Trim() : null,
            PeriodStart = start,
            PeriodEnd = end,
            OpeningBalance = opening.Amount,
            ClosingBalance = closing.Amount,
            OpeningBalanceDate = opening.Date,
            OpeningBalanceValueDate = opening.ValueDate,
            ClosingBalanceDate = closing.Date,
            ClosingBalanceValueDate = closing.ValueDate,
            OpeningBalanceDescription = opening.Label,
            ClosingBalanceDescription = closing.Label,
            Transactions = transactions
        }, warnings, document), metadata);
    }
}
