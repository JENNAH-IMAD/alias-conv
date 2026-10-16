using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;
namespace BankStatementConverter.Tests;

public class AutomaticPdfExtractionTests
{
    internal sealed class OcrStub : IPdfOcrClient
    {
        public int Calls
        {
            get; private set;
        }
        public PdfTextDocument Result { get; set; } = new("PADDLE_OCR", [new(1, 595, 842, [])]);
        public Task<PdfTextDocument> ExtractAsync(byte[] bytes, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(Result);
        }
    }
    private static byte[] Blank()
    {
        var pdf = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
        pdf.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        return pdf.Build();
    }
    [Fact]
    public async Task NativeDocumentsDoNotRequireOcrAndKeepRawBoxes()
    {
        var ocr = new OcrStub();
        var extractor = new AutomaticPdfExtraction(new(), ocr);
        var result = await extractor.ExtractAsync(File.ReadAllBytes(StatementPdfExtractorTests.Fixture("releve_credit_agricole_exemple_02.pdf")), "Test", "CA", "MAD", default);
        Assert.Equal(0, ocr.Calls);
        Assert.Equal(22, result.Statement.Transactions.Count);
        Assert.Equal("NATIVE_PDF", result.RawDocument!.Method);
    }
    [Fact]
    public async Task ScanUsesOcrAndRawReadingPrecedesBankAdaptation()
    {
        var native = new StatementPdfExtractor();
        var document = native.ReadDocument(File.ReadAllBytes(StatementPdfExtractorTests.Fixture("releve_credit_agricole_exemple_02.pdf")), default);
        var ocr = new OcrStub { Result = document with { Method = "PADDLE_OCR", Pages = document.Pages.Select(p => p with { Boxes = p.Boxes.Select(b => b with { Confidence = 0.89 }).ToList() }).ToList() } };
        var result = await new AutomaticPdfExtraction(native, ocr).ExtractAsync(Blank(), "Test", "CA", "MAD", default);
        Assert.Equal(1, ocr.Calls);
        Assert.Equal(22, result.Statement.Transactions.Count);
        Assert.Equal(15245.75m, result.Statement.OpeningBalance);
        Assert.Equal(25966.06m, result.Statement.ClosingBalance);
        Assert.Contains(result.Warnings, w => w.Code == "OCR_REVIEW_REQUIRED");
        Assert.Same(ocr.Result, result.RawDocument);
        Assert.False(result.Statement.IsValidated);
    }
    [Fact]
    public async Task UnknownLayoutKeepsRawTextForMappingAndEmptyScanDoesNotCreateData()
    {
        var ocr = new OcrStub { Result = new("PADDLE_OCR", [new(1, 595, 842, [new("TABLEAU INHABITUEL", 30, 180, 30, 45, 0.95)])]) };
        var extractor = new AutomaticPdfExtraction(new(), ocr);
        var result = await extractor.ExtractAsync(Blank(), "Test", "CA", "MAD", default);
        Assert.Empty(result.Statement.Transactions);
        Assert.Contains(result.Warnings, w => w.Code == "EXTRACTION_MAPPING_REQUIRED");
        Assert.NotNull(result.RawDocument);
        ocr.Result = new("PADDLE_OCR", [new(1, 595, 842, [])]);
        Assert.Equal(422, (await Assert.ThrowsAsync<AppException>(() => extractor.ExtractAsync(Blank(), "Test", "CA", "MAD", default))).Status);
    }
    [Fact]
    public async Task CorruptionAndCurrencyMismatchAreNotRetriedOrMaskedByOcr()
    {
        var ocr = new OcrStub();
        var extractor = new AutomaticPdfExtraction(new(), ocr);
        Assert.Equal(422, (await Assert.ThrowsAsync<AppException>(() => extractor.ExtractAsync("%PDF-invalid"u8.ToArray(), "Test", "CA", "MAD", default))).Status);
        Assert.Equal(422, (await Assert.ThrowsAsync<AppException>(() => extractor.ExtractAsync(File.ReadAllBytes(StatementPdfExtractorTests.Fixture("releve_test_01_attijariwafa.pdf")), "Test", "ATT", "EUR", default))).Status);
        Assert.Equal(0, ocr.Calls);
    }
    [Fact]
    public void ExplicitMappingCanAdaptHeaderlessTextAndRejectInvalidPositions()
    {
        var native = new StatementPdfExtractor();
        var document = native.ReadDocument(File.ReadAllBytes(StatementPdfExtractorTests.Fixture("releve_credit_agricole_exemple_02.pdf")), default);
        // Remove the printed column captions; table geometry and actual values are unchanged.
        document = document with
        {
            Pages = document.Pages.Select(p => p with { Boxes = p.Boxes.Where(b => b.Top < 238 || b.Top > 250).ToList() }).ToList()
        };
        var result = native.Normalize(document, "Test", "CA", "MAD", default, new(Date: 8, ValueDate: 58, Description: 15.5, Debit: 72.5, Credit: 85.9, HeaderTop: 30));
        Assert.Equal(22, result.Statement.Transactions.Count);
        Assert.Empty(new StatementValidator().Validate(result.Statement with
        {
            BalanceConvention = "CREDIT_INCREASES"
        }).Errors);
        var expected = native.Extract(File.ReadAllBytes(StatementPdfExtractorTests.Fixture("releve_credit_agricole_exemple_02.pdf")), "Test", "CA", "MAD", default);
        Assert.Equal(expected.Statement.Transactions.Select(t => (t.Date, t.ValueDate, t.Description, t.Debit, t.Credit)), result.Statement.Transactions.Select(t => (t.Date, t.ValueDate, t.Description, t.Debit, t.Credit)));
        Assert.Equal(400, Assert.Throws<AppException>(() => native.Normalize(document, "Test", "CA", "MAD", default, new(Date: 95, Credit: 5))).Status);
    }
    [Fact]
    public void AdaptationUsesTheCorrectedPeriodWhenPrintedDatesHaveNoYear()
    {
        var normalizer = new StatementPdfExtractor();
        var document = normalizer.ReadDocument(File.ReadAllBytes(StatementPdfExtractorTests.Fixture("releve_credit_agricole_exemple_02.pdf")), default);
        var expected = normalizer.Normalize(document, "Test", "CA", "MAD", default);
        document = document with
        {
            Pages = document.Pages.Select(p => p with
            {
                Boxes = p.Boxes.Select(b => b with
                {
                    Text = System.Text.RegularExpressions.Regex.Replace(b.Text, @"(\d{2}/\d{2})/2026", "$1")
                }).ToList()
            }).ToList()
        };
        var metadata = ExportFixtures.Statement with
        {
            PeriodStart = new(2026, 3, 1),
            PeriodEnd = new(2026, 3, 31)
        };
        var result = normalizer.Normalize(document, "Test", "CA", "MAD", default, metadata: metadata);
        Assert.Equal(expected.Statement.Transactions.Select(t => (t.Date, t.ValueDate)), result.Statement.Transactions.Select(t => (t.Date, t.ValueDate)));
    }
}
