namespace BankStatementConverter.Domain;

public static class StatementAmounts
{
    public static StatementTransaction Normalize(StatementTransaction row)
    {
        if (row.Debit != 0 && row.Credit != 0 && row.Amount != 0 && (row.Debit.HasValue || row.Credit.HasValue || row.Amount.HasValue))
            return row;
        var debit = row.Debit == 0 ? null : row.Debit;
        var credit = row.Credit == 0 ? null : row.Credit;
        var amount = row.Amount == 0 ? null : row.Amount;
        var direction = row.Direction;
        if (debit.HasValue != credit.HasValue)
        {
            if (direction is null or "" or "UNKNOWN" or "DEBIT" or "CREDIT")
                direction = debit.HasValue ? "DEBIT" : "CREDIT";
            amount ??= debit ?? credit;
        }
        else if (!debit.HasValue && !credit.HasValue && !amount.HasValue && direction is null or "" or "UNKNOWN" or "DEBIT" or "CREDIT")
            direction = null;
        return row with
        {
            Debit = debit,
            Credit = credit,
            Amount = amount,
            Direction = direction
        };
    }

    public static BankStatement Normalize(BankStatement statement) => statement with
    {
        Transactions = statement.Transactions?.Select(row => row == null ? null! : Normalize(row)).ToList()!,
        Extracts = statement.Extracts?.Select(extract => extract with { Transactions = extract.Transactions.Select(Normalize).ToList() }).ToList() ?? []
    };
}
