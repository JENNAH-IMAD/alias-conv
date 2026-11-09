using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Exports;

// A year must come from this statement, never from the clock or an export example.
internal sealed record StatementDateContext(DateOnly? Start, DateOnly? End,
    DateOnly? PeriodStart = null, DateOnly? PeriodEnd = null)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    private const string PrintedDate = @"(?<!\d)(?:\d{4}[-/.]\d{1,2}[-/.]\d{1,2}|\d{1,2}[-/.\s]\d{1,2}[-/.\s](?:\d{4}|\d{2})|\d{1,2}\s+[\p{L}.]+\s+\d{4})(?!\d)";
    private static string Fold(string text) => new(text.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).Select(char.ToUpperInvariant).ToArray());
    private static DateOnly? DateIn(string text) => Regex.Matches(text, PrintedDate, RegexOptions.None, Timeout)
        .Select(m => StatementValueParser.Date(m.Value, null, null)).FirstOrDefault(d => d.HasValue);

    internal static StatementDateContext Resolve(PdfTextDocument document, DateOnly? start = null,
        DateOnly? end = null, BankStatement? statement = null)
    {
        start ??= statement?.PeriodStart;
        end ??= statement?.PeriodEnd;
        var pages = (document.NativePages ?? []).Concat(document.Pages).ToArray();
        var lines = pages.SelectMany(PdfStatementMetadata.Lines)
            .Concat(pages.SelectMany(p => (p.Markdown ?? "").Split('\n')))
            .Concat(pages.SelectMany(p => p.Tables ?? []).SelectMany(t => t.Rows.Select(r => string.Join(" ", r))))
            .Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        var text = Fold(string.Join("\n", lines));
        var period = Regex.Match(text, $@"(?:PERIODE\s*(?::|DU)?\s*|\bDU\s+)({PrintedDate})\s*(?:AU|A|—|–|-)\s*({PrintedDate})", RegexOptions.None, Timeout);
        if (period.Success)
        {
            start ??= StatementValueParser.Date(period.Groups[1].Value, null, null);
            end ??= StatementValueParser.Date(period.Groups[2].Value, null, null);
        }
        var balances = lines.Where(line => StatementFieldNames.IsBalance(line)
            && !StatementFieldNames.IsAvailableBalance(line)
            && !Regex.IsMatch(Fold(line), @"\bREPORT(?:E)?\s+(?:DE\s+)?(?:PAGE|INTERMEDIAIRE)\b", RegexOptions.None, Timeout))
            .Select(line => (Kind: StatementFieldNames.Balance(line), Date: DateIn(line))).Where(b => b.Date.HasValue).ToArray();
        var opening = statement?.OpeningBalanceDate ?? balances.FirstOrDefault(b => b.Kind == "OpeningBalance").Date;
        var closing = statement?.ClosingBalanceDate ?? balances.LastOrDefault(b => b.Kind == "ClosingBalance").Date;
        var distinctBalances = balances.Select(b => b.Date!.Value).Distinct().Order().ToArray();
        if (distinctBalances.Length >= 2)
        {
            opening ??= distinctBalances[0];
            closing ??= distinctBalances[^1];
        }
        start ??= opening;
        end ??= closing;
        // A month caption is an actual period; an edition date is only a reference.
        foreach (var line in lines.Where(l => Regex.IsMatch(Fold(l), @"\b(RELEVE|EXTRAIT|PERIODE|MOIS|STATEMENT)\b", RegexOptions.None, Timeout)))
        {
            var caption = Regex.Match(Fold(line), @"\b(?:MOIS(?:\s+DE)?\s*:?\s*|PERIODE\s*:?\s*|(?:RELEVE|EXTRAIT)(?:\s+(?:BANCAIRE|DE\s+COMPTE))?\s*:?\s*)(\d{1,2}|[A-Z.]+)[/\s-]+(\d{4})\b", RegexOptions.None, Timeout);
            if (!caption.Success)
                continue;
            var month = int.TryParse(caption.Groups[1].Value, out var n) ? n : StatementValueParser.Month(caption.Groups[1].Value);
            if (month is not (>= 1 and <= 12))
                continue;
            var year = int.Parse(caption.Groups[2].Value, CultureInfo.InvariantCulture);
            if (year == 0)
                continue;
            start ??= new DateOnly(year, month.Value, 1);
            end ??= new DateOnly(year, month.Value, DateTime.DaysInMonth(year, month.Value));
        }
        if (start.HasValue || end.HasValue)
            return new(start ?? end, end ?? start, start, end);
        if (distinctBalances.Length == 1)
            return new(distinctBalances[0], distinctBalances[0]);
        var headers = pages.SelectMany(p => PdfStatementMetadata.Lines(p with
        {
            Boxes = p.Boxes.Where(b => b.Top < p.Height * .35).ToArray()
        })).Where(l => Regex.IsMatch(Fold(l), @"\b(RELEVE|EXTRAIT|EDITION|EDITE|ARRETE|STATEMENT|DATE|ANNEE|EXERCICE)\b", RegexOptions.None, Timeout)
            || StatementValueParser.Date(l, null, null).HasValue).ToArray();
        var dates = headers.Select(DateIn).Where(d => d.HasValue).Select(d => d!.Value).Distinct().ToArray();
        // Conflicting header dates are not a reliable substitute for a printed period.
        if (dates.Length == 1)
            return new(dates[0], dates[0]);
        var years = headers.SelectMany(l => Regex.Matches(l, @"(?<!\d)(?:19|20)\d{2}(?!\d)", RegexOptions.None, Timeout))
            .Select(m => int.Parse(m.Value, CultureInfo.InvariantCulture)).Distinct().ToArray();
        return years.Length == 1
            ? new(new DateOnly(years[0], 1, 1), new DateOnly(years[0], 12, 31))
            : new(null, null);
    }

    internal PdfStructuredTable CompleteTable(PdfStructuredTable table, ref (int? Operation, int? Value, int Width) columns)
    {
        var operation = table.Rows.FirstOrDefault()?.Count == columns.Width ? columns.Operation : null;
        var value = table.Rows.FirstOrDefault()?.Count == columns.Width ? columns.Value : null;
        var rows = new List<IReadOnlyList<string>>();
        foreach (var row in table.Rows)
        {
            var fields = row.Select(StatementFieldNames.Column).ToArray();
            if (fields.Any(f => f is "Date" or "ValueDate") && fields.Any(f => f == "Description"))
            {
                operation = Array.IndexOf(fields, "Date") is var op && op >= 0 ? op : null;
                value = Array.IndexOf(fields, "ValueDate") is var val && val >= 0 ? val : null;
                columns = (operation, value, row.Count);
                rows.Add(row);
                continue;
            }
            var cells = row.ToArray();
            DateOnly? Complete(int? column, DateOnly? reference = null)
            {
                if (!column.HasValue || column.Value >= cells.Length)
                    return null;
                var text = cells[column.Value];
                var full = StatementValueParser.Date(text, null, null);
                var date = full ?? StatementValueParser.Date(text, Start, End, reference,
                    requireWithinPeriod: column == operation && PeriodStart.HasValue && PeriodEnd > PeriodStart);
                if (date.HasValue && (!full.HasValue
                    || Regex.IsMatch(text.Trim(), @"^\d{1,2}[/.-]\d{1,2}[/.-]\d{2}$", RegexOptions.None, Timeout)))
                    cells[column.Value] = date.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                return date;
            }
            var date = Complete(operation);
            var valueDate = Complete(value, date);
            if (!date.HasValue)
                Complete(operation, valueDate);
            rows.Add(cells);
        }
        return table with
        {
            Rows = rows
        };
    }
}
