using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;

namespace BankStatementConverter.Tests;

public class StatementExtractTests
{
    [Fact]
    public async Task ImportedExtractConvertsAllSourceRowsWithoutRequiringOrApplyingASavedFilter()
    {
        var imported = ExportFixtures.Statement with
        {
            ConversionType = "EXTRACT",
            OpeningBalance = null,
            ClosingBalance = null
        };
        var saved = StatementExtracts.Create(imported, "frais", 1);
        imported = imported with
        {
            Extracts = [saved]
        };
        var whole = StatementExtracts.ForExport(imported, null);
        Assert.Same(imported, whole);
        Assert.Equal(2, whole.Transactions.Count);
        Assert.Null(whole.OpeningBalance);
        Assert.Null(whole.ClosingBalance);
        var preview = await ExportFixtures.Engine.PreviewAsync(whole, ExportFixtures.Csv(), default);
        Assert.Empty(preview.Errors);
        Assert.Equal(2, preview.TotalRows);
        Assert.Contains("VIREMENT CLIENT", preview.Text);
        Assert.Contains("FRAIS BANCAIRES", preview.Text);
        var download = await ExportFixtures.Engine.GenerateAsync(whole with
        {
            IsValidated = true
        }, ExportFixtures.Csv(), default);
        Assert.Empty(download.Errors);
        Assert.NotEmpty(download.Content);
        Assert.Single(StatementExtracts.ForExport(imported, saved.Id).Transactions);
    }
    [Fact]
    public void ExclusionsAcceptSeveralFreeTextsAndWorkWithoutAnInclusionFilter()
    {
        var statement = new BankStatement { Transactions = [new() { Description = "VIREMENT REÇU", Credit = 100m }, new() { Description = "Virement annulé", Credit = 300m }, new() { Description = "FRAIS", Debit = 10m }] };
        var filtered = StatementExtracts.Create(statement, "virement", 1, "annule, frais");
        Assert.Single(filtered.Transactions);
        Assert.Equal(100m, filtered.Balance);
        var exclusionOnly = StatementExtracts.Create(statement, "", 1, "annule; frais");
        Assert.Single(exclusionOnly.Transactions);
        Assert.Equal("annule; frais", exclusionOnly.ExcludeText);
        var scoped = StatementExtracts.ForExport(statement with
        {
            Extracts = [filtered]
        }, filtered.Id);
        Assert.Single(scoped.Transactions);
        Assert.Equal(0m, scoped.OpeningBalance);
        Assert.Equal(100m, scoped.ClosingBalance);
        Assert.Equal(3, statement.Transactions.Count);
    }
    [Fact]
    public void FreeTextMatchesPhrasesWithoutCaseOrAccentsAndCalculatesExactTotals()
    {
        var statement = new BankStatement
        {
            Currency = "MAD",
            Transactions = [
            new() {Description="VIREMENT REÇU client A",Credit=0.1m},
            new() {Description="virement reçu client B",Debit=0.02m,Credit=0.2m},
            new() {Description="FRAIS",Debit=999m},
            new() {Description=null,Credit=300m}]
        };
        var extract = StatementExtracts.Create(statement, "  virement recu  ", 7);
        Assert.Equal(2, extract.Transactions.Count);
        Assert.Equal(0.02m, extract.TotalDebit);
        Assert.Equal(0.3m, extract.TotalCredit);
        Assert.Equal(0.28m, extract.Balance);
        Assert.Equal(7, extract.SourceVersion);
        Assert.Equal("virement recu", extract.FilterText);
        Assert.Equal(4, statement.Transactions.Count);
        var corrected = statement with
        {
            Transactions = []
        };
        Assert.Empty(corrected.Transactions);
        Assert.Equal(2, extract.Transactions.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("absent")]
    public void EmptyOrUnmatchedFiltersCannotBeSaved(string filter)
        => Assert.Throws<AppException>(() => StatementExtracts.Create(new BankStatement { Transactions = [new() { Description = "Frais" }] }, filter, 1));

    [Fact]
    public void CsvContainsSnapshotRowsQuotedLabelsAndCalculatedTotals()
    {
        var extract = StatementExtracts.Create(new BankStatement { Currency = "MAD", Transactions = [new() { Description = "Frais; \"tenue\"", Debit = 125.50m }] }, "frais", 1);
        var csv = Encoding.UTF8.GetString(StatementExtracts.Csv(extract));
        Assert.Contains("\"Frais; \"\"tenue\"\"\"", csv);
        Assert.Contains("\"TOTAL\";\"125,50\";\"0,00\";\"MAD\"", csv);
        Assert.Contains("\"SOLDE (CRÉDIT - DÉBIT)\";;\"-125,50\"", csv);
    }
}
