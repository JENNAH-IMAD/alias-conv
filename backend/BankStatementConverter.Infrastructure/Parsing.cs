using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using CsvHelper;
using CsvHelper.Configuration;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
namespace BankStatementConverter.Infrastructure;

public class PdfExtractionService : IPdfExtractionService
{
    public Task<string> ExtractAsync(Stream stream, CancellationToken ct)
    {
        try
        {
            using var document = PdfDocument.Open(stream);
            Validation.Require(document.NumberOfPages <= 200, "Maximum 200 pages par PDF.");
            var text = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                var pageText = ContentOrderTextExtractor.GetText(page);
                Validation.Require(!string.IsNullOrWhiteSpace(pageText), "Une page est vide ou scannée : OCR non installé. Aucun export partiel n'est généré.");
                text.AppendLine(pageText);
                Validation.Require(text.Length <= 2_000_000, "Texte extrait trop volumineux.");
            }
            return Task.FromResult(text.ToString());
        }
        catch (Exception ex) when (ex is not AppException and not OperationCanceledException)
        { throw new AppException(400, "PDF illisible, endommagé ou protégé par mot de passe."); }
    }
}
public static class AmountNormalizer
{
    public static decimal Parse(string value, string culture)
    {
        var clean = Regex.Replace(value.Trim(), @"[\s\u00A0\u202F]", "");
        if (clean.Length == 0) return 0;
        Validation.Require(Regex.IsMatch(clean, @"^-?\d+(?:[.,]\d+)*$"), $"Montant invalide : {value}.");
        // A single separator with three trailing digits is ambiguous; the configured culture decides.
        var decimalSeparator = CultureInfo.GetCultureInfo(culture).NumberFormat.NumberDecimalSeparator;
        if (clean.Contains(',') && clean.Contains('.')) decimalSeparator = clean.LastIndexOf(',') > clean.LastIndexOf('.') ? "," : ".";
        else if (Regex.IsMatch(clean, @"[.,]\d{1,2}$")) decimalSeparator = clean.Contains(',') ? "," : ".";
        var grouping = decimalSeparator == "," ? "." : ",";
        if (clean.Contains(grouping)) Validation.Require(Regex.IsMatch(clean, @"^-?\d{1,3}(?:" + Regex.Escape(grouping) + @"\d{3})+(?:" + Regex.Escape(decimalSeparator) + @"\d{1,2})?$"), "Groupement de chiffres invalide.");
        clean = clean.Replace(grouping, "").Replace(decimalSeparator, ".");
        Validation.Require(decimal.TryParse(clean, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount), "Montant hors limites.");
        Validation.Require(decimal.Round(amount, 2) == amount, "Plus de deux décimales : vérifier la culture du profil.");
        return amount;
    }
}
public class DelimitedStatementParser : IBankStatementParser
{
    public string Key => "delimited";
    public IReadOnlyList<BankTransaction> Parse(string text, BankStatementTemplate t)
    {
        var result = new List<BankTransaction>();
        var lines = text.Replace("\r", "").Split('\n').Where(x => !string.IsNullOrWhiteSpace(x)).Skip(t.SkipLines).ToArray();
        for (var i = 0; i < lines.Length; i++)
        {
            try
            {
                using var reader = new StringReader(lines[i]);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false, Delimiter = t.Delimiter });
                csv.Read(); var cells = csv.Parser.Record!;
                string Cell(int? col) => col == null ? "" : col < cells.Length ? cells[col.Value].Trim() : throw new AppException(400, "Colonne absente.");
                Validation.Require(DateOnly.TryParseExact(Cell(t.DateColumn), t.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date), "Date invalide.");
                var description = Cell(t.DescriptionColumn); Validation.Text(description, "Libellé", 1000);
                var debit = AmountNormalizer.Parse(Cell(t.DebitColumn), t.NumberCulture);
                var credit = AmountNormalizer.Parse(Cell(t.CreditColumn), t.NumberCulture);
                Validation.Require(!(debit != 0 && credit != 0) && (debit != 0 || credit != 0), "Une seule colonne débit/crédit doit être non nulle.");
                if (debit < 0) { credit = -debit; debit = 0; }
                if (credit < 0) { debit = -credit; credit = 0; }
                result.Add(new() { Date = date, Description = description, Debit = debit, Credit = credit, Reference = Cell(t.ReferenceColumn), Balance = string.IsNullOrWhiteSpace(Cell(t.BalanceColumn)) ? null : AmountNormalizer.Parse(Cell(t.BalanceColumn), t.NumberCulture) });
            }
            catch (Exception ex) when (ex is AppException or CsvHelperException or FormatException)
            { throw new AppException(400, $"Ligne {i + t.SkipLines + 1} invalide : {ex.Message}. Aucun export partiel généré."); }
        }
        Validation.Require(result.Count > 0, "Aucune transaction trouvée. Vérifiez le profil d'extraction.");
        return result;
    }
}
