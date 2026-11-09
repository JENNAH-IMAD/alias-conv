using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Accounting;

// Shared monetary validation for classified entry results.
public static class AccountingEngine
{
    public static bool Money(decimal n) => n == decimal.Round(n, 2) && n >= -999999999999999m && n <= 999999999999999m;
    public static (decimal Debit, decimal Credit)? BankAmounts(BankStatement statement, StatementTransaction transaction)
    {
        var t = StatementAmounts.Normalize(transaction);
        if (t.Debit.HasValue == t.Credit.HasValue || (t.Debit ?? t.Credit) <= 0 || !Money((t.Debit ?? t.Credit)!.Value)) return null;
        return statement.BalanceConvention switch
        {
            "CREDIT_INCREASES" => (t.Credit ?? 0, t.Debit ?? 0),
            "DEBIT_INCREASES" => (t.Debit ?? 0, t.Credit ?? 0),
            _ => null
        };
    }
}
