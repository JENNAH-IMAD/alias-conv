using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;
namespace BankStatementConverter.Tests;

public class NormalizedStatementPdfTests
{
    [Fact]
    public void MultipagePdfRepeatsHeadersAndHasOnlyTwoStatementBalances()
    {
        var statement = new BankStatement
        {
            BankName = "TEST",
            Currency = "MAD",
            AccountNumber = "000123456789",
            PeriodStart = new(2026, 1, 1),
            PeriodEnd = new(2026, 1, 31),
            OpeningBalance = 1000,
            ClosingBalance = 1000,
            Transactions = Enumerable.Range(1, 65).Select(i => new StatementTransaction { Sequence = i, Date = new(2026, 1, 1), Description = "TEST " + new string('A', 120), Debit = 10 }).ToList()
        };
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(NormalizedStatementPdf.Generate(statement, false));
        Assert.True(pdf.NumberOfPages > 1);
        var pages = pdf.GetPages().Select(p => p.Text).ToList();
        var text = string.Join("\n", pages);
        Assert.All(pages, p => Assert.Contains("Date opération", p));
        Assert.Equal(1, text.Split("Solde de départ du relevé").Length - 1);
        Assert.Equal(1, text.Split("Solde final du relevé").Length - 1);
        Assert.Contains("000123456789", text);
        Assert.DoesNotContain("Référence", text);
    }
    [Fact]
    public void NegativeBalancesAreDisplayedAsCreditWithoutChangingSignedData()
    {
        var statement = new BankStatement { BankName = "TEST", Currency = "MAD", OpeningBalance = -1000, ClosingBalance = -1125.50m, Transactions = [new() { Sequence = 1, Date = new(2026, 1, 1), Description = "TEST", Debit = 125.50m }] };
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(NormalizedStatementPdf.Generate(statement, false));
        var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));
        Assert.Contains(" · Crédit", text);
        Assert.DoesNotContain("-1", text);
        Assert.Equal(-1000, statement.OpeningBalance);
        Assert.Equal(-1125.50m, statement.ClosingBalance);
    }
}
