using System.Globalization;
using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
namespace BankStatementConverter.Infrastructure.Accounting;
public static class EntryModelTextExport
{
    public const int FormatVersion = 2;
    public static readonly string[] Fields = ["ACCOUNT", "TITLE", "DESCRIPTION", "BANK", "NET", "GROSS", "TAX", "RATE", "DATE", "REFERENCE", "ABBREVIATION", "CURRENCY", "DEBIT_HT", "CREDIT_HT"];
    public static void Validate(EntryModelOutput output)
    {
        Validation.Require(output.FileExtension is "txt" or "csv" or "tsv", "Type de fichier invalide : TXT, CSV ou TSV attendu.");
        Validation.Require(output.Delimiter is ";" or "," or "\t" or "|", "Séparateur TXT invalide.");
        Validation.Require(output.Encoding is "utf-8" or "windows-1252", "Encodage TXT invalide.");
        Validation.Require(output.NumberCulture is "fr-FR" or "en-US", "Format des nombres invalide.");
        Validation.Require(string.IsNullOrEmpty(output.DateFormat) || DateFormats.Contains(output.DateFormat), "Format de date invalide.");
        Validation.Require(output.Columns is { Count: >= 1 and <= 12 } && output.Columns.All(c => c != null && Fields.Contains(c.Field) && !string.IsNullOrWhiteSpace(c.Label) && c.Label.Length <= 80 && !c.Label.Any(char.IsControl)), "Colonnes du modèle invalides.");
        Validation.Require(output.Columns.Select(c => c.Field).Distinct().Count() == output.Columns.Count, "Une colonne ne peut apparaître qu'une fois.");
        Validation.Require(output.Columns.Any(c => c.Display) && output.Columns.Any(c => c.Export), "Conservez au moins une colonne d'affichage et une colonne d'export.");
    }
    private static readonly string[] DateFormats = ["ddMMyy", "ddMMyyyy", "yyMMdd", "yyyyMMdd", "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "MM/dd/yyyy"];
    public static Encoding EncodingFor(EntryModelOutput output)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return output.Encoding == "utf-8" ? new UTF8Encoding(false, true) : Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }
    public static string Value(EntryResultRow row, string field, string culture, string dateFormat = "dd/MM/yyyy")
    {
        var number = CultureInfo.GetCultureInfo(culture);
        return field switch { "ACCOUNT" => row.AccountCode, "TITLE" => row.Title, "DESCRIPTION" => row.Description, "BANK" => row.Bank,
            "NET" => row.Net.ToString("0.00", number), "DEBIT_HT" => row.DebitHt.ToString("0.00", number), "CREDIT_HT" => row.CreditHt.ToString("0.00", number),
            "GROSS" => row.Gross.ToString("0.00", number), "TAX" => row.Tax.ToString("0.00", number), "RATE" => row.Rate.ToString("0.##", number),
            "DATE" => row.Date?.ToString(string.IsNullOrEmpty(dateFormat) ? "dd/MM/yyyy" : dateFormat, CultureInfo.InvariantCulture) ?? "", "REFERENCE" => row.Reference, "ABBREVIATION" => row.Abbreviation, "CURRENCY" => row.Currency,
            _ => throw new AppException(400, "Colonne inconnue.") };
    }
    public static byte[] Generate(EntryModelOutput output, IReadOnlyList<EntryResultRow> rows, bool structureOnly = false)
    {
        Validate(output); var columns = output.Columns.Where(c => c.Export).ToArray();
        string Cell(string value)
        {
            // PDF labels can wrap over several lines. A text export keeps one physical line per movement.
            var singleLine = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return singleLine.Contains(output.Delimiter, StringComparison.Ordinal) || singleLine.Contains('"')
                ? "\"" + singleLine.Replace("\"", "\"\"") + "\"" : singleLine;
        }
        var lines = new List<string>();
        if (output.IncludeHeader || structureOnly) lines.Add(string.Join(output.Delimiter, columns.Select(c => Cell(c.Label))));
        if (!structureOnly) foreach (var row in rows) lines.Add(string.Join(output.Delimiter, columns.Select(c => Cell(Value(row, c.Field, output.NumberCulture, output.DateFormat)))));
        try { return EncodingFor(output).GetBytes(lines.Count > 0 ? string.Join("\r\n", lines) + "\r\n" : ""); }
        catch (EncoderFallbackException) { throw new AppException(400, "Des caractères ne sont pas représentables dans l'encodage choisi. Sélectionnez UTF-8."); }
    }
}
