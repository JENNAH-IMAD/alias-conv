using BankStatementConverter.Application.Exports;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;

namespace BankStatementConverter.Tests;

public class StatementAdaptationMetadataTests
{
    private static readonly ExportError[] Computed = [new("PDF_TOTALS_COMPUTED", "Calculated from extracted movements")];

    [Fact]
    public void ReplayReleasesComputedTotalsButKeepsStatementIdentityAndBalances()
    {
        var old = ExportFixtures.Statement;
        var metadata = StatementAdaptationMetadata.ForReplay(old, Computed);
        Assert.Null(metadata.TotalDebit);
        Assert.Null(metadata.TotalCredit);
        Assert.Equal(old with
        {
            TotalDebit = null,
            TotalCredit = null
        }, metadata);
    }

    [Fact]
    public void ReplayPreservesPrintedTotalsEvenWhenTheyEqualOldMovementSums()
    {
        var old = ExportFixtures.Statement;
        Assert.Same(old, StatementAdaptationMetadata.ForReplay(old, []));
    }

    [Fact]
    public void ReplayKeepsManualCorrectionsAndReleasesOnlyUnchangedComputedTotals()
    {
        var old = ExportFixtures.Statement with
        {
            TotalDebit = 140m
        };
        var metadata = StatementAdaptationMetadata.ForReplay(old, Computed);
        Assert.Equal(140m, metadata.TotalDebit);
        Assert.Null(metadata.TotalCredit);
    }
}
