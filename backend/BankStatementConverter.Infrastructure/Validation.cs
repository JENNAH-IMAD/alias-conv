using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using BankStatementConverter.Application;
namespace BankStatementConverter.Infrastructure;

public static class Validation
{
    public static void Require(bool condition, string message) { if (!condition) throw new AppException(400, message); }
    public static void Text(string? value, string name, int max = 200) => Require(!string.IsNullOrWhiteSpace(value) && value.Length <= max, $"{name} obligatoire (maximum {max} caractères).");
    public static void Email(string? value) => Require(value is not null && value.Length <= 254 && new EmailAddressAttribute().IsValid(value), "Adresse e-mail invalide.");
    public static void Password(string? value) => Require(value is not null && value.Length is >= 12 and <= 128 && value.Any(char.IsUpper) && value.Any(char.IsLower) && value.Any(char.IsDigit), "Mot de passe : 12 à 128 caractères, majuscule, minuscule et chiffre requis.");
    public static void Client(ClientInput x)
    {
        Text(x.Name, "Nom"); Text(x.LegalName, "Raison sociale");
        Require(x.ICE is not null && Regex.IsMatch(x.ICE, @"^\d{15}$"), "ICE : 15 chiffres requis.");
        Text(x.IF, "IF", 30); Text(x.RC, "RC", 40); Email(x.Email);
        Require(x.Phone is not null && Regex.IsMatch(x.Phone, @"^\+?[\d ()-]{6,30}$"), "Téléphone invalide."); Text(x.Country, "Pays", 80);
    }
    public static void Culture(string value) => Require(value is "fr-FR" or "en-US", "Culture supportée : fr-FR ou en-US.");
    public static void Statement(StatementInput x)
    {
        Text(x.Name, "Nom"); Require(x.ParserKey is "multibank-auto" or "delimited", "Parser non installé.");
        Require(x.Delimiter is ";" or "|" or "\t", "Séparateur de lecture : point-virgule, barre verticale ou tabulation.");
        Require(x.DateFormat is "dd/MM/yyyy" or "yyyy-MM-dd" or "dd-MM-yyyy" or "MM/dd/yyyy", "Format de date non supporté."); Culture(x.NumberCulture);
        Require(x.SkipLines is >= 0 and <= 100, "Nombre de lignes ignorées invalide.");
        int?[] columns = [x.DateColumn, x.DescriptionColumn, x.DebitColumn, x.CreditColumn, x.BalanceColumn, x.ReferenceColumn];
        Require(columns.All(c => c is null or >= 0 and <= 50) && columns.Where(c => c != null).Distinct().Count() == columns.Count(c => c != null), "Indices de colonnes distincts, entre 0 et 50.");
    }
    public static readonly string[] Sources = ["Date", "ValueDate", "Journal", "Account", "Description", "Debit", "Credit", "Reference", "Amount", "Direction", "Analytic", "Balance"];
    public static void Export(ExportInput x)
    {
        Text(x.Name, "Nom"); Require(x.Type is "SAGE100" or "SAGEX3" or "CUSTOMCSV", "Type d'export invalide.");
        Require(x.FileExtension == (x.Type == "SAGE100" ? "txt" : "csv"), "Extension incompatible avec le type.");
        Require(x.Delimiter is " " or ";" or "," or "\t" or "|", "Séparateur invalide.");
        Require(x.Encoding is "utf-8" or "windows-1252", "Encodage invalide."); Culture(x.NumberCulture);
        Require(x.Fields is { Count: > 0 and <= 30 }, "1 à 30 champs requis.");
        Require(x.Fields!.Select(f => f.Position).Distinct().Count() == x.Fields.Count, "Positions dupliquées.");
        foreach (var f in x.Fields)
        {
            Text(f.FieldName, "Libellé du champ", 100); Require(Sources.Contains(f.SourceField) && f.Position >= 0, "Champ source ou position invalide.");
            if (f.SourceField is "Date" or "ValueDate") Require(f.Format is null or "" or "dd/MM/yyyy" or "yyyy-MM-dd" or "ddMMyyyy" or "yyyyMMdd", "Format date d'export invalide.");
            else Require(f.Format is null or "" or "0.00" or "0.000" or "0.##", "Format numérique invalide.");
        }
    }
}
