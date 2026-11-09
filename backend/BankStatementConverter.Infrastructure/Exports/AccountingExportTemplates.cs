using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Exports;

public static class AccountingExportTemplates
{
    public static ExportTemplate Create(string type)
    {
        if (type is not ("SAGE100ENTRIES" or "SAGEX3ENTRIES"))
            throw new ArgumentException("Profil comptable inconnu.", nameof(type));
        var x3 = type == "SAGEX3ENTRIES";
        var template = new ExportTemplate
        {
            Name = x3 ? "Écritures Sage X3 paramétrables" : "Écritures Sage 100 paramétrables",
            Type = type,
            Description = x3 ? "Mouvements bancaires X3 : adapter au modèle du dossier et compléter les contreparties." : "Pièces équilibrées issues de la préparation comptable Sage 100. Adapter les colonnes au modèle d'import du dossier.",
            FileExtension = "txt",
            Delimiter = ";",
            Encoding = "windows-1252",
            NumberCulture = x3 ? "en-US" : "fr-FR",
            IncludeHeader = false,
            IsActive = true
        };
        void Add(string label, string source, string? format = null, bool required = true) => template.Fields.Add(new()
        {
            ExportTemplateId = template.Id,
            Position = template.Fields.Count,
            FieldName = label,
            SourceField = source,
            Format = format,
            Required = required
        });
        if (x3)
        {
            Add("Site", "Constant", "", false);
            Add("Type de pièce", "Constant", "", false);
        }
        else
        {
            Add("Jour", "Day");
            Add("N° pièce", "PieceNumber");
            Add("N° facture", "InvoiceNumber", required: false);
            Add("Référence", "Reference", required: false);
            Add("N° compte général", "Account");
            Add("N° compte tiers", "ThirdParty", required: false);
            Add("Libellé écriture", "Description");
            Add("Date échéance", "DueDate", "dd/MM/yyyy", false);
            Add("Pointage", "Pointage", required: false);
            Add("Débit", "Debit", "0.00");
            Add("Crédit", "Credit", "0.00");
            template.Description = "Journal bancaire : 11 colonnes de saisie Sage 100, du jour au crédit. Export d’un seul journal et d’une seule période mensuelle ; adapter le modèle d’import au dossier Sage.";
            return template;
        }
        Add("Journal comptable", "BankJournal");
        Add("Date comptable", "Date", "yyyyMMdd");
        Add("N° de pièce", "Reference");
        Add("Compte comptable", "BankAccount");
        Add("Libellé", "Description");
        Add("Débit comptable", "AccountingDebit", "0.00");
        Add("Crédit comptable", "AccountingCredit", "0.00");
        Add("Devise", "Currency");
        Add("Section analytique", "Analytic", required: false);
        return template;
    }

    // Upgrade only exact, uncustomized historical defaults. Exports retain their stored snapshots.
    public static bool IsLegacySage100Default(ExportTemplate template)
    {
        if (template.Type != "SAGE100ENTRIES" || template.Name != "Écritures Sage 100 paramétrables"
            || template.FileExtension != "txt" || template.Delimiter != ";" || template.Encoding != "windows-1252"
            || template.NumberCulture != "fr-FR" || template.IncludeHeader || !template.IsActive) return false;
        var descriptions = new[] {
            "Mouvements comptables du compte bancaire, sans lignes de solde. Adaptez les colonnes au modèle d'import du dossier Sage et complétez les contreparties avant intégration.",
            "Pièces équilibrées issues de la préparation comptable Sage 100. Adapter les colonnes au modèle d'import du dossier."
        };
        if (!descriptions.Contains(template.Description, StringComparer.Ordinal)) return false;
        var fields = template.Fields.OrderBy(f => f.Position).ToArray();
        foreach (var length in new[] { 8, 13, 14 })
        foreach (var pieceSource in new[] { "Reference", "PieceNumber" })
        {
            var expected = new List<(string Label, string Source, string? Format, bool Required)> {
                ("Journal comptable", "BankJournal", null, true), ("Date comptable", "Date", "dd/MM/yyyy", true),
                ("N° de pièce", pieceSource, null, true), ("Compte comptable", "BankAccount", null, true)
            };
            if (length > 8) expected.Add(("Compte tiers", "ThirdParty", null, false));
            expected.AddRange([("Libellé", "Description", null, true), ("Débit comptable", "AccountingDebit", "0.00", true),
                ("Crédit comptable", "AccountingCredit", "0.00", true), ("Devise", "Currency", null, true)]);
            if (length == 14) expected.Add(("Section analytique", "Analytic", null, false));
            if (length > 8) expected.AddRange([("Référence bancaire", "Reference", null, false), ("N° de facture", "InvoiceNumber", null, false),
                ("Échéance", "DueDate", "dd/MM/yyyy", false), ("Mode de règlement", "PaymentMethod", null, false)]);
            if (fields.Length == expected.Count && fields.Select((field, index) => field.Position == index
                && field.FieldName == expected[index].Label && field.SourceField == expected[index].Source
                && field.Format == expected[index].Format && field.Required == expected[index].Required).All(match => match)) return true;
        }
        return false;
    }
}
