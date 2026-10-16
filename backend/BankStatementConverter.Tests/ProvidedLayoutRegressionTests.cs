using BankStatementConverter.Application.Exports;
using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BankStatementConverter.Tests;

// Anonymized layout cases observed in the supplied statements. Customer PDFs stay outside Git.
public class ProvidedLayoutRegressionTests
{
    private static PdfTextDocument Monthly(bool corruptTotal = false)
    {
        var boxes = new List<PdfTextBox>();
        void Text(string value, double x, double y) => boxes.Add(new(value, x, x + value.Length * 4, y, y + 8));
        Text("RELEVE MENSUEL DE COMPTE", 30, 20);
        Text("N° de compte 0123456789012345", 30, 40);
        Text("Date opération", 15, 80);
        Text("Libellé", 150, 80);
        Text("Débit", 400, 80);
        Text("Crédit", 500, 80);
        Text("Date valeur", 580, 80);
        Text("01/09/2026", 15, 105);
        Text("SOLDE REPORT", 90, 105);
        Text("1,000.00", 420, 105);
        Text("01/09/2026", 15, 130);
        Text("PAIEMENT FOURNISSEUR", 90, 130);
        Text("250.50", 420, 130);
        Text("31/08/2026", 580, 130);
        Text("02/09/2026", 15, 160);
        Text("VIREMENT CLIENT", 90, 160);
        Text("2,000.00", 520, 160);
        Text("03/09/2026", 580, 160);
        Text("TOTAL PERIODE", 90, 200);
        Text(corruptTotal ? "1,251.50" : "1,250.50", 420, 200);
        Text("2,000.00", 520, 200);
        Text("SOLDE NET", 90, 230);
        Text("749.50", 520, 230);
        return new("NATIVE_PDF", [new(1, 680, 800, boxes)]);
    }

    [Fact]
    public void TrailingValueDateKeepsMoneyLabelsDebitBalancesAndMonthlyMetadata()
    {
        var result = new StatementPdfExtractor().Normalize(Monthly(), "Test", "TEST", "MAD", default);
        var s = result.Statement;
        Assert.Equal(2, s.Transactions.Count);
        Assert.Equal("PAIEMENT FOURNISSEUR", s.Transactions[0].Description);
        Assert.Equal(new DateOnly(2026, 8, 31), s.Transactions[0].ValueDate);
        Assert.Equal(250.50m, s.Transactions[0].Debit);
        Assert.Null(s.Transactions[0].Credit);
        Assert.Equal(2000m, s.Transactions[1].Credit);
        Assert.Equal(-1000m, s.OpeningBalance);
        Assert.Equal(749.50m, s.ClosingBalance);
        Assert.Equal(250.50m, s.TotalDebit);
        Assert.Equal(2000m, s.TotalCredit);
        Assert.Equal("0123456789012345", s.AccountNumber);
        Assert.Equal(new DateOnly(2026, 9, 1), s.PeriodStart);
        Assert.Equal(new DateOnly(2026, 9, 30), s.PeriodEnd);
        Assert.Equal("CREDIT_INCREASES", s.BalanceConvention);
        Assert.Contains(result.Warnings, w => w.Code == "PDF_TOTALS_INCLUDE_OPENING_BALANCE");
        Assert.Empty(new StatementValidator().Validate(s).Errors);
    }

    [Fact]
    public void PrintedMismatchIsPreservedAndBlocksExport()
    {
        var result = new StatementPdfExtractor().Normalize(Monthly(true), "Test", "TEST", "MAD", default);
        Assert.Contains(new StatementValidator().Validate(result.Statement).Errors, e => e.Code == "TOTAL_DEBIT_MISMATCH");
    }

    [Theory]
    [InlineData("CSV")]
    [InlineData("SAGE100")]
    [InlineData("SAGEX3")]
    public async Task RecognizedFieldsFeedTheSelectedExportModel(string format)
    {
        var result = new StatementPdfExtractor().Normalize(Monthly(), "Test", "TEST", "MAD", default);
        var model = format switch
        {
            "CSV" => ExportFixtures.Csv(),
            "SAGE100" => ExportFixtures.Sage(true),
            _ => ExportFixtures.X3(false)
        };
        // RecordType is an explicit model/workspace setting, never inferred from the PDF.
        var statement = result.Statement with
        {
            RecordType = "1",
            IsValidated = true
        };
        var prepared = StatementExportPreparation.Complete(statement, model, result.Warnings, result.RawDocument, new(), default);
        var preview = await ExportFixtures.Engine.PreviewAsync(prepared.Statement, model, default);
        Assert.Empty(preview.Errors);
        Assert.Equal(format == "SAGEX3" ? 4 : 2, preview.Rows.Count);
    }

    [Fact]
    public async Task CloudConfigurationStillReadsSoundNativePdfWithoutSubmittingIt()
    {
        var ocr = new AutomaticPdfExtractionTests.OcrStub();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Ocr:Provider"] = "AI_STUDIO" }).Build();
        var bytes = File.ReadAllBytes(StatementPdfExtractorTests.Fixture("releve_credit_agricole_exemple_02.pdf"));
        var result = await new AutomaticPdfExtraction(new(), ocr, config).ExtractAsync(bytes, "Test", "CA", "MAD", default);
        Assert.Equal(0, ocr.Calls);
        Assert.Equal(22, result.Statement.Transactions.Count);
        Assert.False(result.Statement.IsValidated);
    }

    [Fact]
    public void PrintedPageCountPreventsAnApparentlyBalancedPartialImport()
    {
        var raw = Monthly();
        raw = raw with
        {
            Pages = [raw.Pages[0] with { Boxes = raw.Pages[0].Boxes.Append(new("Page 1 / 2", 450, 520, 760, 770)).ToArray() }]
        };
        Assert.Equal(422, Assert.Throws<AppException>(() => new StatementPdfExtractor().Normalize(raw, "Test", "TEST", "MAD", default)).Status);
    }

    [Fact]
    public void OcrCentersKeepTallAmountBoxesAndDashInDescriptionOutOfDateCell()
    {
        var raw = Monthly();
        var boxes = raw.Pages[0].Boxes.Select(b => b.Text == "250.50" ? b with { Top = b.Top - 4, Bottom = b.Bottom + 4 } : b).ToList();
        boxes.RemoveAll(b => b.Text == "PAIEMENT FOURNISSEUR");
        boxes.Add(new("VR - FOURNISSEUR", 60, 180, 129, 137));
        var result = new StatementPdfExtractor().Normalize(raw with
        {
            Method = "PADDLE_OCR",
            Pages = [raw.Pages[0] with { Boxes = boxes }]
        }, "Test", "TEST", "MAD", default);
        Assert.Equal(new DateOnly(2026, 9, 1), result.Statement.Transactions[0].Date);
        Assert.Equal(250.50m, result.Statement.Transactions[0].Debit);
        Assert.Equal("VR - FOURNISSEUR", result.Statement.Transactions[0].Description);
    }

    [Fact]
    public void SharedDateCaptionAndMultilineLabelsKeepEveryContinuationWithItsMovement()
    {
        var boxes = new List<PdfTextBox>();
        void Text(string value, double x, double y) => boxes.Add(new(value, x, x + value.Length * 4, y, y + 8));
        Text("RELEVE DE COMPTE DU MOIS DE 04/2024", 30, 20);
        Text("NUMERO DE COMPTE: 12345678 K", 30, 40);
        Text("Date", 50, 80);
        Text("Opération", 30, 100);
        Text("Valeur", 100, 100);
        Text("Libellé de l'opération", 250, 100);
        Text("Débit", 440, 100);
        Text("Crédit", 530, 100);
        Text("Solde au 31/03/2024", 230, 125);
        Text("-100,00", 540, 125);
        Text("01/04/24", 30, 150);
        Text("01/04/24", 100, 150);
        Text("PAIEMENT FACTURE", 180, 150);
        Text("25,00", 460, 150);
        Text("CLIENT LIGNE DEUX", 180, 164);
        Text("02/04/24", 30, 180);
        Text("02/04/24", 100, 180);
        Text("VIREMENT", 180, 180);
        Text("200,00", 540, 180);
        Text("REFERENCE CONSERVEE", 180, 194);
        Text("Nouveau Solde au 30/04/2024", 200, 240);
        Text("75,00", 540, 240);
        var result = new StatementPdfExtractor().Normalize(new("PADDLE_OCR", [new(1, 620, 800, boxes)]), "Test", "TEST", "MAD", default);
        Assert.Equal("PAIEMENT FACTURE\nCLIENT LIGNE DEUX", result.Statement.Transactions[0].Description);
        Assert.Equal("VIREMENT\nREFERENCE CONSERVEE", result.Statement.Transactions[1].Description);
        Assert.Equal("12345678 K", result.Statement.AccountNumber);
        Assert.Equal(new DateOnly(2024, 4, 1), result.Statement.PeriodStart);
        Assert.Empty(new StatementValidator().Validate(result.Statement).Errors);
    }
}
