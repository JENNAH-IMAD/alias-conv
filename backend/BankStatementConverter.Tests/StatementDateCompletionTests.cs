using BankStatementConverter.Application.Exports;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;

namespace BankStatementConverter.Tests;

public class StatementDateCompletionTests
{
    private static readonly DateOnly Day = new(2026, 10, 6);
    private static PdfStatementExtraction Structured(string[] header, params string[][] rows)
        => StructuredStatementNormalizer.Normalize(new("PADDLE_OCR", [new(1,650,800,[],
            Tables:[new(new[] { header }.Concat(rows).ToArray())])]), "TEST", "TEST", "MAD", default);

    [Theory]
    [InlineData("Date")]
    [InlineData("Date opération")]
    [InlineData("Date valeur")]
    public void StructuredSingleDatePopulatesBothFields(string caption)
    {
        var result = Structured([caption, "Libellé", "Débit", "Crédit"], ["06/10/2026", "PAIEMENT", "10,00", ""]);
        var row = Assert.Single(result.Statement.Transactions);
        Assert.Equal(Day, row.Date);
        Assert.Equal(Day, row.ValueDate);
        Assert.Contains(result.Warnings, w => w.Code == "PDF_DATES_COMPLETED");
    }

    [Theory]
    [InlineData("", "06/10/2026")]
    [InlineData("06/10/2026", "")]
    [InlineData("Non déterminée", "06/10/2026")]
    [InlineData("06/10/2026", "Non déterminée")]
    [InlineData("-", "06/10/2026")]
    public void StructuredMissingDateUsesOtherDateOnSameRow(string operation, string value)
    {
        var result = Structured(["Date opération", "Date valeur", "Libellé", "Débit", "Crédit"],
            [operation, value, "PAIEMENT", "10,00", ""]);
        var row = Assert.Single(result.Statement.Transactions);
        Assert.Equal(Day, row.Date);
        Assert.Equal(Day, row.ValueDate);
        Assert.DoesNotContain(result.Warnings, w => w.Code == "PDF_DATE_UNREADABLE");
    }

    [Fact]
    public void DistinctDatesAndUnreadableCellsArePreservedWithoutBorrowingFromAnotherRow()
    {
        var result = Structured(["Date opération", "Date valeur", "Libellé", "Débit", "Crédit"],
            ["06/10/2026", "07/10/2026", "DATES DISTINCTES", "10,00", ""],
            ["06/10/2026", "31/02/2026", "DATE INVALIDE", "10,00", ""],
            ["", "", "SANS DATE", "10,00", ""],
            ["31/02/2026", "06/10/2026", "OPERATION INVALIDE", "10,00", ""]);
        var rows = result.Statement.Transactions;
        Assert.Equal(4, rows.Count);
        Assert.Equal(Day, rows[0].Date);
        Assert.Equal(Day.AddDays(1), rows[0].ValueDate);
        Assert.Null(rows[1].ValueDate);
        Assert.Null(rows[2].Date);
        Assert.Null(rows[2].ValueDate);
        Assert.Null(rows[3].Date);
        Assert.Equal(Day, rows[3].ValueDate);
        Assert.DoesNotContain(result.Warnings, w => w.Code == "PDF_DATES_COMPLETED");
        Assert.Contains(result.Warnings, w => w.Code == "PDF_DATE_UNREADABLE");
    }

    private static PdfStatementExtraction Native(string caption, string? operation, string? value = null, bool twoColumns = false, bool unknownBalance = false)
    {
        var boxes = new List<PdfTextBox>();
        void Text(string text, double x, double y, double width = 80) => boxes.Add(new(text, x, x + width, y, y + 10));
        Text(caption, 30, 50);
        if (twoColumns)
            Text("Date valeur", 130, 50);
        Text("Libellé", 240, 50);
        Text("Débit", 430, 50);
        Text("Crédit", 530, 50);
        if (operation != null)
            Text(operation, 30, 100);
        if (value != null)
            Text(value, 130, 100);
        Text("PAIEMENT TEST", 240, 100, 100);
        Text("10,00", 435, 100, 35);
        if (unknownBalance)
        {
            Text("Solde final du relevé : Non déterminé", 30, 160, 400);
            Text("Total des débits calculés : 10,00 MAD", 30, 180, 400);
        }
        return new StatementPdfExtractor().Normalize(new("NATIVE_PDF", [new(1, 650, 800, boxes)]), "TEST", "TEST", "MAD", default);
    }

    [Theory]
    [InlineData("Date")]
    [InlineData("Date opération")]
    [InlineData("Date valeur")]
    public void NativeSingleDatePopulatesBothFields(string caption)
    {
        var row = Assert.Single(Native(caption, "06/10/2026").Statement.Transactions);
        Assert.Equal(Day, row.Date);
        Assert.Equal(Day, row.ValueDate);
        Assert.Equal("PAIEMENT TEST", row.Description);
    }

    [Theory]
    [InlineData(null, "06/10/2026")]
    [InlineData("06/10/2026", null)]
    [InlineData("Non déterminée", "06/10/2026")]
    [InlineData("06/10/2026", "Non déterminée")]
    public void NativeKeepsRowsWhoseOperationOrValueDateIsMissing(string? operation, string? value)
    {
        var result = Native("Date opération", operation, value, true);
        var row = Assert.Single(result.Statement.Transactions);
        Assert.Equal(Day, row.Date);
        Assert.Equal(Day, row.ValueDate);
        Assert.Equal("PAIEMENT TEST", row.Description);
        Assert.Equal(10m, row.Debit);
        Assert.Contains(result.Warnings, w => w.Code == "PDF_DATES_COMPLETED");
    }

    [Theory]
    [InlineData("06/10/2026", "07/10/2026")]
    [InlineData("06/10/2026", "31/02/2026")]
    [InlineData("31/02/2026", "06/10/2026")]
    [InlineData("06/10/2026", "illisible")]
    public void NativePreservesDifferentDatesAndDoesNotOverwriteMalformedDates(string operation, string value)
    {
        var result = Native("Date opération", operation, value, true);
        var row = Assert.Single(result.Statement.Transactions);
        Assert.Equal(operation == "31/02/2026" ? null : Day, row.Date);
        Assert.Equal(value == "06/10/2026" ? Day : value == "07/10/2026" ? Day.AddDays(1) : null, row.ValueDate);
        Assert.DoesNotContain(result.Warnings, w => w.Code == "PDF_DATES_COMPLETED");
        if (operation == "31/02/2026" || value != "07/10/2026")
            Assert.Contains(result.Warnings, w => w.Code == "PDF_DATE_UNREADABLE");
    }

    [Fact]
    public void UnavailableBalanceDoesNotUseFollowingTotalAndCompletedDatesReachCsv()
    {
        var result = Native("Date opération", "06/10/2026", "Non déterminée", true, true);
        Assert.Null(result.Statement.OpeningBalance);
        Assert.Null(result.Statement.ClosingBalance);
        var csv = System.Text.Encoding.UTF8.GetString(StandardStatementCsv.Generate(result.Statement));
        Assert.Contains("2026-10-06;2026-10-06;PAIEMENT TEST", csv);
    }
}
