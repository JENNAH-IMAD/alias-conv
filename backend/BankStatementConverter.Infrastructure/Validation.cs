using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using BankStatementConverter.Application;
namespace BankStatementConverter.Infrastructure;

public static class Validation
{
    public static void Require(bool condition, string message)
    {
        if (!condition)
            throw new AppException(400, message);
    }
    public static void Text(string? value, string name, int max = 200) => Require(!string.IsNullOrWhiteSpace(value) && value.Length <= max, $"{name} obligatoire (maximum {max} caractères).");
    public static void Email(string? value) => Require(value is not null && value.Length <= 254 && new EmailAddressAttribute().IsValid(value), "Adresse e-mail invalide.");
    public static void Client(ClientInput x)
    {
        Text(x.Name, "Nom");
        Text(x.LegalName, "Raison sociale");
        Require(x.ICE is not null && Regex.IsMatch(x.ICE, @"^\d{15}$"), "ICE : 15 chiffres requis.");
        Text(x.IF, "IF", 30);
        Text(x.RC, "RC", 40);
        Email(x.Email);
        Require(x.Phone is not null && Regex.IsMatch(x.Phone, @"^\+?[\d ()-]{6,30}$"), "Téléphone invalide.");
        Text(x.Country, "Pays", 80);
    }
    public static readonly string[] ExportSources = ["Date", "ValueDate", "Journal", "Account", "Description", "MovementDescription", "DescriptionWithBalances", "Debit", "Credit", "Reference", "Amount", "Direction", "Analytic", "Balance", "BankCode", "Currency", "StatementNumber", "RecordType", "Sequence", "OpeningBalance", "ClosingBalance"];
    public static readonly string[] DateFormats = ["ddMMyy", "ddMMyyyy", "yyMMdd", "yyyyMMdd", "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "MM/dd/yyyy"];
    public static void Export(ExportInput x)
    {
        Text(x.Name, "Nom");
        Require(x.Type is "SAGE100" or "SAGEX3" or "CUSTOMCSV", "Type de modèle invalide.");
        Require(x.FileExtension == (x.Type == "CUSTOMCSV" ? "csv" : "txt"), "Extension incompatible avec le type.");
        Require(x.Delimiter is " " or ";" or "," or "\t" or "|", "Séparateur invalide.");
        Require(x.Encoding is "utf-8" or "windows-1252" or "ascii", "Encodage invalide.");
        Require(x.NumberCulture is "fr-FR" or "en-US", "Culture supportée : fr-FR ou en-US.");
        Require(x.Fields is { Count: > 0 and <= 30 }, "1 à 30 champs requis.");
        Require(x.Fields!.Select(f => f.Position).Distinct().Count() == x.Fields.Count, "Positions dupliquées.");
        foreach (var f in x.Fields)
        {
            Text(f.FieldName, "Libellé du champ", 100);
            Require(ExportSources.Contains(f.SourceField) && f.Position >= 0, "Champ source ou position invalide.");
            if (f.SourceField is "Date" or "ValueDate")
                Require(string.IsNullOrEmpty(f.Format) || DateFormats.Contains(f.Format), "Format de date invalide.");
            else
                Require(f.Format is null or "" or "0.00" or "0.000" or "0.##", "Format numérique invalide.");
        }
    }
}
