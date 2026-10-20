using System.Text.Json;
namespace BankStatementConverter.Application.Exports;

// Coordinates are PDF points; origin is top-left. Scores describe OCR, never financial certainty.
public sealed record PdfTextBox(string Text, double Left, double Right, double Top, double Bottom, double? Confidence = 1,
    PdfStructuredTable? Table = null);
public sealed record PdfStructuredTable(IReadOnlyList<IReadOnlyList<string>> Rows);
public sealed record PdfTextPage(int Number, double Width, double Height, IReadOnlyList<PdfTextBox> Boxes,
    string? Markdown = null, IReadOnlyList<PdfStructuredTable>? Tables = null, JsonElement? RawResult = null,
    string? ReadingMethod = null, bool RequiresImageReading = false, bool HasUnreliableText = false);
public sealed record PdfTextDocument(string Method, IReadOnlyList<PdfTextPage> Pages, PdfColumnMapping? LastMapping = null,
    IReadOnlyList<PdfTextPage>? NativePages = null, int? SourcePageCount = null, ExtractionPerformance? Performance = null);
public sealed record ExtractionPerformance(long DurationMs, int NativePages, int PaddlePages, int AiPages);
public sealed record ExtractionSummary(string Method, int PageCount, int LowConfidenceCount, bool NeedsMapping,
    ExtractionPerformance? Performance = null);
// Column starts and first data row, as percentages of each page. Optional for automatic adaptation.
public sealed record PdfColumnMapping(double Date = 8, double? ValueDate = 20, double Description = 32,
    double Debit = 75, double Credit = 88, double HeaderTop = 30, PdfTableColumnMapping? TableColumns = null);
// Structured tables have real cells. Indices are zero-based; StartRow skips a header when necessary.
public sealed record PdfTableColumnMapping(int Date = 0, int? ValueDate = 1, int Description = 2,
    int Debit = 3, int Credit = 4, int StartRow = 1, int? Reference = null);
public sealed record AdaptExtractionRequest(int StatementVersion, PdfColumnMapping? Mapping = null, Guid? ProfileId = null);
public interface IPdfOcrClient
{
    // An explicitly selected transcription provider must also read text-native PDFs.
    bool RequiresProviderReading => false;
    Task<PdfTextDocument> ExtractAsync(byte[] content, CancellationToken cancellationToken);
    Task<PdfTextDocument> ExtractPagesWithContextAsync(byte[] content, IReadOnlyList<int> pages, PdfTextDocument context, CancellationToken cancellationToken)
        => ExtractPagesAsync(content, pages, cancellationToken);
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
