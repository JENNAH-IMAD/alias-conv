using System.Text.Json.Serialization;
using BankStatementConverter.Domain;
namespace BankStatementConverter.Application;

/// <summary>Client-specific classification models and their optimistic concurrency version.</summary>
public sealed record EntryModelDossier(Guid ClientId, int Version, IReadOnlyList<AccountingEntryModel> Models);
/// <summary>Replaces the client's models without modifying the other accounting references.</summary>
public sealed record EntryModelSave(int Version, IReadOnlyList<AccountingEntryModel> Models);
public sealed record EntryOutputTemplateDto(Guid Id, string Name, string Description, bool Active, int Version, EntryModelOutput Output);
public sealed record EntryOutputTemplateSave(string Name, string Description, bool Active, int Version, EntryModelOutput Output);
/// <summary>Read-only classification request; null decisions explicitly exclude an ambiguous movement.</summary>
public sealed record EntryModelRun(Guid ClientId, Guid StatementId, Guid ExportTemplateId, string Abbreviation,
    Guid? ExtractId = null, IReadOnlyDictionary<Guid, Guid?>? Decisions = null, string? PreviewFingerprint = null, bool Confirmed = false, Guid? ModelId = null,
    IReadOnlyList<string>? KeywordOverrides = null, Guid? OutputTemplateId = null);
/// <summary>One matching model and the original configured phrases found in the movement label.</summary>
public sealed record EntryModelCandidate(Guid Id, string Name, string AccountCode, decimal TaxRate, IReadOnlyList<string> Keywords);
/// <summary>Auditable classification of an original movement with decimal amounts serialized as strings.</summary>
public sealed record EntryModelRow(Guid TransactionId, int Sequence, DateOnly? Date, string Description, string Status,
    IReadOnlyList<EntryModelCandidate> Candidates, Guid? ModelId,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal? Total,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal? Net,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal? Tax, string Message);
/// <summary>Classification and proposed result; last preview is remembered without posting or exporting entries.</summary>
public sealed record EntryModelPreview(Guid ClientId, Guid StatementId, Guid? ExtractId, string Abbreviation,
    int SourceVersion, int SettingsVersion, string Fingerprint, IReadOnlyList<EntryModelRow> Rows,
    IReadOnlyList<AccountingLine> Lines, IReadOnlyList<AccountingIssue> Errors, bool CanExport,
    EntryModelOutput? Output = null, IReadOnlyList<EntryResultRow>? ResultRows = null);
public sealed record EntryResultRow(Guid TransactionId, int Sequence, DateOnly? Date, string AccountCode, string Title,
    string Description, string Bank, string Abbreviation, string Currency, string Reference,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal Gross,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal Net,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal Tax,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal Rate,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal DebitHt = 0,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal CreditHt = 0);
public sealed record EntryArchiveDetail(Guid Id, Guid ClientId, Guid StatementId, string ClientName, string BankName,
    string SourceFileName, string ModelName, string FileName, int SourceVersion, int DossierVersion, DateTime CreatedAt,
    string GeneratedByName, string ContentSha256, AccountingEntryModel Model, EntryModelOutput Output,
    IReadOnlyList<EntryResultRow> Rows, string Text, bool Truncated);
