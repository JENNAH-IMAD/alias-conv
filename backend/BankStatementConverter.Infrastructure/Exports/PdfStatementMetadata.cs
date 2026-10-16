using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Exports;

// Completes document metadata from OCR/native text. Accounting fields are never invented.
public static class PdfStatementMetadata
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    private const string FullDate = @"\d{2}(?:[/.-]|[ \t]+)\d{2}(?:[/.-]|[ \t]+)(?:\d{4}|\d{2})(?!\d)";
    private static string Fold(string s) => new(s.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).Select(char.ToUpperInvariant).ToArray());
    private static Match Match(string s, string pattern) => Regex.Match(s, pattern, RegexOptions.CultureInvariant, Timeout);
    private static DateOnly? Date(string s) => StatementValueParser.Date(s, null, null);
    private static decimal? Money(string text) => StatementValueParser.Money(text);
    private static IReadOnlyList<decimal> Amounts(string text) => Regex.Matches(text,
        @"(?<![\w/])[-+]?\d+(?:(?:[ \u00a0\u202f]+|[.,])\d{3})*[,.]\d{2}(?!\d)", RegexOptions.None, Timeout)
        .Select(m => Money(m.Value)).Where(n => n.HasValue).Select(n => n!.Value).ToArray();
    private static IEnumerable<string> Lines(PdfTextPage page)
    {
        var groups = new List<List<PdfTextBox>>();
        foreach (var b in page.Boxes.OrderBy(b => (b.Top + b.Bottom) / 2).ThenBy(b => b.Left))
        {
            if (groups.Count == 0 || Math.Abs((groups[^1][0].Top + groups[^1][0].Bottom - b.Top - b.Bottom) / 2) > 3)
                groups.Add([]);
            groups[^1].Add(b);
        }
        return groups.SelectMany(g => string.Join(" ", g.OrderBy(b => b.Left).Select(b => b.Text)).Split('\n'));
    }
    public static PdfStatementExtraction Complete(PdfStatementExtraction extraction, BankStatement? manual = null)
    {
        extraction = extraction with
        {
            Statement = StatementAmounts.Normalize(extraction.Statement)
        };
        if (manual != null)
            manual = StatementAmounts.Normalize(manual);
        var document = extraction.RawDocument;
        if (document == null)
            return extraction;
        var s = extraction.Statement;
        var warnings = extraction.Warnings.Where(w => w.Code != "PDF_TOTALS_REVIEW").ToList();
        var pages = document.NativePages ?? document.Pages;
        var lines = pages.SelectMany(Lines).Concat(document.Pages.SelectMany(p => p.Boxes.SelectMany(b => b.Text.Split('\n')))).ToArray();
        var text = string.Join("\n", lines);
        var holderLine = lines.Select(line => Match(line, @"(?i)^\s*(?:Titulaire(?:\s+du\s+compte)?|Account\s+holder)\s*:\s*(.+?)\s*$")).FirstOrDefault(m => m.Success);
        var holder = manual?.AccountHolder ?? s.AccountHolder ?? (holderLine?.Success == true ? holderLine.Groups[1].Value : null);
        var period = Match(Fold(text), $@"(?:PERIODE\s*(?::|DU)?\s*|\bDU\s+)({FullDate})\s*(?:AU|-)\s*({FullDate})");
        var start = manual?.PeriodStart ?? s.PeriodStart ?? (period.Success ? Date(period.Groups[1].Value) : null);
        var end = manual?.PeriodEnd ?? s.PeriodEnd ?? (period.Success ? Date(period.Groups[2].Value) : null);
        var monthCaption = Match(Fold(text), @"RELEVE\s+DE\s+COMPTE\s+DU\s+MOIS\s+DE\s*:?\s*(\d{2})/(\d{4})\b");
        if (monthCaption.Success && DateOnly.TryParseExact("01/" + monthCaption.Groups[1].Value + "/" + monthCaption.Groups[2].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var printedMonth))
        {
            start ??= printedMonth;
            end ??= new DateOnly(printedMonth.Year, printedMonth.Month, DateTime.DaysInMonth(printedMonth.Year, printedMonth.Month));
        }
        // Some monthly exports print only full movement dates, without a period caption.
        // Infer the calendar month only when all dated movements agree on that month.
        var movementDates = s.Transactions.Where(t => t.Date.HasValue).Select(t => t.Date!.Value).ToArray();
        if ((!start.HasValue || !end.HasValue) && Match(Fold(text), @"RELEVE\s+MENSUEL\b").Success
            && movementDates.Length > 0 && movementDates.Select(d => (d.Year, d.Month)).Distinct().Count() == 1)
        {
            var month = movementDates[0];
            start ??= new DateOnly(month.Year, month.Month, 1);
            end ??= new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
            warnings.Add(new("PDF_PERIOD_INFERRED", "La période mensuelle a été complétée à partir des dates des mouvements. Vérifiez-la avec le relevé.", SourceField: "PeriodEnd"));
        }
        var accountLines = pages.Take(1).SelectMany(p => Lines(p with { Boxes = p.Boxes.Where(b => b.Top < p.Height * .3).ToArray() }));
        var printedAccount = accountLines.Select(line => Match(Fold(line), @"^(?:N[°ºO]\s*DE\s+|NUMERO\s+DE\s+)?COMPTE\s*:?\s*([0-9][0-9 ]{5,35}[0-9](?: ?[A-Z]\b)?)\b"))
            .FirstOrDefault(m => m.Success);
        var accountNumber = manual?.AccountNumber ?? s.AccountNumber ?? (printedAccount?.Success == true ? printedAccount.Groups[1].Value.Trim() : null);
        if (manual?.AccountNumber == null && accountNumber != null && !accountNumber.Any(char.IsDigit))
            accountNumber = null;
        if (accountNumber == null)
        {
            foreach (var table in document.Pages.Take(1).SelectMany(p => p.Tables ?? []))
                for (var i = 0; i + 1 < table.Rows.Count; i++)
                {
                    var captions = table.Rows[i];
                    var indices = captions.Select((c, j) => (c, j)).Where(x => Match(Fold(x.c), @"^(?:N[°ºO]?\s*(?:DE\s*)?|NUMERO\s*(?:DE\s*)?)COMPTE$").Success).Select(x => x.j).ToArray();
                    if (indices.Length != 1 || indices[0] >= table.Rows[i + 1].Count)
                        continue;
                    var value = table.Rows[i + 1][indices[0]].Trim();
                    if (Match(value, @"^[0-9][0-9 ]{4,35}[0-9]$").Success)
                        accountNumber = value;
                }
        }
        var headerText = string.Join(" ", pages.Take(1).Concat(document.Pages.Take(1)).SelectMany(p => p.Boxes.Where(b => b.Top < p.Height * .3).Select(b => b.Text)));
        var detectedBanks = MoroccanBankCatalog.IdentityCodes(headerText);
        var bankName = s.BankName;
        var identity = MoroccanBankCatalog.IdentityCodes(bankName);
        if (manual == null && detectedBanks.Length == 1 && !identity.Contains(detectedBanks[0]))
        {
            var detectedBank = MoroccanBankCatalog.Entries.Single(b => b.Code == detectedBanks[0]);
            bankName = detectedBank.Name;
            identity = [detectedBank.Code];
            warnings.Add(new("PDF_BANK_DETECTED", "La banque a été identifiée dans l’en-tête du relevé. Vérifiez qu’elle correspond au compte choisi.", SourceField: "BankName"));
        }
        var code = identity.Length == 1 ? identity[0] == "AWB" ? "ATT" : identity[0] : s.BankCode;
        code = manual?.BankCode ?? code;
        var explicitNumber = Match(Fold(text), @"\b(?:RELEVE\s*(?:BANCAIRE\s*)?(?:N[°ºO]\s*:?\s*|NUMERO\s*:?\s*)|NUMERO\s+(?:DU\s+)?RELEVE\s*:?\s*)([A-Z0-9][A-Z0-9/_-]{0,79})\b");
        var number = manual?.StatementNumber ?? s.StatementNumber ?? (explicitNumber.Success ? explicitNumber.Groups[1].Value : null);
        if (number == null && !string.IsNullOrWhiteSpace(code) && start.HasValue)
        {
            number = "RB" + code.Trim().ToUpperInvariant() + start.Value.ToString("MMyyyy", CultureInfo.InvariantCulture);
            warnings.Add(new("PDF_STATEMENT_NUMBER_GENERATED", "Numéro de relevé généré à partir du code banque et du mois de la période. Vous pouvez le modifier.", SourceField: "StatementNumber"));
        }

        decimal? printedDebit = null, printedCredit = null;
        foreach (var page in document.Pages)
        {
            PdfTableColumnMapping? header = null;
            foreach (var table in page.Tables ?? [])
                foreach (var row in table.Rows)
                {
                    header = StructuredStatementNormalizer.Header(row) ?? header;
                    if (header == null || !Match(Fold(string.Join(" ", row)), @"\bTOTAL\b").Success || Match(Fold(string.Join(" ", row)), @"REPORT|PAGE").Success)
                        continue;
                    if (header.Debit < row.Count && Money(row[header.Debit]) is { } d)
                        printedDebit = d;
                    if (header.Credit < row.Count && Money(row[header.Credit]) is { } c)
                        printedCredit = c;
                }
        }
        // Native totals can be read only when the page explicitly identifies debit/credit column order.
        decimal? nativeDebit = null, nativeCredit = null;
        foreach (var page in pages)
        {
            var pageLines = Lines(page).ToArray();
            var headers = pageLines.Select(Fold).Where(l => l.Contains("DEBIT") && l.Contains("CREDIT") && !l.Contains("TOTAL")).ToArray();
            var normalOrder = headers.Any(l => l.IndexOf("DEBIT", StringComparison.Ordinal) < l.IndexOf("CREDIT", StringComparison.Ordinal));
            var reverseOrder = headers.Any(l => l.IndexOf("CREDIT", StringComparison.Ordinal) < l.IndexOf("DEBIT", StringComparison.Ordinal));
            foreach (var line in pageLines)
            {
                var folded = Fold(line);
                if (!Match(folded, @"\bTOTAL\b").Success || Match(folded, @"REPORT|PAGE|SOLDE").Success)
                    continue;
                var values = Amounts(line);
                if (values.Count == 2 && normalOrder != reverseOrder)
                {
                    nativeDebit = values[normalOrder ? 0 : 1];
                    nativeCredit = values[normalOrder ? 1 : 0];
                }
                if (values.Count == 1 && folded.Contains("DEBIT") && !folded.Contains("CREDIT"))
                    nativeDebit = values[0];
                if (values.Count == 1 && folded.Contains("CREDIT") && !folded.Contains("DEBIT"))
                    nativeCredit = values[0];
            }
        }
        printedDebit ??= nativeDebit;
        printedCredit ??= nativeCredit;
        var opening = manual?.OpeningBalance ?? s.OpeningBalance;
        var closing = manual?.ClosingBalance ?? s.ClosingBalance;
        foreach (var line in lines)
        {
            var balanceKind = StatementFieldNames.Balance(line);
            if (balanceKind == null)
                continue;
            var caption = Match(line, $@"(?i)\bSOLDE\s+(?:PRECEDENT|PRÉCÉDENT|INITIAL|FINAL|NOUVEAU|DE\s+DEPART|DE\s+DÉPART)\s*(?:AU\s+)?(?:{FullDate})?\s*:\s*([-+]?\d+(?:(?:[ \u00a0\u202f]+|[.,])\d{{3}})*[,.]\d{{2}})\s*(CR|DR|D|C)?\s*$");
            if (!caption.Success)
                continue;
            var amount = Money(caption.Groups[1].Value);
            if (!amount.HasValue)
                continue;
            if (caption.Groups[2].Value.ToUpperInvariant() is "D" or "DR")
                amount = -Math.Abs(amount.Value);
            if (balanceKind == "OpeningBalance")
                opening ??= amount;
            else
                closing ??= amount;
        }
        // Nullable missing side is normal; explicitly reject negative magnitudes and OCR amount warnings.
        var complete = s.Transactions.Count > 0 && s.Transactions.All(t => t.Debit.HasValue != t.Credit.HasValue && !(t.Debit < 0) && !(t.Credit < 0))
            && !warnings.Any(w => w.Code == "PDF_AMOUNT_UNREADABLE");
        decimal? debit = s.TotalDebit, credit = s.TotalCredit;
        string? convention = s.BalanceConvention;
        if (complete)
        {
            try
            {
                var sumDebit = s.Transactions.Sum(t => t.Debit ?? 0m);
                var sumCredit = s.Transactions.Sum(t => t.Credit ?? 0m);
                if (opening.HasValue && printedDebit == sumDebit + Math.Max(-opening.Value, 0m) && printedCredit == sumCredit + Math.Max(opening.Value, 0m)
                    && (printedDebit != sumDebit || printedCredit != sumCredit))
                {
                    debit ??= sumDebit;
                    credit ??= sumCredit;
                    warnings.Add(new("PDF_TOTALS_INCLUDE_OPENING_BALANCE", "Les totaux imprimés incluent le solde initial. Les totaux de la fiche correspondent uniquement aux mouvements.", SourceField: "TotalDebit"));
                }
                else
                {
                    debit ??= printedDebit ?? sumDebit;
                    credit ??= printedCredit ?? sumCredit;
                }
                if (!printedDebit.HasValue || !printedCredit.HasValue)
                    warnings.Add(new("PDF_TOTALS_COMPUTED", "Les totaux absents ou non lisibles ont été calculés à partir des mouvements extraits. Vérifiez-les avec le relevé.", SourceField: "TotalDebit"));
                if (convention == null && opening.HasValue && closing.HasValue)
                {
                    if (opening + sumCredit - sumDebit == closing)
                        convention = "CREDIT_INCREASES";
                    else if (opening + sumDebit - sumCredit == closing)
                        convention = "DEBIT_INCREASES";
                }
            }
            catch (OverflowException) { warnings.Add(new("PDF_METADATA_OVERFLOW", "Montants trop grands pour compléter automatiquement les totaux et la convention.")); }
        }
        else
        {
            debit ??= printedDebit;
            credit ??= printedCredit;
        }
        var openingDate = manual?.OpeningBalanceDate ?? s.OpeningBalanceDate ?? start;
        var closingDate = manual?.ClosingBalanceDate ?? s.ClosingBalanceDate ?? end;
        return extraction with
        {
            Statement = s with
            {
                BankName = manual?.BankName ?? bankName,
                BankCode = code,
                AccountNumber = accountNumber,
                AccountHolder = holder,
                StatementNumber = number,
                PeriodStart = start,
                PeriodEnd = end,
                OpeningBalance = opening,
                ClosingBalance = closing,
                OpeningBalanceDate = openingDate,
                ClosingBalanceDate = closingDate,
                OpeningBalanceValueDate = manual?.OpeningBalanceValueDate ?? s.OpeningBalanceValueDate ?? openingDate,
                ClosingBalanceValueDate = manual?.ClosingBalanceValueDate ?? s.ClosingBalanceValueDate ?? closingDate,
                TotalDebit = manual?.TotalDebit ?? debit,
                TotalCredit = manual?.TotalCredit ?? credit,
                BalanceConvention = manual?.BalanceConvention ?? convention,
                OpeningBalanceDescription = manual?.OpeningBalanceDescription ?? s.OpeningBalanceDescription,
                ClosingBalanceDescription = manual?.ClosingBalanceDescription ?? s.ClosingBalanceDescription
            },
            Warnings = warnings
        };
    }
}
