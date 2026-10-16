using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Exports;

public static class StatementAdaptationMetadata
{
    // A replay must not pin the sums of the old, possibly misread movements.
    // Keep printed totals and any later manual correction; only release a total
    // identified as computed and still equal to the old movement sum.
    public static BankStatement ForReplay(BankStatement statement, IReadOnlyList<ExportError> warnings)
    {
        if (!warnings.Any(w => w.Code == "PDF_TOTALS_COMPUTED"))
            return statement;
        try
        {
            var debit = statement.Transactions.Sum(t => t.Debit ?? 0m);
            var credit = statement.Transactions.Sum(t => t.Credit ?? 0m);
            return statement with
            {
                TotalDebit = statement.TotalDebit == debit ? null : statement.TotalDebit,
                TotalCredit = statement.TotalCredit == credit ? null : statement.TotalCredit
            };
        }
        catch (OverflowException) { return statement; }
    }
}
