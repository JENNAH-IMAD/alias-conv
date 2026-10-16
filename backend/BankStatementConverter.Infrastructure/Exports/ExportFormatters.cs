using System.Globalization;
namespace BankStatementConverter.Infrastructure.Exports;

public sealed class ExportDateFormatter
{
    public string Format(DateOnly date, string? format)
    {
        var selected = string.IsNullOrEmpty(format) ? "yyyy-MM-dd" : format;
        if (!Validation.DateFormats.Contains(selected))
            throw new ArgumentException("Format de date invalide.");
        return date.ToString(selected, CultureInfo.InvariantCulture);
    }
}
public sealed class ExportNumberFormatter
{
    public string Format(decimal number, string? format, string culture)
    {
        var selected = string.IsNullOrEmpty(format) ? "0.00" : format;
        if (selected is not ("0.00" or "0.000" or "0.##"))
            throw new ArgumentException("Format numérique invalide.");
        if (culture is not ("fr-FR" or "en-US"))
            throw new ArgumentException("Culture numérique invalide.");
        return number.ToString(selected, CultureInfo.GetCultureInfo(culture));
    }
}
