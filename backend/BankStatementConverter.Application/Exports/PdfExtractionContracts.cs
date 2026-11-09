using System.Text.Json;
namespace BankStatementConverter.Application.Exports;

// Coordinates are PDF points; origin is top-left. Scores describe OCR, never financial certainty.
public sealed record PdfTextBox(string Text, double Left, double Right, double Top, double Bottom, double? Confidence = 1,
    PdfStructuredTable? Table = null);
public sealed record PdfTableBounds(double Left, double Right, double Top, double Bottom);
public sealed record PdfStructuredTable(IReadOnlyList<IReadOnlyList<string>> Rows, PdfTableBounds? Bounds = null);
public sealed record PdfTextPage(int Number, double Width, double Height, IReadOnlyList<PdfTextBox> Boxes,
    string? Markdown = null, IReadOnlyList<PdfStructuredTable>? Tables = null, JsonElement? RawResult = null,
    string? ReadingMethod = null, bool RequiresImageReading = false, bool HasUnreliableText = false,
    string? Model = null, PdfReadingComparison? Comparison = null, PdfReadingEvidence? AlternateReading = null);
public sealed record PdfFieldDifference(int? Row, string Field, string? Primary, string? Secondary, string Kind);
public sealed record PdfReadingComparison(int Page, string PrimaryMethod, string SecondaryMethod,
    string? PrimaryModel, string? SecondaryModel, string AcceptedMethod, IReadOnlyList<PdfFieldDifference> Differences);
public sealed record PdfReadingEvidence(string Method, string? Model, IReadOnlyList<PdfTextBox> Boxes,
    IReadOnlyList<PdfStructuredTable>? Tables, string? Markdown);
public sealed record PdfTextDocument(string Method, IReadOnlyList<PdfTextPage> Pages, PdfColumnMapping? LastMapping = null,
    IReadOnlyList<PdfTextPage>? NativePages = null, int? SourcePageCount = null, ExtractionPerformance? Performance = null,
    bool ReadingsReviewed = false, IReadOnlyList<ExportError>? Warnings = null, string? PipelineVersion = null);
public sealed record ExtractionPerformance(long DurationMs, int NativePages, int PaddlePages);
public sealed record ExtractionSummary(string Method, int PageCount, int LowConfidenceCount, bool NeedsMapping,
    ExtractionPerformance? Performance = null, IReadOnlyList<PdfReadingComparison>? Comparisons = null,
    bool ReadingsReviewed = false, IReadOnlyList<PdfReadingSource>? Sources = null);
public sealed record PdfReadingSource(int Page, string Method, string? Model);
// Column starts and first data row, as percentages of each page. Optional for automatic adaptation.
public sealed record PdfColumnMapping(double Date = 8, double? ValueDate = 20, double Description = 32,
    double Debit = 75, double Credit = 88, double HeaderTop = 30, PdfTableColumnMapping? TableColumns = null);
// Structured tables have real cells. Indices are zero-based; StartRow skips a header when necessary.
public sealed record PdfTableColumnMapping(int Date = 0, int? ValueDate = 1, int Description = 2,
    int Debit = 3, int Credit = 4, int StartRow = 1, int? Reference = null);
public sealed record AdaptExtractionRequest(int StatementVersion, PdfColumnMapping? Mapping = null, Guid? ProfileId = null);
public interface IPdfOcrClient
{
    Task<PdfTextDocument> ExtractAsync(byte[] content, CancellationToken cancellationToken);
    async Task<PdfTextDocument> ExtractPagesAsync(byte[] content, IReadOnlyList<int> pages, CancellationToken cancellationToken)
    {
        var document = await ExtractAsync(content, cancellationToken);
        var selected = pages.Select(number => document.Pages.SingleOrDefault(page => page.Number == number)
            ?? throw new AppException(502, "Une page demandée manque dans la réponse du moteur de lecture.")).ToArray();
        return document with
        {
            Pages = selected,
            SourcePageCount = document.SourcePageCount ?? document.Pages.Count
        };
    }
}
public interface IAutomaticPdfExtraction
{
    Task<PdfStatementExtraction> ExtractAsync(byte[] content, string bankName, string bankCode, string currency, CancellationToken cancellationToken);
}
