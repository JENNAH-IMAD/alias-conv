using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace BankStatementConverter.Infrastructure.Exports;

public static class SageTextCompatibility
{
    public const string ProfileVersion = "Sage-text-v1:";
    public static string Ascii(string text)
    {
        text = text.Replace("œ", "oe").Replace("Œ", "OE").Replace("æ", "ae").Replace("Æ", "AE")
            .Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"')
            .Replace('–', '-').Replace('—', '-').Replace('−', '-').Replace("…", "...").Replace('\u00a0', ' ').Replace('\u202f', ' ');
        return new string(text.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
    }
    public static string Description(string text, bool x3) => Regex.Replace(x3 ? text.Replace(';', ' ').Replace(',', ' ').Replace('"', '\'') : text, @"\s+", " ").Trim();
    public static string Direction(string value) => value.ToUpperInvariant() switch { "CREDIT" => "C", "DEBIT" => "D", _ => value };
}
