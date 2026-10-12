using BankStatementConverter.Application.Exports;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;

namespace BankStatementConverter.Tests;

// Fictional amounts and holders. Exercises observed layout traits, not downloaded Scribd PDFs.
public class MoroccanStatementVariantsTests
{
    private static PdfTextDocument Document(bool structured, string[][] rows, string totals = "")
    {
        var boxes = new List<PdfTextBox>();
        void Text(string text, double x, double y) => boxes.Add(new(text, x, x + text.Length * 3, y, y + 8));
        Text("SPECIMEN FICTIF", 30, 20);
        var headers = new[] { "Date", "Valeur", "Libellé", "Débit", "Crédit" };
        var positions = new[] { 30d, 110d, 210d, 450d, 530d };
        for (var c = 0; c < headers.Length; c++) Text(headers[c], positions[c], 80);
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c] != "") Text(rows[r][c], positions[c], 110 + r * 30);
        if (totals != "") Text(totals, 30, 110 + rows.Length * 30);
        var tables = structured ? PaddleDocumentParser.Tables("<table>" +
            string.Join("", new[] { headers }.Concat(rows).Select(row => "<tr>" +
                string.Join("", row.Select(cell => "<td>" + System.Net.WebUtility.HtmlEncode(cell) + "</td>")) + "</tr>")) + "</table>") : null;
        return new(structured ? "PADDLE_OCR" : "NATIVE_PDF", [new(1, 600, 800, boxes, Tables: tables)]);
    }

    private static BankStatement Metadata(DateOnly? start = null, DateOnly? end = null,
        decimal opening = 100m, decimal closing = 150m) => new()
    {
        BankName = "Banque de test", BankCode = "TEST", Currency = "MAD",
        PeriodStart = start ?? new DateOnly(2025, 7, 1), PeriodEnd = end ?? new DateOnly(2025, 7, 31),
        OpeningBalance = opening, ClosingBalance = closing
    };

    private static PdfStatementExtraction Extract(bool structured, string[][] rows, BankStatement? metadata = null, string totals = "") =>
        new StatementPdfExtractor().Normalize(Document(structured, rows, totals), "Banque de test", "TEST", "MAD", default,
            metadata: metadata ?? Metadata());

    [Theory] [InlineData(false)] [InlineData(true)]
    public void DescendingDatesNegativeDebitsAndDuplicateMovementsRemainDistinct(bool structured)
    {
        var result = Extract(structured, [
            ["30 07 2025", "29 07 2025", "VIREMENT FICTIF", "", "70,00"],
            ["05 07 2025", "04 07 2025", "FRAIS FICTIFS", "-10,00", ""],
            ["05 07 2025", "04 07 2025", "FRAIS FICTIFS", "-10,00", ""]]);
        var s = result.Statement;
        Assert.Equal(3, s.Transactions.Count);
        Assert.Equal(new DateOnly(2025, 7, 30), s.Transactions[0].Date);
        Assert.Equal(new DateOnly(2025, 7, 5), s.Transactions[1].Date);
        Assert.Equal(10m, s.Transactions[1].Debit);
        Assert.Equal(10m, s.Transactions[2].Amount);
        Assert.Equal(20m, s.TotalDebit); Assert.Equal(70m, s.TotalCredit);
        Assert.Empty(new StatementValidator().Validate(s).Errors);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void OperationAndValueYearsAreResolvedIndependentlyAcrossDecemberJanuary(bool structured)
    {
        var s = Extract(structured, [
            ["10/01", "31/12", "ACHAT FICTIF", "50,00", ""],
            ["30/12", "02/01", "VIREMENT FICTIF", "", "100,00"]],
            Metadata(new(2025, 12, 1), new(2026, 1, 31))).Statement;
        Assert.Equal(new DateOnly(2026, 1, 10), s.Transactions[0].Date);
        Assert.Equal(new DateOnly(2025, 12, 31), s.Transactions[0].ValueDate);
        Assert.Equal(new DateOnly(2025, 12, 30), s.Transactions[1].Date);
        Assert.Equal(new DateOnly(2026, 1, 2), s.Transactions[1].ValueDate);
        Assert.Empty(new StatementValidator().Validate(s).Errors);
    }

    [Theory]
    [InlineData(false, "1,234.56")] [InlineData(true, "1,234.56")]
    [InlineData(false, "1.234,56")] [InlineData(true, "1.234,56")]
    [InlineData(false, "1 234,56")] [InlineData(true, "1 234,56")]
    public void NumericConventionsPreserveExactAmounts(bool structured, string amount)
    {
        var s = Extract(structured, [["02/07/25", "02/07/25", "DEPOT FICTIF", "", amount]],
            Metadata(opening: 0m, closing: 1234.56m)).Statement;
        Assert.Equal(1234.56m, s.Transactions.Single().Credit);
        Assert.Equal(1234.56m, s.TotalCredit);
        Assert.Empty(new StatementValidator().Validate(s).Errors);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void PrintedTotalsIncludingOpeningAreNotCountedTwice(bool structured)
    {
        var result = Extract(structured, [
            ["02/07/25", "02/07/25", "ACHAT FICTIF", "50,00", ""],
            ["03/07/25", "03/07/25", "DEPOT FICTIF", "", "100,00"]], totals: "TOTAL 50,00 200,00");
        Assert.Equal(50m, result.Statement.TotalDebit); Assert.Equal(100m, result.Statement.TotalCredit);
        Assert.Contains(result.Warnings, w => w.Code == "PDF_TOTALS_INCLUDE_OPENING_BALANCE");
        Assert.Empty(new StatementValidator().Validate(result.Statement).Errors);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void ShortDatesPreferTheStatementPeriodAndValueDatesFollowTheOperation(bool structured)
    {
        var s = Extract(structured, [
            ["10/01", "09/01", "DEPOT FICTIF", "", "70,00"],
            ["30/12", "02/01", "ACHAT FICTIF", "20,00", ""]],
            Metadata(new(2025, 7, 1), new(2026, 1, 31))).Statement;
        Assert.Equal(new DateOnly(2026, 1, 10), s.Transactions[0].Date);
        Assert.Equal(new DateOnly(2026, 1, 9), s.Transactions[0].ValueDate);
        Assert.Equal(new DateOnly(2026, 1, 2), s.Transactions[1].ValueDate);
        Assert.Empty(new StatementValidator().Validate(s).Errors);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void InvalidDatesAndMalformedAmountsAreNotGuessed(bool structured)
    {
        var result = Extract(structured, [["31/09/25", "31/09/25", "DEPOT FICTIF", "", "50,00"]]);
        Assert.Null(result.Statement.Transactions.Single().Date);
        Assert.Contains(result.Warnings, w => w.Code == "PDF_DATE_UNREADABLE");
        Assert.Contains(new StatementValidator().Validate(result.Statement).Warnings, w => w.Code == "DATE_MISSING");
        if (structured)
        {
            Assert.Equal(422, Assert.Throws<AppException>(() => Extract(true,
                [["02/07/25", "02/07/25", "DEPOT FICTIF", "", "1,23,4.56"]])).Status);
            return;
        }
        result = Extract(false, [["02/07/25", "02/07/25", "DEPOT FICTIF", "", "1,23,4.56"]]);
        Assert.Null(result.Statement.Transactions.Single().Credit);
        Assert.Contains(result.Warnings, w => w.Code == "PDF_AMOUNT_UNREADABLE");
        Assert.NotEmpty(new StatementValidator().Validate(result.Statement).Errors);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void AmericanPrintedTotalsHaveTheSameMagnitudeAsTheirMovements(bool structured)
    {
        var result = Extract(structured, [["02/07/25", "02/07/25", "DEPOT FICTIF", "", "1,234.56"]],
            Metadata(opening: 0, closing: 1234.56m), "TOTAL 0.00 1,234.56");
        Assert.Equal(1234.56m, result.Statement.TotalCredit);
        Assert.DoesNotContain(result.Warnings, w => w.Code == "PDF_TOTALS_COMPUTED");
        Assert.Empty(new StatementValidator().Validate(result.Statement).Errors);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void ContradictoryPrintedTotalsRemainValidationErrors(bool structured)
    {
        var result = Extract(structured, [["02/07/25", "02/07/25", "DEPOT FICTIF", "", "50,00"]],
            totals: "TOTAL 0,00 51,00");
        Assert.Contains(new StatementValidator().Validate(result.Statement).Errors, e => e.SourceField == "TotalCredit");
    }
}
