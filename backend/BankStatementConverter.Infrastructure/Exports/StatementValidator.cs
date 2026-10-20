using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
namespace BankStatementConverter.Infrastructure.Exports;

public sealed class StatementValidator
{
    public StatementValidationResult Validate(BankStatement s)
    {
        s = StatementAmounts.Normalize(s);
        var errors = new List<ExportError>();
        var warnings = new List<ExportError>();
        if (s.Transactions is not { Count: > 0 })
            return new(false, [], [new("INVALID_TRANSACTIONS", "Le relevé doit contenir au moins une transaction.")]);
        if (string.IsNullOrWhiteSpace(s.Currency) || s.Currency.Length != 3 || !s.Currency.All(char.IsAsciiLetterUpper))
            errors.Add(new("INVALID_CURRENCY", "La devise du relevé doit comporter trois lettres majuscules."));
        if (s.PeriodStart.HasValue && s.PeriodEnd.HasValue && s.PeriodStart > s.PeriodEnd)
            errors.Add(new("INVALID_PERIOD", "La fin de période précède le début."));
        if (!s.PeriodStart.HasValue || !s.PeriodEnd.HasValue)
            warnings.Add(new("PERIOD_MISSING", "La période du relevé est incomplète."));
        if (s.BalanceConvention is not (null or "" or "CREDIT_INCREASES" or "DEBIT_INCREASES"))
            errors.Add(new("INVALID_BALANCE_CONVENTION", "Convention de solde invalide."));
        var ids = new HashSet<Guid>();
        var sequences = new HashSet<int>();
        var transactions = s.Transactions ?? [];
        foreach (var t in transactions)
        {
            if (t == null)
            {
                errors.Add(new("INVALID_TRANSACTION", "Transaction vide."));
                continue;
            }
            void Error(string code, string message, string? source = null) => errors.Add(new(code, message, t.Sequence, null, source));
            if (t.Id == Guid.Empty || !ids.Add(t.Id))
                Error("INVALID_TRANSACTION_ID", "Identifiant de transaction vide ou dupliqué.");
            if (t.Sequence < 1 || !sequences.Add(t.Sequence))
                Error("INVALID_SEQUENCE", "Les numéros de transaction doivent être positifs et distincts.", "Sequence");
            if (t.Debit.HasValue && t.Credit.HasValue)
                Error("AMBIGUOUS_AMOUNT", "Débit et crédit sont tous deux renseignés ; corrigez explicitement le mouvement.", "Amount");
            if (!t.Debit.HasValue && !t.Credit.HasValue && !t.Amount.HasValue)
                Error("MISSING_TRANSACTION_AMOUNT", "Renseignez le débit, le crédit ou le montant vérifié de cette transaction.", "Amount");
            if (t.Debit < 0 || t.Credit < 0)
                Error("INVALID_AMOUNT", "Un débit ou crédit doit être une magnitude positive ou nulle.", "Amount");
            if (t.Direction is not (null or "" or "DEBIT" or "CREDIT" or "UNKNOWN"))
                Error("INVALID_DIRECTION", "Sens accepté : DEBIT, CREDIT ou UNKNOWN.", "Direction");
            if ((t.Direction == "DEBIT" && t.Credit.HasValue) || (t.Direction == "CREDIT" && t.Debit.HasValue))
                Error("DIRECTION_AMOUNT_MISMATCH", "Le sens fourni contredit le débit/crédit.", "Direction");
            var magnitude = t.Debit.HasValue && !t.Credit.HasValue ? t.Debit : t.Credit.HasValue && !t.Debit.HasValue ? t.Credit : null;
            if (t.Amount.HasValue && magnitude.HasValue && t.Amount != magnitude)
                Error("AMOUNT_MISMATCH", "Le montant fourni diffère du débit/crédit ; aucune correction automatique.", "Amount");
            if (!t.Date.HasValue)
                warnings.Add(new("DATE_MISSING", "Date d’opération manquante.", t.Sequence, null, "Date"));
            if (t.Date.HasValue && ((s.PeriodStart.HasValue && t.Date < s.PeriodStart) || (s.PeriodEnd.HasValue && t.Date > s.PeriodEnd)))
                warnings.Add(new("DATE_OUTSIDE_PERIOD", "Date d’opération hors période.", t.Sequence, null, "Date"));
            if (t.Currency is { Length: > 0 } && (t.Currency.Length != 3 || !t.Currency.All(char.IsAsciiLetterUpper)))
                Error("INVALID_CURRENCY", "Devise de transaction invalide.", "Currency");
            if (t.Currency is { Length: > 0 } && t.Currency != s.Currency)
                Error("MIXED_CURRENCIES", "La devise diffère de celle du relevé ; aucune conversion de devise n’est effectuée.", "Currency");
        }
        var valid = transactions.Where(t => t != null).ToList();
        decimal debit, credit;
        try
        {
            debit = valid.Sum(t => t.Debit ?? 0m);
            credit = valid.Sum(t => t.Credit ?? 0m);
        }
        catch (OverflowException) { errors.Add(new("NUMERIC_OVERFLOW", "Montants trop grands pour contrôler les totaux.")); return new(false, warnings, errors); }
        if (s.TotalDebit.HasValue && s.TotalDebit != debit)
            errors.Add(new("TOTAL_DEBIT_MISMATCH", "Le total des débits fourni ne correspond pas aux mouvements.", SourceField: "TotalDebit"));
        if (s.TotalCredit.HasValue && s.TotalCredit != credit)
            errors.Add(new("TOTAL_CREDIT_MISMATCH", "Le total des crédits fourni ne correspond pas aux mouvements.", SourceField: "TotalCredit"));
        if (s.OpeningBalance.HasValue && s.ClosingBalance.HasValue)
        {
            if (string.IsNullOrEmpty(s.BalanceConvention))
                warnings.Add(new("BALANCE_CONVENTION_MISSING", "Précisez la convention de signe pour contrôler les soldes."));
            else if (valid.Any(t => !t.Debit.HasValue && !t.Credit.HasValue))
                warnings.Add(new("BALANCE_CHECK_UNAVAILABLE", "Certains mouvements n’ont ni débit ni crédit ; le rapprochement des soldes est incomplet."));
            else
            {
                try
                {
                    var delta = s.BalanceConvention == "CREDIT_INCREASES" ? credit - debit : debit - credit;
                    if (s.OpeningBalance + delta != s.ClosingBalance)
                        errors.Add(new("BALANCE_MISMATCH", "Solde initial, mouvements et solde final ne concordent pas. Vérifiez la convention et les montants."));
                }
                catch (OverflowException) { errors.Add(new("NUMERIC_OVERFLOW", "Montants trop grands pour contrôler les soldes.")); }
            }
        }
        return new(errors.Count == 0, warnings, errors);
    }
}
