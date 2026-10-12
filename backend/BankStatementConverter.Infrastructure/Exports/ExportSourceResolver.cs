using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
namespace BankStatementConverter.Infrastructure.Exports;

public sealed class ExportSourceResolver : IExportSourceResolver
{
    public ResolvedExportSource Resolve(string source, BankStatement s, StatementTransaction t) => source switch
    {
        "Date" => new(true,t.Date), "ValueDate" => new(true,t.ValueDate), "Description" or "MovementDescription" or "DescriptionWithBalances" => new(true,t.Description),
        "Debit" => new(true,t.Debit), "Credit" => new(true,t.Credit), "Reference" => new(true,t.Reference),
        // Magnitude fallback, never infer a sign or modify the transaction.
        "Amount" => new(true,t.Amount ?? (t.Debit.HasValue && !t.Credit.HasValue ? t.Debit : t.Credit.HasValue && !t.Debit.HasValue ? t.Credit : null)),
        "Direction" => new(true,t.Direction), "Balance" => new(true,t.Balance),
        "Currency" => new(true,string.IsNullOrWhiteSpace(t.Currency) ? s.Currency : t.Currency),
        "BankCode" => new(true,s.BankCode), "StatementNumber" => new(true,s.StatementNumber), "Sequence" => new(true,t.Sequence),
        "OpeningBalance" => new(true,s.OpeningBalance), "ClosingBalance" => new(true,s.ClosingBalance),
        "Journal" => new(true,t.Journal ?? s.Journal), "Account" => new(true,t.Account ?? s.Account),
        "Analytic" => new(true,t.Analytic ?? s.Analytic), "RecordType" => new(true,t.RecordType ?? s.RecordType),
        _ => new(false,null)
    };
}
