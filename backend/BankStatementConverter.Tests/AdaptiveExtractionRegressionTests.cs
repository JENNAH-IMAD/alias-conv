using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Infrastructure.Exports;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace BankStatementConverter.Tests;

public class AdaptiveExtractionRegressionTests
{
    private static PdfTextDocument Native()
    {
        var boxes = new List<PdfTextBox>();
        void Text(string text, double x, double y) => boxes.Add(new(text, x, x + text.Length * 3, y, y + 8));
        Text("DU 01 09 2026 AU 30 09 2026", 30, 20);
        Text("Date", 30, 80);
        Text("Libellé", 90, 80);
        Text("Valeur", 380, 80);
        Text("Débit", 440, 80);
        Text("Crédit", 530, 80);
        Text("01", 30, 120);
        Text("09", 42, 120);
        Text("2026", 54, 120);
        Text("RECU CLIENT", 75, 120);
        Text("31", 380, 120);
        Text("08", 392, 120);
        Text("2026", 406, 120);
        Text("22460,90", 443, 120);
        return new("NATIVE_PDF", [new(1, 600, 800, boxes)]);
    }

    [Fact]
    public void AdjacentLabelsAndValueDateYearsNeverPolluteOperationDatesOrAmounts()
    {
        var s = new StatementPdfExtractor().Normalize(Native(), "TEST", "TEST", "MAD", default).Statement;
        var t = Assert.Single(s.Transactions);
        Assert.Equal(new DateOnly(2026, 9, 1), t.Date);
        Assert.Equal(new DateOnly(2026, 8, 31), t.ValueDate);
        Assert.Equal("RECU CLIENT", t.Description);
        Assert.Equal(22460.90m, t.Debit);
    }

    [Fact]
    public void NativeDateYearOnFollowingLineUsesPrintedYearInsteadOfTwoDigitPrefix()
    {
        var raw = Native();
        var boxes = raw.Pages[0].Boxes.Where(b => b.Left >= 70 || b.Top < 110).ToList();
        boxes.Add(new("01/09/20", 30, 66, 120, 128));
        boxes.Add(new("26", 30, 39, 128, 136));
        var s = new StatementPdfExtractor().Normalize(raw with
        {
            Pages = [raw.Pages[0] with { Boxes = boxes }]
        }, "TEST", "TEST", "MAD", default).Statement;
        Assert.Equal(new DateOnly(2026, 9, 1), Assert.Single(s.Transactions).Date);
    }

    [Theory]
    [InlineData("2026 22460,90", null)]
    [InlineData("2026 230,00", null)]
    [InlineData("1 234,56", "1234.56")]
    [InlineData("1 843 556,55", "1843556.55")]
    [InlineData("202622460,90", "202622460.90")]
    public void OnlyValidThousandsGroupsCanBeJoinedAndLargeAmountsAreNeverTrimmed(string amount, string? expected)
    {
        var rows = new IReadOnlyList<string>[] {
            new[] {"Date", "Valeur", "Libellé", "Débit", "Crédit"},
            new[] {"01/09/2026", "31/08/2026", "TEST", amount, ""}};
        var raw = new PdfTextDocument("PADDLE_OCR", [new(1, 600, 800, [], Tables: [new(rows)])]);
        var result = new StatementPdfExtractor().Normalize(raw, "TEST", "TEST", "MAD", default);
        var t = Assert.Single(result.Statement.Transactions);
        if (expected == null)
        {
            Assert.Null(t.Amount);
            Assert.Contains(result.Warnings, w => w.Code == "PDF_AMOUNT_UNREADABLE");
        }
        else
            Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), t.Amount);
    }

    [Fact]
    public void IncompleteStructuredCellsAutomaticallyRecoverFromOriginalNativeColumns()
    {
        var native = Native();
        var rows = new IReadOnlyList<string>[] {
            new[] {"Date", "Libellé", "Valeur", "Débit", "Crédit"},
            new[] {"", "RECU CLIENT", "31/08/2026", "2026 22460,90", ""}};
        var raw = native with
        {
            Method = "PADDLE_OCR",
            NativePages = native.Pages,
            Pages = [native.Pages[0] with { Tables = [new(rows)] }]
        };
        var result = new StatementPdfExtractor().Normalize(raw, "TEST", "TEST", "MAD", default);
        Assert.Equal(new DateOnly(2026, 9, 1), Assert.Single(result.Statement.Transactions).Date);
        Assert.Equal(22460.90m, result.Statement.Transactions[0].Debit);
        Assert.Contains(result.Warnings, w => w.Code == "PDF_NATIVE_RECOVERY");
        Assert.Same(raw, result.RawDocument);
    }

    [Fact]
    public void ScanWithoutOperationDateCopiesValueDateButDoesNotInventMoney()
    {
        var rows = new IReadOnlyList<string>[] {
            new[] {"Date", "Libellé", "Valeur", "Débit", "Crédit"},
            new[] {"", "RECU CLIENT", "31/08/2026", "2026 22460,90", ""}};
        var result = new StatementPdfExtractor().Normalize(new("PADDLE_OCR", [new(1, 600, 800, [], Tables: [new(rows)])]), "TEST", "TEST", "MAD", default);
        var t = Assert.Single(result.Statement.Transactions);
        Assert.Equal(t.ValueDate, t.Date);
        Assert.Null(t.Amount);
        Assert.Equal(new DateOnly(2026, 8, 31), t.ValueDate);
        Assert.Contains(result.Warnings, w => w.Code == "PDF_DATES_COMPLETED");
        Assert.False(result.Statement.IsValidated);
    }

    [Fact]
    public void StructuredShortDatesUsePrintedPeriodAcrossSeparateHeaderBoxes()
    {
        var rows = new IReadOnlyList<string>[] {
            new[] {"Date", "Libellé", "Valeur", "Débit", "Crédit"},
            new[] {"01 09", "RECU CLIENT", "31/08/2026", "22460,90", ""}};
        var boxes = new PdfTextBox[] { new("DU", 30, 40, 20, 30), new("01 09 2026", 45, 90, 20, 30), new("AU", 95, 105, 20, 30), new("30 09 2026", 110, 155, 20, 30) };
        var result = new StatementPdfExtractor().Normalize(new("PADDLE_OCR", [new(1, 600, 800, boxes, Tables: [new(rows)])]), "TEST", "TEST", "MAD", default);
        Assert.Equal(new DateOnly(2026, 9, 1), Assert.Single(result.Statement.Transactions).Date);
        Assert.Equal(new DateOnly(2026, 8, 31), result.Statement.Transactions[0].ValueDate);
    }

    private static byte[] DamagedNativePdf()
    {
        var pdf = new PdfDocumentBuilder();
        var font = pdf.AddStandard14Font(Standard14Font.Helvetica);
        var page = pdf.AddPage(PageSize.A4);
        void Text(string value, double x, double top) => page.AddText(value, 8, new PdfPoint(x, 842 - top - 8), font);
        Text("DU 01/09/2026 AU 30/09/2026", 30, 20);
        Text("Date", 30, 80);
        Text("Libelle", 100, 80);
        Text("Valeur", 350, 80);
        Text("Debit", 440, 80);
        Text("Credit", 530, 80);
        Text("01/09/2026", 30, 120);
        Text("PAIEMENT CLIENT", 100, 120);
        Text("01/09/2026", 350, 120);
        Text("2026 22460,90", 440, 120);
        return pdf.Build();
    }

    [Fact]
    public async Task SuccessfulButIncompleteNativeReadingTriggersAutomaticSecondPass()
    {
        var content = DamagedNativePdf();
        var native = new StatementPdfExtractor();
        Assert.Null(Assert.Single(native.Extract(content, "TEST", "TEST", "MAD", default).Statement.Transactions).Amount);
        var ocr = new AutomaticPdfExtractionTests.OcrStub { Result = Native() with { Method = "PADDLE_OCR" } };
        var result = await new AutomaticPdfExtraction(native, ocr).ExtractAsync(content, "TEST", "TEST", "MAD", default);
        Assert.Equal(1, ocr.Calls);
        Assert.Equal(22460.90m, Assert.Single(result.Statement.Transactions).Amount);
        Assert.Contains(result.Warnings, w => w.Code == "EXTRACTION_AUTOMATIC_RECHECK");
        Assert.False(result.Statement.IsValidated);
    }

    [Fact]
    public async Task SecondPassCannotChangeMovementCountWithoutIndependentBalanceReconciliation()
    {
        var raw = Native();
        var boxes = raw.Pages[0].Boxes.Concat(raw.Pages[0].Boxes.Where(b => b.Top == 120).Select(b => b with { Top = b.Top + 30, Bottom = b.Bottom + 30 })).ToArray();
        var ocr = new AutomaticPdfExtractionTests.OcrStub { Result = raw with { Method = "PADDLE_OCR", Pages = [raw.Pages[0] with { Boxes = boxes }] } };
        var result = await new AutomaticPdfExtraction(new(), ocr).ExtractAsync(DamagedNativePdf(), "TEST", "TEST", "MAD", default);
        Assert.Equal(1, ocr.Calls);
        Assert.Null(Assert.Single(result.Statement.Transactions).Amount);
        Assert.Contains(result.Warnings, w => w.Code == "PDF_AMOUNT_UNREADABLE");
    }

    private sealed class UnavailableOcr : IPdfOcrClient
    {
        public Task<PdfTextDocument> ExtractAsync(byte[] content, CancellationToken ct) => throw new AppException(503, "Unavailable");
    }

    [Fact]
    public async Task AutomaticSecondPassFailureKeepsUncertainReadingAndExplicitWarning()
    {
        var result = await new AutomaticPdfExtraction(new(), new UnavailableOcr()).ExtractAsync(DamagedNativePdf(), "TEST", "TEST", "MAD", default);
        Assert.Null(Assert.Single(result.Statement.Transactions).Amount);
        Assert.Contains(result.Warnings, w => w.Code == "EXTRACTION_RECHECK_UNAVAILABLE");
        Assert.False(result.Statement.IsValidated);
    }
}
