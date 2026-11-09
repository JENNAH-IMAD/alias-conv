using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
namespace BankStatementConverter.Infrastructure.Exports;

public sealed class ExportSourceResolver : IExportSourceResolver
{
    public ResolvedExportSource Resolve(string source, BankStatement s, StatementTransaction t)
    {
        // Sage X3 has already projected balance rows, where a zero balance remains meaningful.
        if (t.Direction is not ("1" or "-1"))
            t = StatementAmounts.Normalize(t);
        return source switch
        {
            "Date" => new(true, t.Date),
            "Day" => new(true, t.Date?.Day.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)),
            "ValueDate" => new(true, t.ValueDate),
            "Description" or "MovementDescription" or "DescriptionWithBalances" => new(true, t.Description),
            "Debit" => new(true, t.Debit),
            "Credit" => new(true, t.Credit),
            "Reference" => new(true, t.Reference),
            // Magnitude fallback, never infer a sign or modify the transaction.
            "Amount" => new(true, t.Amount ?? (t.Debit.HasValue && !t.Credit.HasValue ? t.Debit : t.Credit.HasValue && !t.Debit.HasValue ? t.Credit : null)),
            "Direction" => new(true, t.Direction),
            "Balance" => new(true, t.Balance),
            "Currency" => new(true, string.IsNullOrWhiteSpace(t.Currency) ? s.Currency : t.Currency),
            "BankCode" => new(true, s.BankCode),
            "StatementNumber" => new(true, s.StatementNumber),
            "Sequence" => new(true, t.Sequence),
            "OpeningBalance" => new(true, s.OpeningBalance),
            "ClosingBalance" => new(true, s.ClosingBalance),
            "Journal" => new(true, t.Journal ?? s.Journal),
            "Account" => new(true, t.Account ?? s.Account),
            "BankJournal" => new(true, s.BankJournal),
            "BankAccount" => new(true, s.BankAccount),
            "AccountingDebit" => new(true, t.Debit.HasValue == t.Credit.HasValue ? null : s.BalanceConvention switch
            {
                "CREDIT_INCREASES" => t.Credit ?? 0m,
                "DEBIT_INCREASES" => t.Debit ?? 0m,
                _ => null
            }),
            "AccountingCredit" => new(true, t.Debit.HasValue == t.Credit.HasValue ? null : s.BalanceConvention switch
            {
                "CREDIT_INCREASES" => t.Debit ?? 0m,
                "DEBIT_INCREASES" => t.Credit ?? 0m,
                _ => null
            }),
            "Analytic" => new(true, t.Analytic ?? s.Analytic),
            "RecordType" => new(true, t.RecordType ?? s.RecordType),
            "PieceNumber" or "ThirdParty" or "InvoiceNumber" or "DueDate" or "PaymentMethod" or "Pointage" => new(true, null),
            _ => new(false, null)
        };
    }
}
