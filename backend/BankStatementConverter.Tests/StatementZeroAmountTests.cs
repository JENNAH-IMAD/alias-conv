using System.Text;
using System.Text.Json;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;

namespace BankStatementConverter.Tests;

public class StatementZeroAmountTests
{
    [Theory]
    [InlineData("125.50", "0", "CREDIT", "DEBIT")]
    [InlineData("0", "125.50", "DEBIT", "CREDIT")]
    [InlineData("125.50", null, null, "DEBIT")]
    [InlineData(null, "125.50", null, "CREDIT")]
    public void ZeroAndNullSidesDoNotDisturbDirection(string? debit, string? credit, string? direction, string expected)
    {
        var row = new StatementTransaction { Debit = debit == null ? null : decimal.Parse(debit, System.Globalization.CultureInfo.InvariantCulture), Credit = credit == null ? null : decimal.Parse(credit, System.Globalization.CultureInfo.InvariantCulture), Amount = 0, Direction = direction };
        var normalized = StatementAmounts.Normalize(row);
        Assert.Equal(expected, normalized.Direction);
        Assert.Equal(125.50m, normalized.Amount);
        Assert.Null(expected == "DEBIT" ? normalized.Credit : normalized.Debit);
        var validation = new StatementValidator().Validate(new BankStatement { Currency = "MAD", Transactions = [row] });
        Assert.DoesNotContain(validation.Errors, e => e.Code is "AMBIGUOUS_AMOUNT" or "DIRECTION_AMOUNT_MISMATCH" or "AMOUNT_MISMATCH");
    }

    [Fact]
    public void BothEmptyAmountsHaveNoDirectionButTwoRealAmountsRemainAmbiguous()
    {
        var blank = StatementAmounts.Normalize(new StatementTransaction { Debit = 0, Credit = 0, Amount = 0, Direction = "DEBIT" });
        Assert.Null(blank.Debit);
        Assert.Null(blank.Credit);
        Assert.Null(blank.Amount);
        Assert.Null(blank.Direction);
        Assert.Null(StatementAmounts.Normalize(new StatementTransaction { Direction = "CREDIT" }).Direction);
        var result = new StatementValidator().Validate(new BankStatement { Currency = "MAD", Transactions = [new() { Debit = 10, Credit = 20 }] });
        Assert.Contains(result.Errors, e => e.Code == "AMBIGUOUS_AMOUNT");
    }

    [Fact]
    public async Task ZeroCreditExportsAnEmptyCellAndCorrectDirectionWithoutMutatingInput()
    {
        var s = ExportFixtures.Statement with
        {
            Transactions = [ExportFixtures.Statement.Transactions[1] with { Credit = 0, Direction = "CREDIT" }],
            OpeningBalance = 0,
            ClosingBalance = -125.50m,
            TotalCredit = 0
        };
        var preview = await ExportFixtures.Engine.PreviewAsync(s, ExportFixtures.Csv(), default);
        Assert.Equal("", Assert.Single(preview.Rows)[3]);
        var sage = await ExportFixtures.Engine.PreviewAsync(s, ExportFixtures.X3(false), default);
        Assert.Equal("1", sage.Rows[1][4]);
        Assert.Equal("0.00", sage.Rows[0][2]);
        Assert.Equal(0, s.Transactions[0].Credit);
        Assert.Equal("CREDIT", s.Transactions[0].Direction);
        var json = JsonSerializer.Serialize(StatementAmounts.Normalize(s), StatementImportService.Json);
        Assert.Contains("\"credit\":null", json);
        var csv = await ExportFixtures.Engine.GenerateAsync(s with
        {
            IsValidated = true
        }, ExportFixtures.Csv(), default);
        Assert.DoesNotContain(";0,00;", Encoding.UTF8.GetString(csv.Content));
    }
}
