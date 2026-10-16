using System.Text;
using System.Text.Json;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;

namespace BankStatementConverter.Tests;

public class SageX3Tests
{
    [Fact]
    public async Task CustomBalanceDescriptionModesAndColumnOrderAreAppliedToExactFileAndPreview()
    {
        var template = ExportFixtures.X3(false);
        template.Fields = [template.Fields.Single(f => f.SourceField == "Description"), template.Fields.Single(f => f.SourceField == "Amount")];
        template.Fields.First().Position = 0;
        template.Fields.Last().Position = 1;
        var statement = ExportFixtures.Statement with
        {
            OpeningBalanceDescription = "SOLDE PDF"
        };
        var preview = await ExportFixtures.Engine.PreviewAsync(statement, template, default);
        Assert.Equal(new[] { 1 }, preview.RowColumnIndices![0]);
        Assert.Equal(new[] { 0, 1 }, preview.RowColumnIndices[1]);
        Assert.Equal(new[] { "1000.00" }, preview.Rows[0]);
        template.Fields.First().SourceField = "MovementDescription";
        preview = await ExportFixtures.Engine.PreviewAsync(statement, template, default);
        Assert.Equal(new[] { "", "1000.00" }, preview.Rows[0]);
        template.Fields.First().SourceField = "DescriptionWithBalances";
        preview = await ExportFixtures.Engine.PreviewAsync(statement, template, default);
        Assert.Equal(new[] { "SOLDE PDF", "1000.00" }, preview.Rows[0]);
        var file = await ExportFixtures.Engine.GenerateAsync(statement, template, default);
        Assert.Empty(file.Errors);
        Assert.Equal(new DelimitedTextExportWriter().Write(preview.Columns, preview.Rows, ";", false), Encoding.UTF8.GetString(file.Content));
    }
    [Fact]
    public async Task CreditAgricolePdfMatchesTheUserExampleExactlyIncludingBothBalances()
    {
        var extraction = new StatementPdfExtractor().Extract(File.ReadAllBytes(StatementPdfExtractorTests.Fixture("releve_credit_agricole_exemple_02.pdf")), "Crédit Agricole", "CA", "MAD", default);
        var statement = extraction.Statement with
        {
            IsValidated = true
        };
        var result = await ExportFixtures.Engine.GenerateAsync(statement, ExportFixtures.X3(false), default);
        Assert.Empty(result.Errors);
        Assert.Equal(24, result.RowCount);
        var expected = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "golden", "sagex3_credit_agricole_032026.txt"));
        Assert.Equal(Encoding.UTF8.GetString(expected), Encoding.UTF8.GetString(result.Content));
        Assert.Equal(expected, result.Content);
        var preview = await ExportFixtures.Engine.PreviewAsync(statement, ExportFixtures.X3(false), default);
        Assert.Equal(24, preview.TotalRows);
        Assert.Equal(24, preview.Rows.Count);
        Assert.Equal(Encoding.UTF8.GetString(result.Content), new DelimitedTextExportWriter().Write(preview.Columns, preview.Rows, ";", false));
    }
    [Theory]
    [InlineData(15, "-1")]
    [InlineData(0, "-1")]
    [InlineData(-15, "1")]
    public async Task BalanceSignUsesMagnitudeAndTheConfirmedConvention(int balance, string direction)
    {
        var statement = ExportFixtures.Statement with
        {
            OpeningBalance = balance,
            ClosingBalance = balance,
            BalanceConvention = null
        };
        var preview = await ExportFixtures.Engine.PreviewAsync(statement, ExportFixtures.X3(false), default);
        Assert.Empty(preview.Errors);
        foreach (var row in new[] { preview.Rows[0], preview.Rows[^1] })
        {
            Assert.Equal(direction, row[3]);
            Assert.Equal(Math.Abs(balance).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), row[2]);
        }
    }
    [Theory]
    [InlineData("AWB", "ATT")]
    [InlineData("ATT", "ATT")]
    [InlineData("CA", "CA")]
    [InlineData("CDM", "CDM")]
    [InlineData("ABB", "ABB")]
    [InlineData("BP", "BP")]
    [InlineData("BMCE", "BMCE")]
    [InlineData("BMCI", "BMCI")]
    [InlineData("CFG", "CFG")]
    [InlineData("CIH", "CIH")]
    [InlineData("SAHAM", "SAHAM")]
    public async Task BankCodesUseAttijariAliasAndKeepTheOtherCatalogCodes(string code, string exported)
    {
        var preview = await ExportFixtures.Engine.PreviewAsync(ExportFixtures.Statement with
        {
            BankCode = code
        }, ExportFixtures.X3(false), default);
        for (var i = 0; i < preview.Rows.Count; i++)
        {
            var row = preview.Rows[i];
            var balance = preview.RowKinds![i] != "TRANSACTION";
            Assert.Equal(exported, row[balance ? 4 : 5]);
            Assert.Equal($"RB{exported}092026", row[balance ? 6 : 7]);
        }
    }
    [Fact]
    public async Task ExplicitBalanceDatesAndLabelsAndMonthlyIdentifiersPreserveInputData()
    {
        var fixture = ExportFixtures.Statement;
        var statement = fixture with
        {
            RecordType = null,
            StatementNumber = null,
            OpeningBalanceDate = new(2026, 8, 31),
            OpeningBalanceValueDate = new(2026, 8, 30),
            OpeningBalanceDescription = "OUVERTURE PDF",
            ClosingBalanceDate = new(2026, 10, 1),
            ClosingBalanceDescription = "CLÔTURE PDF",
            Transactions = [fixture.Transactions[1] with { Sequence = 90 }, fixture.Transactions[0] with { Sequence = 7 }]
        };
        var template = ExportFixtures.X3(false);
        template.Fields.Single(f => f.SourceField == "Description").SourceField = "DescriptionWithBalances";
        var before = JsonSerializer.Serialize(statement);
        var beforeTemplate = JsonSerializer.Serialize(template);
        var preview = await ExportFixtures.Engine.PreviewAsync(statement, template, default);
        Assert.Empty(preview.Errors);
        Assert.Equal("20260831", preview.Rows[0][0]);
        Assert.Equal("20260830", preview.Rows[0][1]);
        Assert.Equal("OUVERTURE PDF", preview.Rows[0][2]);
        Assert.Equal("RBBOA082026", preview.Rows[0][7]);
        Assert.Equal("20261001", preview.Rows[^1][0]);
        Assert.Equal("20261001", preview.Rows[^1][1]);
        Assert.Equal("CLÔTURE PDF", preview.Rows[^1][2]);
        Assert.Equal("RBBOA102026", preview.Rows[^1][7]);
        Assert.Equal(new int?[] { null, 7, 90, null }, preview.RowSequences);
        Assert.Equal(new[] { "1", "2", "3", "4" }, preview.Rows.Select(r => r[9]));
        Assert.Equal(before, JsonSerializer.Serialize(statement));
        Assert.Equal(beforeTemplate, JsonSerializer.Serialize(template));
    }
    [Fact]
    public async Task MissingBalanceDatesAndUnknownDirectionBlockDownload()
    {
        var s = ExportFixtures.Statement with
        {
            PeriodStart = null,
            PeriodEnd = null
        };
        var result = await ExportFixtures.Engine.GenerateAsync(s, ExportFixtures.X3(false), default);
        Assert.Empty(result.Content);
        Assert.Contains(result.Errors, e => e.Code == "REQUIRED_VALUE_MISSING" && e.SourceField == "Date" && e.TransactionSequence == null);
        s = ExportFixtures.Statement with
        {
            Transactions = [ExportFixtures.Statement.Transactions[0] with { Direction = "UNKNOWN" }, ExportFixtures.Statement.Transactions[1]]
        };
        Assert.Contains((await ExportFixtures.Engine.GenerateAsync(s, ExportFixtures.X3(false), default)).Errors, e => e.Code == "INVALID_DIRECTION" && e.TransactionSequence == 1);
    }
    [Fact]
    public async Task ClosingBalanceEncodingIsCheckedBeyondThePreviewLimit()
    {
        var s = ExportFixtures.Statement with
        {
            TotalDebit = null,
            TotalCredit = null,
            BalanceConvention = null,
            ClosingBalanceDescription = "إغلاق",
            Transactions = Enumerable.Range(1, 60).Select(i => new StatementTransaction { Sequence = i, Date = new(2026, 9, 1), ValueDate = new(2026, 9, 1), Credit = 1m }).ToList()
        };
        var template = ExportFixtures.X3(true);
        template.Fields.Single(f => f.SourceField == "Description").SourceField = "DescriptionWithBalances";
        var preview = await ExportFixtures.Engine.PreviewAsync(s, template, default);
        Assert.Equal(62, preview.TotalRows);
        Assert.Equal(50, preview.Rows.Count);
        Assert.Contains(preview.Errors, e => e.Code == "ENCODING_ERROR" && e.TransactionSequence == null);
        var final = await ExportFixtures.Engine.GenerateAsync(s, template, default);
        Assert.Empty(final.Content);
        Assert.Equal(preview.Errors, final.Errors);
    }
}
