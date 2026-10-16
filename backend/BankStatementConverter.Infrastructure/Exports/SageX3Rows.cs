using System.Globalization;
using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Exports;

// A projection for export only: imported movements and template metadata stay intact.
internal sealed record ExportRow(BankStatement Statement, StatementTransaction Transaction, int? OriginalSequence, string Kind);
internal static class SageX3Rows
{
    public const string ProfileVersion = "sage-x3-balances-v3-debit-1-credit-minus1";
    public static IEnumerable<ExportRow> Build(BankStatement statement)
    {
        statement = StatementAmounts.Normalize(statement);
        var code = statement.BankCode?.Trim().ToUpperInvariant();
        if (code == "AWB")
            code = "ATT";
        var context = statement with
        {
            BankCode = code
        };
        var line = 1;
        ExportRow Row(StatementTransaction transaction, int? original, string kind)
        {
            var number = transaction.Date?.ToString("MMyyyy", CultureInfo.InvariantCulture);
            return new(context with
            {
                StatementNumber = string.IsNullOrWhiteSpace(code) || number == null ? null : $"RB{code}{number}"
            },
                transaction with
                {
                    Sequence = line++,
                    Currency = statement.Currency
                }, original, kind);
        }
        StatementTransaction Balance(bool opening)
        {
            var amount = opening ? statement.OpeningBalance : statement.ClosingBalance;
            var date = opening ? statement.OpeningBalanceDate ?? statement.PeriodStart : statement.ClosingBalanceDate ?? statement.PeriodEnd;
            return new()
            {
                Date = date,
                ValueDate = (opening ? statement.OpeningBalanceValueDate : statement.ClosingBalanceValueDate) ?? date,
                Description = opening ? statement.OpeningBalanceDescription : statement.ClosingBalanceDescription,
                Amount = amount.HasValue ? Math.Abs(amount.Value) : null,
                Direction = amount.HasValue ? amount.Value < 0 ? "-1" : "1" : null,
                RecordType = opening ? "1" : "3"
            };
        }
        yield return Row(Balance(true), null, "OPENING_BALANCE");
        foreach (var transaction in statement.Transactions.Where(t => t != null).OrderBy(t => t.Sequence))
        {
            var direction = transaction.Direction switch
            {
                "CREDIT" => "-1",
                "DEBIT" => "1",
                null or "" when transaction.Debit.HasValue != transaction.Credit.HasValue => transaction.Credit.HasValue ? "-1" : "1",
                _ => "UNKNOWN"
            };
            var amount = transaction.Amount ?? (transaction.Debit.HasValue != transaction.Credit.HasValue ? transaction.Debit ?? transaction.Credit : null);
            yield return Row(transaction with
            {
                Amount = amount.HasValue ? Math.Abs(amount.Value) : null,
                Direction = direction,
                RecordType = "2"
            },
                transaction.Sequence, "TRANSACTION");
        }
        yield return Row(Balance(false), null, "CLOSING_BALANCE");
    }
}
