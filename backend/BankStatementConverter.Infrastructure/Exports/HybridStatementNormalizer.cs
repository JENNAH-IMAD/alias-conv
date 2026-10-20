using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Exports;

public static class HybridStatementNormalizer
{
    internal static PdfStatementExtraction? ReadPage(StatementPdfExtractor native, PdfTextPage page, int totalPages,
        BankStatement context, CancellationToken ct)
    {
        try
        {
            var method = page.ReadingMethod ?? "NATIVE_PDF";
            return native.Normalize(new(method, [page], SourcePageCount: totalPages), context.BankName,
                context.BankCode ?? "", context.Currency ?? "", ct, metadata: context with
                {
                    Transactions = [],
                    OpeningBalance = null,
                    ClosingBalance = null,
                    TotalDebit = null,
                    TotalCredit = null,
                    BalanceConvention = null
                });
        }
        catch (AppException error) when (CanReview(error)) { return null; }
    }

    internal static bool CanReview(AppException error) => error.Status == 422
        && !error.Message.Contains("devise", StringComparison.OrdinalIgnoreCase)
        && !error.Message.Contains("pages sont désordonnées", StringComparison.OrdinalIgnoreCase);

    public static PdfStatementExtraction Normalize(StatementPdfExtractor native, PdfTextDocument document,
        string bankName, string bankCode, string currency, CancellationToken ct, BankStatement? manual = null)
    {
        var empty = new BankStatement { BankName = bankName, BankCode = bankCode, Currency = currency };
        var context = PdfStatementMetadata.Complete(new(empty, [], document), manual).Statement;
        // Keep the existing whole-document parsers for continuation rows and repeated table headers.
        if (document.Pages.All(page => page.ReadingMethod != "OPENROUTER"))
        {
            try
            {
                var result = native.Normalize(document with
                {
                    Method = "PADDLE_OCR"
                }, bankName, bankCode, currency, ct, metadata: manual);
                return result with
                {
                    RawDocument = document
                };
            }
            catch (AppException error) when (CanReview(error)) { }
        }
        if (document.Pages.All(page => page.ReadingMethod == "OPENROUTER"))
        {
            try
            {
                var result = native.Normalize(document with
                {
                    Method = "OPENROUTER"
                }, bankName, bankCode, currency, ct, metadata: manual);
                return result with
                {
                    RawDocument = document
                };
            }
            catch (AppException error) when (CanReview(error)) { }
        }
        var warnings = new List<ExportError>();
        var transactions = new List<StatementTransaction>();
        var readings = new List<PdfStatementExtraction>();
        foreach (var page in document.Pages.OrderBy(page => page.Number))
        {
            ct.ThrowIfCancellationRequested();
            var reading = ReadPage(native, page, document.SourcePageCount ?? document.Pages.Count, context, ct);
            if (reading == null)
            {
                if (ExtractionAudit.ExpectedRows(page) > 0 || page.Boxes.Count < 10)
                    warnings.Add(new("EXTRACTION_PAGE_REVIEW", $"Les mouvements de la page {page.Number} ne sont pas reconnus. Consultez le texte source et le PDF original."));
                continue;
            }
            readings.Add(reading);
            warnings.AddRange(reading.Warnings.Where(warning => warning.Code != "PDF_TOTALS_COMPUTED"));
            transactions.AddRange(reading.Statement.Transactions.Select(transaction => transaction with
            {
                Id = Guid.NewGuid(),
                Sequence = transactions.Count + transaction.Sequence,
                SourcePages = [page.Number]
            }));
        }
        var merged = context with
        {
            Transactions = transactions.Select((transaction, index) => transaction with { Sequence = index + 1 }).ToArray(),
            OpeningBalance = context.OpeningBalance ?? readings.FirstOrDefault()?.Statement.OpeningBalance,
            ClosingBalance = context.ClosingBalance ?? readings.LastOrDefault()?.Statement.ClosingBalance,
            IsValidated = false
        };
        return PdfStatementMetadata.Complete(new(merged, warnings.DistinctBy(warning => (warning.Code, warning.Message)).ToArray(), document), manual);
    }
}
