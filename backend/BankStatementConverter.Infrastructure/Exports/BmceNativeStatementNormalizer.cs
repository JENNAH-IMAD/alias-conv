using System.Globalization;
using System.Text.RegularExpressions;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Exports;

// The legacy BOA form draws its captions as outlines. Its monospaced data remains selectable.
internal static class BmceNativeStatementNormalizer
{
    private sealed record Row(double Bottom, IReadOnlyList<PdfTextBox> Boxes)
    {
        public string Text => string.Join(" ", Boxes.Select(b => b.Text));
        public string Cell(double left, double right) => string.Join(" ", Boxes.Where(b => b.Left >= left && b.Left < right).Select(b => b.Text));
    }
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    private static Match Match(string text, string pattern) => Regex.Match(text, pattern, RegexOptions.CultureInvariant, Timeout);
    private static List<Row> Rows(PdfTextPage page)
    {
        var groups = new List<List<PdfTextBox>>();
        foreach (var box in page.Boxes.Where(b => !string.IsNullOrWhiteSpace(b.Text)).OrderBy(b => b.Bottom).ThenBy(b => b.Left))
        {
            if (groups.Count == 0 || Math.Abs(groups[^1][0].Bottom - box.Bottom) > 1.5)
                groups.Add([]);
            groups[^1].Add(box);
        }
        return groups.Select(g => new Row(g.Average(b => b.Bottom), g.OrderBy(b => b.Left).ToArray())).ToList();
    }
    private static decimal? Money(string text)
    {
        text = Regex.Replace(text, @"\s+", "", RegexOptions.None, Timeout);
        return Match(text, @"^\d+,\d{2}$").Success && decimal.TryParse(text.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n) ? n : null;
    }
    private static DateOnly FullDate(string text) => DateOnly.ParseExact(text, "dd MM yyyy", CultureInfo.InvariantCulture);
    private static DateOnly ShortDate(string text, DateOnly start, DateOnly end)
    {
        var candidates = Enumerable.Range(start.Year - 1, end.Year - start.Year + 3)
            .Select(y => DateOnly.TryParseExact(text + $" {y}", "dd MM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? (DateOnly?)d : null)
            .Where(d => d.HasValue).Select(d => d!.Value).OrderBy(d => Math.Abs(d.DayNumber - start.DayNumber)).ToList();
        Require(candidates.Count > 0, "Date BMCE invalide.");
        return candidates[0];
    }
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new AppException(422, message);
    }

    public static PdfStatementExtraction? TryNormalize(PdfTextDocument document, string bankName, string bankCode, string currency, CancellationToken ct)
    {
        if (document.Method != "NATIVE_PDF" || document.Pages.Count == 0)
            return null;
        var first = Rows(document.Pages[0]);
        var rib = first.Select(r => Regex.Replace(r.Text, @"\s", "", RegexOptions.None, Timeout)).FirstOrDefault(t => Match(t, @"^011\d{21}$").Success);
        // Outlined logos are absent from the text layer. Require the bank's RIB prefix
        // together with the strict folio/period/column checks below before recognizing it.
        if (rib == null && !bankCode.Equals("BMCE", StringComparison.OrdinalIgnoreCase)
            && !MoroccanBankCatalog.IdentityCodes(bankName).Contains("BMCE"))
            return null;
        const string periodPattern = @"\b1/(\d+)\s+(\d{2} \d{2} \d{4})\s+(\d{2} \d{2} \d{4})\b";
        var period = first.Select(r => Match(r.Text, periodPattern)).FirstOrDefault(m => m.Success);
        var openingRow = first.FirstOrDefault(r => Match(r.Text, @"^\d{2} \d{2} SOLDE\b").Success);
        if (period == null || openingRow == null)
            return null;
        Require(currency == "MAD", "Ce relevé BMCE en dirhams doit être importé en MAD.");
        Require(int.Parse(period.Groups[1].Value, CultureInfo.InvariantCulture) == document.Pages.Count, "Le relevé BMCE est incomplet : nombre de pages incorrect.");
        var start = FullDate(period.Groups[2].Value);
        var end = FullDate(period.Groups[3].Value);
        Require(end >= start && end.Year - start.Year <= 1, "Période BMCE invalide.");
        var transactions = new List<StatementTransaction>();
        decimal? opening = null, closing = null;
        decimal cumulativeDebit = 0, cumulativeCredit = 0;
        foreach (var page in document.Pages)
        {
            ct.ThrowIfCancellationRequested();
            var rows = Rows(page);
            var scale = page.Width / 594d;
            string Cell(Row r, double left, double right) => r.Cell(left * scale, right * scale);
            decimal? Debit(Row r) => Money(Cell(r, 355, 455));
            decimal? Credit(Row r) => Money(Cell(r, 455, 550));
            var folio = rows.SingleOrDefault(r => Match(r.Text, $@"\b{page.Number}/{document.Pages.Count}\s+{Regex.Escape(period.Groups[2].Value)}\s+{Regex.Escape(period.Groups[3].Value)}\b").Success);
            Require(folio != null, $"Période ou folio BMCE manquant sur la page {page.Number}.");
            var openingOrReport = rows.FirstOrDefault(r => r.Bottom > folio!.Bottom && Match(r.Text, page.Number == 1 ? @"^\d{2} \d{2} SOLDE\b" : @"^REPORT\b").Success);
            Require(openingOrReport != null, $"Solde/report BMCE manquant sur la page {page.Number}.");
            if (page.Number == 1)
            {
                var d = Debit(openingOrReport!);
                var c = Credit(openingOrReport!);
                Require(d.HasValue != c.HasValue, "Solde initial BMCE ambigu.");
                opening = c ?? -d;
            }
            else
                Require(Debit(openingOrReport!) == cumulativeDebit && Credit(openingOrReport!) == cumulativeCredit, $"Report BMCE incohérent sur la page {page.Number}.");
            var body = rows.Where(r => r.Bottom > openingOrReport!.Bottom).ToList();
            var total = body.LastOrDefault(r => r.Boxes.All(b => b.Left >= 355 * scale) && Debit(r).HasValue && Credit(r).HasValue);
            Require(total != null, $"Totaux BMCE absents sur la page {page.Number}.");
            StatementTransaction? current = null;
            foreach (var row in body.Where(r => r.Bottom < total!.Bottom))
            {
                var date = Cell(row, 35, 78);
                var value = Cell(row, 315, 355);
                var description = Cell(row, 78, 315);
                if (Match(date, @"^\d{2} \d{2}$").Success)
                {
                    var d = Debit(row);
                    var c = Credit(row);
                    Require(d.HasValue != c.HasValue && Match(value, @"^\d{2} \d{2}$").Success && description.Length > 0, $"Mouvement BMCE incomplet sur la page {page.Number}.");
                    current = new()
                    {
                        Sequence = transactions.Count + 1,
                        Date = ShortDate(date, start, end),
                        ValueDate = ShortDate(value, start, end),
                        Description = description,
                        Debit = d,
                        Credit = c,
                        Amount = d ?? c,
                        Direction = d.HasValue ? "DEBIT" : "CREDIT",
                        Currency = currency
                    };
                    transactions.Add(current);
                    cumulativeDebit += d ?? 0;
                    cumulativeCredit += c ?? 0;
                }
                else
                {
                    Require(current != null && date.Length == 0 && value.Length == 0 && Debit(row) == null && Credit(row) == null && description.Length > 0,
                        $"Ligne BMCE non reconnue sur la page {page.Number}.");
                    current = current! with
                    {
                        Description = current!.Description + "\n" + description
                    };
                    transactions[^1] = current;
                }
            }
            Require(Debit(total!) == cumulativeDebit && Credit(total!) == cumulativeCredit, $"Les totaux imprimés BMCE diffèrent des mouvements sur la page {page.Number}.");
            var after = body.Where(r => r.Bottom > total!.Bottom).ToList();
            if (page.Number == document.Pages.Count)
            {
                Require(after.Count == 1 && Debit(after[0]).HasValue != Credit(after[0]).HasValue, "Solde final BMCE absent ou ambigu.");
                closing = Credit(after[0]) ?? -Debit(after[0]);
            }
            else
                Require(after.Count == 0, $"Texte inattendu après les totaux BMCE sur la page {page.Number}.");
        }
        Require(opening + transactions.Sum(t => t.Credit ?? 0) - transactions.Sum(t => t.Debit ?? 0) == closing, "Les soldes BMCE ne concordent pas avec les mouvements.");
        if (rib != null && !MoroccanBankCatalog.IdentityCodes(bankName).Contains("BMCE"))
        {
            bankName = "BANK OF AFRICA";
            bankCode = "BMCE";
        }
        var statement = new BankStatement
        {
            BankName = bankName,
            BankCode = bankCode,
            Currency = currency,
            AccountNumber = rib,
            PeriodStart = start,
            PeriodEnd = end,
            OpeningBalance = opening,
            ClosingBalance = closing,
            OpeningBalanceDate = ShortDate(openingRow.Cell(35, 78), start, end),
            ClosingBalanceDate = end,
            OpeningBalanceDescription = "SOLDE",
            ClosingBalanceDescription = "Nouveau solde",
            BalanceConvention = "CREDIT_INCREASES",
            Transactions = transactions,
            TotalDebit = transactions.Sum(t => t.Debit ?? 0),
            TotalCredit = transactions.Sum(t => t.Credit ?? 0)
        };
        return PdfStatementMetadata.Complete(new(statement, [new("PDF_REVIEW_REQUIRED", "Format BMCE natif : reports, totaux de chaque page et solde final contrôlés. Vérifiez les libellés avec le PDF avant validation.")], document));
    }
}
