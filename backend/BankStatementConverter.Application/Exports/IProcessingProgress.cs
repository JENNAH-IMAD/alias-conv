namespace BankStatementConverter.Application.Exports;

// A request-scoped reporter; percentages describe completed pipeline stages, not an OCR confidence score.
public interface IProcessingProgress
{
    void Report(string stage, int percent, int? extractedPages = null, int? totalPages = null);
}
