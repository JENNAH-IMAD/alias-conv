using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;

namespace BankStatementConverter.Tests;

public class StatementExportPreparationTests
{
    private static ExportTemplate Model(params string[] sources) => new()
    {
        Type = "CUSTOMCSV",
        Fields = sources.Select((source, index) =>
        new ExportTemplateField { FieldName = source, SourceField = source, Position = index, Required = true }).ToList()
    };
    private static PdfTextDocument Raw(bool duplicate = false) => new("PADDLE_OCR", [new(1,600,800,[], Tables:[new(
        duplicate ? new IReadOnlyList<string>[] {
            new[] {"Date", "Valeur", "Libellé", "Débit", "Crédit"},
            new[] {"01/09/2026", "02/09/2026", "CLIENT", "", "12500,00"},
            new[] {"03/09/2026", "02/09/2026", "CLIENT", "", "12500,00"}
        } : new IReadOnlyList<string>[] {
            new[] {"Date", "Valeur", "Libellé", "Débit", "Crédit"},
            new[] {"01/09/2026", "02/09/2026", "CLIENT", "", "12500,00"}
        })])]);
    private static BankStatement MissingDate => ExportFixtures.Statement with
    {
        Transactions = [ExportFixtures.Statement.Transactions[0] with {
        Date=null,ValueDate=new(2026,9,2),Description="CLIENT"}]
    };

    [Fact]
    public void SelectedModelCompletesOnlyItsDerivedSourcesAndKeepsIdentity()
    {
        var original = ExportFixtures.Statement;
        var missing = original with
        {
            Transactions = original.Transactions.Select(t => t with { Amount = null, Direction = null }).ToArray()
        };
        var result = StatementExportPreparation.Complete(missing, Model("Amount", "Direction"), [], null, new(), default);
        Assert.Equal(original.Transactions, result.Statement.Transactions);
        Assert.Equal(4, result.Warnings.Count(w => w.Code == "EXPORT_FIELD_COMPLETED"));
        var unused = StatementExportPreparation.Complete(missing, Model("Date"), [], null, new(), default);
        Assert.All(unused.Statement.Transactions, t => { Assert.Null(t.Amount); Assert.Null(t.Direction); });
    }

    [Fact]
    public void ContradictoryOrAmbiguousFinancialValuesAreNeverSilentlyRepaired()
    {
        var source = ExportFixtures.Statement.Transactions[0];
        StatementTransaction[] rows = [source with {Amount=1m,Direction=null},source with {Debit=4m,Amount=null,Direction=null},
            source with {Credit=null,Debit=-4m,Amount=null,Direction=null},source with {Amount=null,Direction="UNKNOWN"}];
        var result = StatementExportPreparation.Complete(ExportFixtures.Statement with
        {
            Transactions = rows
        }, Model("Amount", "Direction"), [], null, new(), default);
        Assert.Equal(rows, result.Statement.Transactions);
    }

    [Fact]
    public void RequiredOperationDateIsRecoveredOnlyFromUniquelyMatchingPrintedMovement()
    {
        var statement = MissingDate;
        var result = StatementExportPreparation.Complete(statement, Model("Date"), [], Raw(), new(), default);
        Assert.Equal(new DateOnly(2026, 9, 1), result.Statement.Transactions[0].Date);
        Assert.Equal(statement.Transactions[0].Id, result.Statement.Transactions[0].Id);
        Assert.Equal(new DateOnly(2026, 9, 2), result.Statement.Transactions[0].ValueDate);
        Assert.Contains(result.Warnings, w => w.Code == "EXPORT_FIELD_COMPLETED" && w.SourceField == "Date");
    }

    [Fact]
    public void RepeatedDescriptionsAndAmountsLeaveAmbiguousDatesUnfilled()
    {
        var result = StatementExportPreparation.Complete(MissingDate, Model("Date"), [], Raw(true), new(), default);
        Assert.Null(result.Statement.Transactions[0].Date);
        var current = MissingDate with
        {
            Transactions = [MissingDate.Transactions[0], MissingDate.Transactions[0] with { Id = Guid.NewGuid(), Sequence = 2 }]
        };
        var reused = StatementExportPreparation.Complete(current, Model("Date"), [], Raw(), new(), default);
        Assert.All(reused.Statement.Transactions, t => Assert.Null(t.Date));
    }

    [Fact]
    public void ManualDateAndDescriptionAreNotOverwrittenAndNoSourceMeansNoInventedDate()
    {
        var manual = MissingDate with
        {
            Transactions = [MissingDate.Transactions[0] with { Date = new(2026, 9, 7), Description = "CORRECTION" }]
        };
        var result = StatementExportPreparation.Complete(manual, Model("Date"), [], Raw(), new(), default);
        Assert.Equal(manual.Transactions, result.Statement.Transactions);
        var noRaw = StatementExportPreparation.Complete(MissingDate, Model("Date", "Journal", "Account"), [], null, new(), default);
        Assert.Null(noRaw.Statement.Transactions[0].Date);
        Assert.Null(noRaw.Statement.Journal);
        Assert.Null(noRaw.Statement.Account);
    }

    [Fact]
    public void OptionalSelectedDateIsAlsoRecoveredFromUniquePrintedMovement()
    {
        var model = new ExportTemplate { Fields = [new() { FieldName = "Date", SourceField = "Date", Position = 0, Required = false }] };
        var result = StatementExportPreparation.Complete(MissingDate, model, [], Raw(), new(), default);
        Assert.Equal(new DateOnly(2026, 9, 1), result.Statement.Transactions[0].Date);
    }

    [Fact]
    public void SelectedRunningBalanceRequiresChronologyAndReconciledEndpoints()
    {
        var s = ExportFixtures.Statement;
        var completed = StatementExportPreparation.Complete(s, Model("Balance"), [], null, new(), default);
        Assert.Equal(new decimal?[] { 13500m, 13374.50m }, completed.Statement.Transactions.Select(t => t.Balance));
        Assert.All(s.Transactions, t => Assert.Null(t.Balance));
        foreach (var invalid in new[] {
            s with {ClosingBalance=1m},s with {OpeningBalance=null},s with {BalanceConvention=null},
            s with {Transactions=s.Transactions.Reverse().ToArray()},
            s with {Transactions=[s.Transactions[0] with {Balance=10m},s.Transactions[1]]}})
        {
            var result = StatementExportPreparation.Complete(invalid, Model("Balance"), [], null, new(), default);
            Assert.Equal(invalid.Transactions, result.Statement.Transactions);
        }
    }
}
