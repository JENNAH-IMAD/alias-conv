using System.Text.Json.Serialization;
namespace BankStatementConverter.Domain;

public sealed record AccountingEntryModel(Guid Id, string Name, string Abbreviation, IReadOnlyList<string> Keywords,
    Guid BankAccountId, string AccountCode, Guid ExportTemplateId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal TaxRate = 0,
    string TaxAccountCode = "", bool Active = true, bool BankAccountBased = false, EntryModelOutput? Output = null, Guid? OutputTemplateId = null);
public sealed record EntryModelColumn(string Field, string Label, bool Display = true, bool Export = true);
public sealed record EntryModelOutput
{
    public string FileExtension { get; init; } = "txt";
    public string Delimiter { get; init; } = ";";
    public string Encoding { get; init; } = "utf-8";
    public string NumberCulture { get; init; } = "fr-FR";
    public string DateFormat { get; init; } = "dd/MM/yyyy";
    public bool IncludeHeader { get; init; } = true;
    public IReadOnlyList<EntryModelColumn> Columns { get; init; } = [new("ACCOUNT", "N° compte"), new("TITLE", "Intitulé"), new("DESCRIPTION", "Libellé"), new("BANK", "Banque"), new("DATE", "Date"), new("DEBIT_HT", "Débit HT"), new("CREDIT_HT", "Crédit HT")];
}
public sealed class EntryOutputTemplate : Entity
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string OutputJson { get; set; } = "{}";
    public bool Active { get; set; } = true;
    public int Version { get; set; } = 1;
}
// No foreign keys to source documents or legacy formats: archives survive their removal.
public sealed class AccountingEntryArchive : Entity
{
    public Guid ClientId { get; set; }
    public Guid StatementId { get; set; }
    public Guid ModelId { get; set; }
    public string ClientName { get; set; } = "";
    public string BankName { get; set; } = "";
    public string SourceFileName { get; set; } = "";
    public string ModelName { get; set; } = "";
    public int SourceVersion { get; set; }
    public int DossierVersion { get; set; }
    public int RowCount { get; set; }
    public string FileName { get; set; } = "";
    public string Encoding { get; set; } = "utf-8";
    public string Delimiter { get; set; } = ";";
    public string StorageKey { get; set; } = "";
    public string ContentSha256 { get; set; } = "";
    public string PreviewFingerprint { get; set; } = "";
    public string SnapshotJson { get; set; } = "{}";
    public string GeneratedByName { get; set; } = "";
}
public sealed record AccountingLine(Guid TransactionId, int Sequence, DateOnly? Date, string Journal, string PieceNumber,
    string AccountCode, string? ThirdPartyCode, string Description, string Reference, string InvoiceNumber,
    DateOnly? DueDate, string PaymentMethod, string Analytic, [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Debit, [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Credit, string Currency, bool IsBank,
    string AnalyticPlanCode = "", string Pointage = "");
