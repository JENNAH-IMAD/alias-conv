using BankStatementConverter.Domain;
using System.Text.Json.Serialization;
namespace BankStatementConverter.Application.Exports;

public record ExportError(string Code, string Message, int? TransactionSequence = null, string? FieldName = null, string? SourceField = null);
public record ExportResult(string FileName, string MimeType, string FileExtension, byte[] Content, int RowCount,
    IReadOnlyList<ExportError> Warnings, IReadOnlyList<ExportError> Errors, bool DownloadAllowed = false, string? Encoding = null, bool TextEdited = false);
public record ExportTemplateSummary(Guid Id, string Name);
public record ExportPreview(ExportTemplateSummary Template, IReadOnlyList<string> Columns, IReadOnlyList<string[]> Rows,
    IReadOnlyList<int?> RowSequences, int TotalRows, bool IsValidated,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? OpeningBalance,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal? ClosingBalance,
    IReadOnlyList<ExportError> Warnings, IReadOnlyList<ExportError> Errors, int StatementVersion = 0, string TemplateFingerprint = "",
    IReadOnlyList<string>? RowKinds = null, IReadOnlyList<int[]>? RowColumnIndices = null, string? Text = null, bool TextTruncated = false, Guid? ExtractId = null);
public record StatementVersionRequest(int Version);
public sealed record SaveStatementExtractRequest(int StatementVersion, string? FilterText, string? ExcludeText = null, BankStatement? Statement = null);
public record PrepareStatementExportRequest(int StatementVersion, Guid TemplateId, Guid? ExtractId = null);
public record PreparedStatementExport(StatementDetail Detail, ExportPreview Preview);
public record ExportRequest(int StatementVersion, string? TemplateFingerprint = null, bool AllowErrors = false, string? EditedText = null, Guid? ExtractId = null);
public record ExportGenerationOptions(bool AllowErrors = false, string? EditedText = null);
public record ResolvedExportSource(bool IsKnown, object? Value);

public interface IExportSourceResolver
{
    ResolvedExportSource Resolve(string source, BankStatement statement, StatementTransaction transaction);
}
public interface IExportWriter
{
    string Write(IReadOnlyList<string> columns, IReadOnlyList<string[]> rows, string delimiter, bool includeHeader);
}
public interface IExportEngine
{
    Task<ExportResult> GenerateAsync(BankStatement statement, ExportTemplate template, CancellationToken cancellationToken, ExportGenerationOptions? options = null);
    Task<ExportPreview> PreviewAsync(BankStatement statement, ExportTemplate template, CancellationToken cancellationToken);
}
public record StatementSummary(Guid Id, string BankName, string? StatementNumber, string? Currency, DateOnly? PeriodStart,
    DateOnly? PeriodEnd, string Status, int Version, int TransactionCount, DateTime CreatedAt);
public record StatementDetail(Guid Id, string Status, int Version, DateTime? ValidatedAt, BankStatement Statement,
    IReadOnlyList<ExportError> Warnings, IReadOnlyList<ExportError> Errors, string? SourceFileName = null, ExtractionSummary? Extraction = null,
    Guid? ClientId = null, Guid? BankAccountId = null, string? ImportedByName = null, string? ModifiedByName = null);
public record StatementAssociation(int Version, Guid ClientId, Guid BankAccountId);
public record PdfStatementExtraction(BankStatement Statement, IReadOnlyList<ExportError> Warnings, PdfTextDocument? RawDocument = null);
public interface IStatementPdfExtractor
{
    PdfStatementExtraction Extract(byte[] content, string bankName, string bankCode, string currency, CancellationToken cancellationToken);
}
public record StatementValidationResult(bool IsValid, IReadOnlyList<ExportError> Warnings, IReadOnlyList<ExportError> Errors);
public record ExportJobDto(Guid Id, Guid TemplateId, string TemplateName, int StatementVersion, string FileName, int RowCount,
    string Extension, string Encoding, DateTime CreatedAt, string Status, string ContentSha256, bool HasFile = false, string? GeneratedByName = null);
