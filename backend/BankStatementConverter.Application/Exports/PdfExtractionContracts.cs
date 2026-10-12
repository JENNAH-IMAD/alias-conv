using System.Text.Json;
namespace BankStatementConverter.Application.Exports;

// Coordinates are PDF points; origin is top-left. Scores describe OCR, never financial certainty.
public sealed record PdfTextBox(string Text, double Left, double Right, double Top, double Bottom, double? Confidence = 1);
public sealed record PdfStructuredTable(IReadOnlyList<IReadOnlyList<string>> Rows);
public sealed record PdfTextPage(int Number, double Width, double Height, IReadOnlyList<PdfTextBox> Boxes,
    string? Markdown = null, IReadOnlyList<PdfStructuredTable>? Tables = null, JsonElement? RawResult = null);
public sealed record PdfTextDocument(string Method, IReadOnlyList<PdfTextPage> Pages, PdfColumnMapping? LastMapping = null,
    IReadOnlyList<PdfTextPage>? NativePages = null);
public sealed record ExtractionSummary(string Method, int PageCount, int LowConfidenceCount, bool NeedsMapping);
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
}
public interface IAutomaticPdfExtraction
{
    Task<PdfStatementExtraction> ExtractAsync(byte[] content, string bankName, string bankCode, string currency, CancellationToken cancellationToken);
}
