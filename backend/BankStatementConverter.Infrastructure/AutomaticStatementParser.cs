using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace BankStatementConverter.Infrastructure;

// Keep PDF coordinates until columns and logical rows have been reconstructed.
public sealed class AutomaticStatementParser
{
    private static readonly Regex MoneyPattern = new(@"-?\d[\d \u00a0\u202f]*,\d{2}(?!\d)");
    private static readonly Regex DatePattern = new(@"^\d{2}[ /]\d{2}(?:[ /]\d{4})?$");
    private sealed record Line(double Y, List<Letter> Letters)
    {
        public string Text => Join(Letters);
        public string Cell(double left, double right) => Join(Letters.Where(l => l.StartBaseLine.X >= left - .5 && l.StartBaseLine.X < right - .5));
    }

    public IReadOnlyList<BankTransaction> Parse(Stream stream, CancellationToken ct = default)
    {
        try { return Read(stream, ct); }
        catch (Exception ex) when (ex is not AppException and not OperationCanceledException)
        { throw new AppException(400, "PDF illisible ou structure non reconnue par le profil automatique. Aucun export genere."); }
    }

    private static IReadOnlyList<BankTransaction> Read(Stream stream, CancellationToken ct)
    {
        using var document = PdfDocument.Open(stream);
        Validation.Require(document.NumberOfPages is > 0 and <= 200, "Maximum 200 pages par PDF.");
        var result = new List<BankTransaction>();
        decimal? opening = null;
        decimal running = 0, debitSum = 0, creditSum = 0;
        bool closingSeen = false;
        DateOnly? periodStart = null, periodEnd = null;
        var charCount = 0;

        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            charCount += page.Letters.Count;
            Validation.Require(charCount <= 2_000_000, "PDF trop volumineux pour l'extraction.");
            var lines = Lines(page.Letters.Where(l => Math.Abs(l.StartBaseLine.Y - l.EndBaseLine.Y) < .1));
            Validation.Require(lines.Count > 0, $"Page {page.Number} scannee ou vide : le profil multibanque necessite un PDF texte. Utilisez un profil OCR adapte.");
            var header = lines.FirstOrDefault(l => Normalize(l.Text).Contains("DEBIT") && Normalize(l.Text).Contains("CREDIT") && !Normalize(l.Text).Contains("TOTAL"));
            Validation.Require(header != null, $"Page {page.Number} : colonnes debit/credit introuvables.");
            var verticals = page.Paths.SelectMany(p => p).SelectMany(p => p.Commands).OfType<PdfSubpath.Line>()
                .Where(l => Math.Abs(l.From.X - l.To.X) < .5 && Math.Min(l.From.Y, l.To.Y) < header!.Y && Math.Max(l.From.Y, l.To.Y) > header.Y)
                .Select(l => Math.Round(l.From.X, 1)).Distinct().Order().ToArray();
            Validation.Require(verticals.Length is 6 or 7, $"Page {page.Number} : grille de colonnes non reconnue.");
            var horizontals = page.Paths.SelectMany(p => p).SelectMany(p => p.Commands).OfType<PdfSubpath.Line>()
                .Where(l => Math.Abs(l.From.Y - l.To.Y) < .5 && Math.Abs(l.From.X - l.To.X) >= verticals[^1] - verticals[0] - 2).Select(l => l.From.Y).ToList();
            var headerTop = horizontals.Where(y => y > header!.Y).DefaultIfEmpty(header!.Y + 10).Min();
            var headerBottom = horizontals.Where(y => y < header!.Y).DefaultIfEmpty(header!.Y - 10).Max();
            var columns = Enumerable.Range(0, verticals.Length - 1).Select(i => Normalize(string.Join(" ", lines.Where(l => l.Y < headerTop && l.Y > headerBottom).Select(l => l.Cell(verticals[i], verticals[i + 1]))))).ToArray();
            int debit = Array.FindIndex(columns, c => c.Contains("DEBIT"));
            int credit = Array.FindIndex(columns, c => c.Contains("CREDIT"));
            int description = Array.FindIndex(columns, c => c.Contains("LIBELLE") || c.Contains("NATURE") || c == "OPERATION");
            int valueDate = Array.FindIndex(columns, c => c.Contains("VALEUR"));
            int reference = Array.FindIndex(columns, c => c.Contains("REFERENCE") || c == "CODE");
            int date = Array.FindIndex(columns, c => c.Contains("DATE") && !c.Contains("VALEUR"));
            Validation.Require(date >= 0 && description >= 0 && valueDate >= 0 && debit >= 0 && credit >= 0, "En-tetes du tableau non reconnus.");
            string Cell(Line line, int col) => col < 0 ? "" : line.Cell(verticals[col], verticals[col + 1]);
            var pageText = Normalize(string.Join("\n", lines.Select(l => l.Text)));
            var includesOpening = pageText.Contains("TOTAL CREDIT : SOLDE");
            var datesAbove = lines.Where(l => l.Y > header!.Y && !Normalize(l.Text).Contains("SOLDE"))
                .SelectMany(l => Regex.Matches(l.Text, @"\b\d{2}/\d{2}/(\d{4})\b")).ToList();
            if (datesAbove.Count >= 2)
            {
                var start = ParseDate(datesAbove[0].Value, null);
                var end = ParseDate(datesAbove[1].Value, null);
                Validation.Require(start <= end && end.DayNumber - start.DayNumber < 366, "Periode du releve invalide.");
                Validation.Require(periodStart == null || (periodStart == start && periodEnd == end), "Plusieurs periodes differentes dans un PDF.");
                periodStart = start; periodEnd = end;
            }

            var anchors = lines.Where(l => l.Y < headerBottom && DatePattern.IsMatch(Cell(l, date)) && !IsSummary(l.Text)).ToList();
            Validation.Require(anchors.Count > 0, $"Page {page.Number} : aucune operation reconnue.");
            var summaryLines = lines.Where(l => IsSummary(l.Text)).ToList();
            var handled = new HashSet<Line>();
            foreach (var line in lines)
            {
                ct.ThrowIfCancellationRequested();
                var normalized = Normalize(line.Text);
                if (IsSummary(line.Text))
                {
                    var amounts = SummaryAmounts(line.Text);
                    // Some statements put closing balance and both totals below their labels.
                    if (normalized.Contains("SOLDE") && normalized.Contains("TOTAL DEBIT") && normalized.Contains("TOTAL CREDIT"))
                    {
                        var next = lines.Skip(lines.IndexOf(line) + 1).FirstOrDefault();
                        Validation.Require(next != null, "Recapitulatif final absent.");
                        amounts = SummaryAmounts(next!.Text);
                        Validation.Require(amounts.Length == 3 && opening != null, "Recapitulatif final illisible.");
                        CheckTotals(amounts[1], amounts[2], debitSum, creditSum, includesOpening ? opening!.Value : 0);
                        Validation.Require(amounts[0] == running, "Ecart de solde final. Aucun export genere.");
                        closingSeen = true; handled.Add(next);
                    }
                    else if (amounts.Length > 0 && (normalized.Contains("SOLDE") || normalized.Contains("REPORT")))
                    {
                        Validation.Require(amounts.Length == 1, "Plusieurs montants dans une ligne de solde.");
                        var balance = amounts[0];
                        if (normalized.Contains("DEBITEUR") || Regex.IsMatch(normalized, @"\bDB\b")) balance = -Math.Abs(balance);
                        if (opening == null) { opening = balance; running = balance; }
                        else
                        {
                            Validation.Require(balance == running, $"Page {page.Number} : ecart de solde ou de report. Aucun export genere.");
                            if (!normalized.Contains("REPORT")) closingSeen = true;
                        }
                    }
                    else if (normalized.Contains("TOTAL") && amounts.Length > 0)
                    {
                        Validation.Require(amounts.Length == 2 && opening != null, "Totaux illisibles.");
                        CheckTotals(amounts[0], amounts[1], debitSum, creditSum, includesOpening ? opening!.Value : 0);
                    }
                    continue;
                }
                if (!anchors.Contains(line)) continue;
                Validation.Require(opening != null && !closingSeen, "Solde initial absent ou operations apres le solde final.");
                var index = anchors.IndexOf(line);
                var top = index == 0 ? headerBottom : (anchors[index - 1].Y + line.Y) / 2;
                var bottom = index == anchors.Count - 1 ? line.Y - 16 : (line.Y + anchors[index + 1].Y) / 2;
                top = Math.Min(top, summaryLines.Where(l => l.Y > line.Y && l.Y < top).Select(l => l.Y - 3).DefaultIfEmpty(top).Min());
                bottom = Math.Max(bottom, summaryLines.Where(l => l.Y < line.Y && l.Y > bottom).Select(l => l.Y + 3).DefaultIfEmpty(bottom).Max());
                var band = lines.Where(l => l.Y < top && l.Y > bottom).ToList();
                foreach (var part in band.Where(l => l != line))
                    Validation.Require(Cell(part, date).Length == 0 && Cell(part, debit).Length == 0 && Cell(part, credit).Length == 0, "Ligne supplementaire ambigue dans une operation.");
                var label = string.Join(" ", band.Select(l => Cell(l, description)).Where(s => s.Length > 0));
                var origin = Regex.Match(label, @"(?i)Origine\s*:\s*(-?\d[\d ]*,\d{2})\s*MAD");
                Validation.Require(!label.Contains("Origine", StringComparison.OrdinalIgnoreCase) || origin.Success, "Montant Origine illisible.");
                label = Regex.Split(label, "Origine", RegexOptions.IgnoreCase)[0].Trim();
                Validation.Text(label, "Libelle", 1000);
                var d = ParseAmount(Cell(line, debit)); var c = ParseAmount(Cell(line, credit));
                Validation.Require((d > 0) != (c > 0), "Debit/credit absent ou ambigu.");
                if (origin.Success) Validation.Require(AmountNormalizer.Parse(origin.Groups[1].Value, "fr-FR") == c - d, "Montant Origine different du debit/credit.");
                var operationDate = OperationDate(Cell(line, date), periodStart, periodEnd);
                Validation.Require(periodStart == null || (operationDate >= periodStart && operationDate <= periodEnd), "Date d'operation hors periode du releve.");
                var valueText = Cell(line, valueDate);
                var settlementDate = ParseDate(valueText, operationDate.Year);
                if (valueText.Length == 5)
                    settlementDate = new[] { -1, 0, 1 }.Select(offset => settlementDate.AddYears(offset)).OrderBy(d => Math.Abs(d.DayNumber - operationDate.DayNumber)).First();
                var transaction = new BankTransaction { Date = operationDate, ValueDate = settlementDate, Description = label, Reference = Cell(line, reference), Debit = d, Credit = c };
                running += c - d; debitSum += d; creditSum += c; transaction.Balance = running;
                result.Add(transaction);
                foreach (var part in band) handled.Add(part);
            }
            // A monetary row that was not attached to an operation must never disappear silently.
            foreach (var line in lines.Where(l => l.Y < headerBottom && !handled.Contains(l) && !IsSummary(l.Text)))
                Validation.Require(!MoneyPattern.IsMatch(Cell(line, debit)) && !MoneyPattern.IsMatch(Cell(line, credit)), $"Page {page.Number} : montant sans operation reconnue.");
        }
        Validation.Require(result.Count > 0 && closingSeen, "Solde final absent : verification impossible.");
        return result;
    }

    private static bool IsSummary(string text)
    {
        var s = Normalize(text);
        return Regex.IsMatch(s, @"^(?:\d{2}[ /]\d{2}[ /]\d{4}\s+)?(?:(?:ANCIEN |NOUVEAU )?SOLDE\b|REPORT PAGE\b|TOTAL (?:DES )?MOUVEMENTS?\b|TOTAL CREDIT : SOLDE)");
    }
    private static decimal[] SummaryAmounts(string text) => MoneyPattern.Matches(Regex.Replace(text, @"\b\d{2}/\d{2}/\d{4}\b", ""))
        .Select(m => AmountNormalizer.Parse(m.Value, "fr-FR")).ToArray();
    private static void CheckTotals(decimal debit, decimal credit, decimal expectedDebit, decimal expectedCredit, decimal opening)
        => Validation.Require(debit == expectedDebit + Math.Max(-opening, 0) && credit == expectedCredit + Math.Max(opening, 0), "Totaux des mouvements incoherents. Aucun export genere.");

    private static decimal ParseAmount(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        Validation.Require(MoneyPattern.IsMatch(value) && MoneyPattern.Match(value).Value == value, "Montant illisible dans le tableau.");
        return AmountNormalizer.Parse(value, "fr-FR");
    }
    private static DateOnly ParseDate(string value, int? year)
    {
        value = value.Replace(' ', '/');
        if (value.Length == 5)
        {
            Validation.Require(year != null, "Annee du releve introuvable.");
            value += "/" + year;
        }
        Validation.Require(DateOnly.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date), "Date d'operation ou de valeur invalide.");
        return date;
    }
    private static DateOnly OperationDate(string value, DateOnly? start, DateOnly? end)
    {
        if (value.Length != 5) return ParseDate(value, null);
        Validation.Require(start != null && end != null, "Periode requise pour les dates sans annee.");
        var candidates = Enumerable.Range(start!.Value.Year, end!.Value.Year - start.Value.Year + 1)
            .Select(y => DateOnly.TryParseExact(value + "/" + y, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? (DateOnly?)d : null)
            .Where(d => d >= start && d <= end).ToArray();
        Validation.Require(candidates.Length == 1, "Date sans annee ambigue ou hors periode.");
        return candidates[0]!.Value;
    }
    private static string Normalize(string text) => new string(text.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToUpperInvariant();
    private static List<Line> Lines(IEnumerable<Letter> letters)
    {
        var result = new List<Line>();
        foreach (var letter in letters.OrderByDescending(l => l.StartBaseLine.Y).ThenBy(l => l.StartBaseLine.X))
        {
            if (result.Count == 0 || Math.Abs(result[^1].Y - letter.StartBaseLine.Y) > 2) result.Add(new(letter.StartBaseLine.Y, []));
            result[^1].Letters.Add(letter);
        }
        return result;
    }
    private static string Join(IEnumerable<Letter> letters)
    {
        var text = new StringBuilder(); Letter? previous = null;
        foreach (var letter in letters.OrderBy(l => l.StartBaseLine.X))
        {
            if (previous != null && letter.StartBaseLine.X - previous.EndBaseLine.X > 1.3) text.Append(' ');
            text.Append(letter.Value); previous = letter;
        }
        return Regex.Replace(text.ToString(), @"\s+", " ").Trim();
    }
}
