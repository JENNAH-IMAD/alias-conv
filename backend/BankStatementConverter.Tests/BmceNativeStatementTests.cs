using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;

namespace BankStatementConverter.Tests;

public class BmceNativeStatementTests
{
    private static PdfTextBox Box(string text, double x, double y) => new(text, x, x + text.Length * 4, y - 5, y);
    private static PdfTextDocument Document()
    {
        PdfTextPage Page(int number, params PdfTextBox[] boxes) => new(number, 594, 842, boxes);
        return new("NATIVE_PDF", [
            Page(1, Box("1/2 01 09 2026 30 09 2026", 100, 80),
                Box("31", 50, 120), Box("08", 63, 120), Box("SOLDE", 84, 120), Box("100,00", 400, 120),
                Box("01", 50, 140), Box("09", 63, 140), Box("FRAIS", 84, 140), Box("01", 328, 140), Box("09", 340, 140), Box("25,00", 400, 140),
                Box("02", 50, 160), Box("09", 63, 160), Box("VIREMENT", 84, 160), Box("02", 328, 160), Box("09", 340, 160), Box("200,00", 500, 160),
                Box("CLIENT REFERENCE 123", 84, 168), Box("25,00", 400, 200), Box("200,00", 500, 200)),
            Page(2, Box("2/2 01 09 2026 30 09 2026", 100, 80),
                Box("REPORT", 84, 120), Box("25,00", 400, 120), Box("200,00", 500, 120),
                Box("03", 50, 140), Box("09", 63, 140), Box("FRAIS", 84, 140), Box("02", 328, 140), Box("09", 340, 140), Box("10,00", 400, 140),
                Box("35,00", 400, 200), Box("200,00", 500, 200), Box("65,00", 500, 220))]);
    }
    private static PdfStatementExtraction Normalize(PdfTextDocument d) => new StatementPdfExtractor().Normalize(d, "BANK OF AFRICA", "BMCE", "MAD", default);
    [Fact]
    public void OutlinedHeadersKeepDatesDirectionsContinuationsAndDebitOpeningBalance()
    {
        var s = Normalize(Document()).Statement;
        Assert.Equal(3, s.Transactions.Count);
        Assert.Equal(-100m, s.OpeningBalance);
        Assert.Equal(65m, s.ClosingBalance);
        Assert.Equal(35m, s.TotalDebit);
        Assert.Equal(200m, s.TotalCredit);
        Assert.Equal("VIREMENT\nCLIENT REFERENCE 123", s.Transactions[1].Description);
        Assert.Equal(new DateOnly(2026, 9, 2), s.Transactions[2].ValueDate);
        Assert.Equal(new DateOnly(2026, 8, 31), s.OpeningBalanceDate);
        Assert.Empty(new StatementValidator().Validate(s).Errors);
    }
    [Theory]
    [InlineData(1, 120, "200,00", "201,00")]
    [InlineData(0, 200, "25,00", "26,00")]
    [InlineData(1, 220, "65,00", "66,00")]
    [InlineData(1, 140, "10,00", "")]
    public void RejectsIncorrectReportsTotalsBalancesAndMissingAmounts(int page, double bottom, string before, string after)
    {
        var d = Document();
        var pages = d.Pages.ToArray();
        pages[page] = pages[page] with
        {
            Boxes = pages[page].Boxes.Select(b => b.Bottom == bottom && b.Text == before ? b with { Text = after } : b).ToArray()
        };
        Assert.Equal(422, Assert.Throws<AppException>(() => Normalize(d with { Pages = pages })).Status);
    }
    [Fact]
    public void RecognizesBmceNameWhenWorkspaceUsesACustomBankCode()
    {
        var result = new StatementPdfExtractor().Normalize(Document(), "BMCE Bank", "BOA", "MAD", default);
        Assert.Equal(3, result.Statement.Transactions.Count);
        Assert.Equal(65m, result.Statement.ClosingBalance);
    }
    [Fact]
    public void RejectsOmittedPages()
    {
        var d = Document();
        Assert.Throws<AppException>(() => Normalize(d with { Pages = [d.Pages[0]] }));
    }
}
