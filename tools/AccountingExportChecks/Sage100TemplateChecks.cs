using System.Text;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;

internal static class Sage100TemplateChecks
{
    public static void Run()
    {
        static void Check(bool valid, string message) { if (!valid) throw new Exception(message); }
        var legacy = new ExportTemplate { Name = "Écritures Sage 100 paramétrables", Type = "SAGE100ENTRIES", FileExtension = "txt", Delimiter = ";", Encoding = "windows-1252", NumberCulture = "fr-FR", IncludeHeader = false, IsActive = true,
            Description = "Mouvements comptables du compte bancaire, sans lignes de solde. Adaptez les colonnes au modèle d'import du dossier Sage et complétez les contreparties avant intégration." };
        var old = new (string Label, string Source, string? Format)[] { ("Journal comptable", "BankJournal", null), ("Date comptable", "Date", "dd/MM/yyyy"), ("N° de pièce", "PieceNumber", null), ("Compte comptable", "BankAccount", null), ("Libellé", "Description", null), ("Débit comptable", "AccountingDebit", "0.00"), ("Crédit comptable", "AccountingCredit", "0.00"), ("Devise", "Currency", null) };
        foreach (var (label, source, format) in old) legacy.Fields.Add(new() { FieldName = label, SourceField = source, Format = format, Required = true, Position = legacy.Fields.Count });
        Check(AccountingExportTemplates.IsLegacySage100Default(legacy), "Historical standard profile was not recognized.");
        legacy.Fields.First().FieldName = "Journal personnalisé";
        Check(!AccountingExportTemplates.IsLegacySage100Default(legacy), "Customized profile would be overwritten.");
        legacy.Fields.First().FieldName = "Journal comptable";
        legacy.Encoding = "utf-8";
        Check(!AccountingExportTemplates.IsLegacySage100Default(legacy), "Customized encoding would be overwritten.");
        Check(!AccountingExportTemplates.IsLegacySage100Default(AccountingExportTemplates.Create("SAGE100ENTRIES")), "Upgraded default is not idempotent.");
        var current = AccountingExportTemplates.Create("SAGE100ENTRIES");
        Check(current.Fields.OrderBy(field => field.Position).Select(field => field.SourceField).SequenceEqual(
            new[] { "Day", "PieceNumber", "InvoiceNumber", "Reference", "Account", "ThirdParty", "Description", "DueDate", "Pointage", "Debit", "Credit" }),
            "Sage 100 default export columns changed.");
        Console.WriteLine("PASS Sage100_DefaultColumnsAndSafeLegacyRecognition");
    }
}
