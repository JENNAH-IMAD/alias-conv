using BankStatementConverter.Application.Exports;

namespace BankStatementConverter.Infrastructure.Exports;

// A parser returning rows is not proof that its dates, amounts and column boundaries are sound.
internal static class PdfExtractionQuality
{
    internal static int Defects(PdfStatementExtraction extraction)
    {
        var s = BankStatementConverter.Domain.StatementAmounts.Normalize(extraction.Statement);
        if (s.Transactions.Count == 0)
            return int.MaxValue;
        var missing = s.Transactions.Sum(t => (t.Date.HasValue ? 0 : 1)
            + (string.IsNullOrWhiteSpace(t.Description) ? 1 : 0)
            + (t.Debit.HasValue != t.Credit.HasValue ? 0 : 1));
        var validation = new StatementValidator().Validate(s);
        // A missing convention after metadata completion means neither balance equation reconciled.
        var unbalanced = s.OpeningBalance.HasValue && s.ClosingBalance.HasValue && string.IsNullOrEmpty(s.BalanceConvention) ? 1 : 0;
        return missing + validation.Errors.Count + unbalanced
            + extraction.Warnings.Count(w => w.Code is "PDF_DATE_UNREADABLE" or "PDF_AMOUNT_UNREADABLE" or "PDF_METADATA_OVERFLOW");
    }

    internal static bool Improves(PdfStatementExtraction candidate, PdfStatementExtraction current)
    {
        if (Defects(candidate) >= Defects(current))
            return false;
        // A different row count is accepted only with an independently reconciled opening/closing
        // balance. This prevents a smaller, incomplete table from winning by having fewer errors.
        return candidate.Statement.Transactions.Count == current.Statement.Transactions.Count
            || Defects(candidate) == 0 && candidate.Statement.OpeningBalance.HasValue
                && candidate.Statement.ClosingBalance.HasValue && !string.IsNullOrEmpty(candidate.Statement.BalanceConvention);
    }
}
