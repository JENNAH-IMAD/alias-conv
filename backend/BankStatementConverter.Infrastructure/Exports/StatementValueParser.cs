using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using BankStatementConverter.Application.Exports;

namespace BankStatementConverter.Infrastructure.Exports;

internal static class StatementValueParser
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    internal static (DateOnly? Operation, DateOnly? Value) CompleteDates(DateOnly? operation, DateOnly? value,
        string operationText, string valueText, ICollection<ExportError> warnings)
    {
        var completed = false;
        if (!operation.HasValue && value.HasValue && IsMissingDate(operationText))
        {
            operation = value;
            completed = true;
        }
        if (!value.HasValue && operation.HasValue && IsMissingDate(valueText))
        {
            value = operation;
            completed = true;
        }
        if (completed && !warnings.Any(w => w.Code == "PDF_DATES_COMPLETED"))
            warnings.Add(new("PDF_DATES_COMPLETED", "Une date absente ou non déterminée a été complétée avec l’autre date du même mouvement (opération ou valeur).", SourceField: "Date"));
        return (operation, value);
    }

    internal static bool IsMissingDate(string text)
    {
        var label = new string(text.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        label = Regex.Replace(label.ToUpperInvariant(), @"\s+", " ", RegexOptions.None, Timeout).Trim();
        return label is "" or "-" or "—" or "–" or "NON DETERMINE" or "NON DETERMINEE" or "N/A" or "ND" or "N.D.";
    }

    internal static decimal? Money(string text, string? currency = null)
    {
        text = text.Trim().Replace('\u2212', '-').Replace('\uFF0D', '-').Replace('\uFF0B', '+');
        var accountingNegative = text.StartsWith('(') && text.EndsWith(')');
        if (accountingNegative)
            text = text[1..^1].Trim();
        if (!string.IsNullOrWhiteSpace(currency))
        {
            // Only the selected currency is decoration; a foreign currency must remain unreadable.
            var label = currency.ToUpperInvariant() switch
            {
                "MAD" => @"MAD|DHS?\.?|DIRHAMS?",
                "EUR" => @"EUR|EUROS?|€",
                "USD" => @"USD|US\$|\$",
                "GBP" => @"GBP|£",
                _ => Regex.Escape(currency)
            };
            text = Regex.Replace(text, $@"\s*(?:\(\s*(?:{label})\s*\)|(?:{label}))\s*$", "", RegexOptions.IgnoreCase, Timeout).Trim();
            text = Regex.Replace(text, $@"^(?:\(\s*(?:{label})\s*\)|(?:{label}))\s*", "", RegexOptions.IgnoreCase, Timeout).Trim();
        }
        text = Regex.Replace(text, @"^([+-])\s+", "$1", RegexOptions.None, Timeout);
        // Spaces are thousands separators, not permission to concatenate neighboring cells.
        // In particular, a leaked date year ("2026 22460,90") must not become 202622460.90.
        if (Regex.IsMatch(text, @"\s", RegexOptions.None, Timeout))
        {
            if (!Regex.IsMatch(text, @"^[+-]?\d{1,3}(?:[ \u00a0\u202f]+\d{3})+[,.]\d{2}$", RegexOptions.None, Timeout))
                return null;
            text = Regex.Replace(text, @"[ \u00a0\u202f]+", "", RegexOptions.None, Timeout);
        }
        var match = Regex.Match(text, @"^([+-]?)(\d+(?:[.,]\d+)*)([.,])(\d{2})$", RegexOptions.None, Timeout);
        if (!match.Success)
            return null;
        var integer = match.Groups[2].Value;
        var decimalSeparator = match.Groups[3].Value[0];
        if (integer.Contains('.') || integer.Contains(','))
        {
            var groupSeparator = decimalSeparator == '.' ? ',' : '.';
            // Reject malformed or ambiguous grouping instead of changing the magnitude.
            if (integer.Contains(decimalSeparator))
                return null;
            var groups = integer.Split(groupSeparator);
            if (groups[0].Length is < 1 or > 3 || groups.Skip(1).Any(g => g.Length != 3))
                return null;
            integer = string.Concat(groups);
        }
        if (accountingNegative && match.Groups[1].Value.Length > 0)
            return null; // Conflicting signs must not silently change direction.
        return decimal.TryParse(match.Groups[1].Value + integer + "." + match.Groups[4].Value,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? accountingNegative ? -value : value : null;
    }

    internal static DateOnly? Date(string text, DateOnly? start, DateOnly? end, DateOnly? reference = null)
    {
        // OCR may split even the year over two lines: "01/03/20\n26".
        text = text.Contains('/') || text.Contains('-') || text.Contains('.')
            ? Regex.Replace(text.Trim(), @"\s+", "", RegexOptions.None, Timeout).Replace('-', '/').Replace('.', '/')
            : Regex.Replace(text.Trim(), @"\s+", "/", RegexOptions.None, Timeout);
        if (DateOnly.TryParseExact(text, ["dd/MM/yyyy", "d/MM/yyyy", "dd/MM/yy", "d/MM/yy", "yyyyMMdd"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        if (!start.HasValue || !Regex.IsMatch(text, @"^\d{1,2}/\d{2}$", RegexOptions.None, Timeout))
            return null;
        var candidates = new List<DateOnly>();
        for (var year = start.Value.Year - 1; year <= (end ?? start).Value.Year + 1; year++)
            if (DateOnly.TryParseExact($"{text}/{year:0000}", ["dd/MM/yyyy", "d/MM/yyyy"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                candidates.Add(date);
        // Operation dates prefer the period; value dates follow their operation, including adjacent years.
        return candidates.OrderBy(d => reference.HasValue || !end.HasValue || d >= start.Value && d <= end.Value ? 0 : 1)
            .ThenBy(d => Math.Abs(d.DayNumber - (reference ?? start.Value).DayNumber)).Select(d => (DateOnly?)d).FirstOrDefault();
    }
}
