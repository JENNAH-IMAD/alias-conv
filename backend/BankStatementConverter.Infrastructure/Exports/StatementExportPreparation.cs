using System.Text.RegularExpressions;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Exports;

// Preparation is driven by the selected model. Existing values, row identities and order remain authoritative.
public static class StatementExportPreparation
{
    private static readonly string[] RecoverableSources = ["Date", "ValueDate", "Description", "MovementDescription", "DescriptionWithBalances", "Reference"];

    public static PdfStatementExtraction Complete(BankStatement statement, ExportTemplate template,
        IReadOnlyList<ExportError> diagnostics, PdfTextDocument? raw, StatementPdfExtractor normalizer, CancellationToken ct)
    {
        statement = StatementAmounts.Normalize(statement);
        var prepared = raw == null ? new PdfStatementExtraction(statement, diagnostics) :
            PdfStatementMetadata.Complete(new(statement, diagnostics, raw), statement);
        var warnings = prepared.Warnings.ToList();
        var sources = template.Fields.Select(f => f.SourceField).ToHashSet(StringComparer.Ordinal);
        var required = template.Fields.Where(f => RecoverableSources.Contains(f.SourceField))
            .Select(f => f.SourceField).ToHashSet(StringComparer.Ordinal);
        var rows = prepared.Statement.Transactions.ToArray();
        if (raw != null && rows.Any(row => required.Any(source => Missing(row, source))))
        {
            try
            {
                // Reuse saved text; preparation does not send the document to OCR again.
                var reread = normalizer.Normalize(raw, statement.BankName, statement.BankCode ?? "", statement.Currency ?? "",
                    ct, raw.LastMapping, prepared.Statement);
                rows = RecoverMissing(rows, reread.Statement.Transactions, required, warnings, ct);
            }
            catch (AppException exception) when (exception.Status == 422)
            {
                warnings.Add(new("EXPORT_PREPARATION_REVIEW_REQUIRED",
                    "Le texte conservé ne permet pas de compléter avec certitude toutes les colonnes du modèle. Vérifiez les champs signalés avec le PDF."));
            }
        }
        for (var index = 0; index < rows.Length; index++)
        {
            ct.ThrowIfCancellationRequested();
            var row = rows[index];
            var amount = Magnitude(row);
            if (!amount.HasValue || row.Amount.HasValue && row.Amount != amount)
                continue;
            var direction = row.Debit.HasValue ? "DEBIT" : "CREDIT";
            if (!string.IsNullOrWhiteSpace(row.Direction) && row.Direction != direction)
                continue;
            if (sources.Contains("Amount") && !row.Amount.HasValue)
            {
                row = row with
                {
                    Amount = amount
                };
                warnings.Add(Completed(row, "Amount", "Montant complété à partir de l’unique colonne débit ou crédit renseignée."));
            }
            if (sources.Contains("Direction") && string.IsNullOrWhiteSpace(row.Direction))
            {
                row = row with
                {
                    Direction = direction
                };
                warnings.Add(Completed(row, "Direction", "Sens complété à partir de l’unique colonne débit ou crédit renseignée."));
            }
            rows[index] = row;
        }
        if (sources.Contains("Balance"))
            rows = CompleteRunningBalances(prepared.Statement, rows, warnings);
        return prepared with
        {
            Statement = prepared.Statement with
            {
                Transactions = rows
            },
            Warnings = warnings.DistinctBy(w => (w.Code, w.TransactionSequence, w.SourceField)).ToList()
        };
    }

    private static StatementTransaction[] CompleteRunningBalances(BankStatement statement, StatementTransaction[] rows, List<ExportError> warnings)
    {
        // Printed order is authoritative. Descending or undated statements cannot be
        // assigned running balances without changing their meaning.
        if (!statement.OpeningBalance.HasValue || !statement.ClosingBalance.HasValue
            || statement.BalanceConvention is not ("CREDIT_INCREASES" or "DEBIT_INCREASES")
            || rows.Any(r => !r.Date.HasValue || !Magnitude(r).HasValue)
            || rows.Zip(rows.Skip(1)).Any(pair => pair.First.Date > pair.Second.Date)
            || new StatementValidator().Validate(statement with
            {
                Transactions = rows
            }).Errors.Count != 0)
            return rows;
        try
        {
            var balance = statement.OpeningBalance.Value;
            var computed = new decimal[rows.Length];
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                balance += statement.BalanceConvention == "CREDIT_INCREASES"
                    ? (row.Credit ?? 0m) - (row.Debit ?? 0m) : (row.Debit ?? 0m) - (row.Credit ?? 0m);
                computed[index] = balance;
                if (row.Balance.HasValue && row.Balance != balance)
                    return rows;
            }
            if (balance != statement.ClosingBalance)
                return rows;
            return rows.Select((row, index) =>
            {
                if (row.Balance.HasValue)
                    return row;
                warnings.Add(Completed(row, "Balance", "Solde progressif calculé depuis le solde initial et les mouvements, avec contrôle du solde final."));
                return row with
                {
                    Balance = computed[index]
                };
            }).ToArray();
        }
        catch (OverflowException) { return rows; }
    }

    private static StatementTransaction[] RecoverMissing(StatementTransaction[] current, IReadOnlyList<StatementTransaction> extracted,
        HashSet<string> required, List<ExportError> warnings, CancellationToken ct)
    {
        var byDescription = new Dictionary<(string, decimal), List<int>>();
        var byReference = new Dictionary<(string, decimal), List<int>>();
        for (var index = 0; index < extracted.Count; index++)
        {
            var candidate = extracted[index];
            if (MatchAmount(candidate) is not { } amount)
                continue;
            Add(byDescription, candidate.Description, amount, index);
            Add(byReference, candidate.Reference, amount, index);
        }
        var pairs = new List<(int Current, int Candidate)>();
        // Include complete rows when checking uniqueness: a new value may never be borrowed from another movement.
        for (var index = 0; index < current.Length; index++)
        {
            ct.ThrowIfCancellationRequested();
            var row = current[index];
            if (MatchAmount(row) is not { } amount)
                continue;
            var key = !string.IsNullOrWhiteSpace(row.Reference) ? Text(row.Reference) : Text(row.Description);
            var lookup = !string.IsNullOrWhiteSpace(row.Reference) ? byReference : byDescription;
            if (key.Length == 0 || !lookup.TryGetValue((key, amount), out var options))
                continue;
            var candidates = options.Where(i => Compatible(row, extracted[i])).Take(2).ToArray();
            if (candidates.Length == 1)
                pairs.Add((index, candidates[0]));
        }
        var uniquelyUsed = pairs.GroupBy(pair => pair.Candidate).Where(group => group.Count() == 1).SelectMany(group => group);
        var result = current.ToArray();
        foreach (var pair in uniquelyUsed)
        {
            var row = current[pair.Current];
            var candidate = extracted[pair.Candidate];
            foreach (var source in required.Where(source => Missing(row, source) && !Missing(candidate, source)))
            {
                row = source switch
                {
                    "Date" => row with { Date = candidate.Date },
                    "ValueDate" => row with { ValueDate = candidate.ValueDate },
                    "Reference" => row with { Reference = candidate.Reference },
                    _ => row with { Description = candidate.Description }
                };
                warnings.Add(Completed(row, source, "Champ complété depuis le texte conservé du PDF après identification unique du mouvement. Vérifiez-le avec l’original."));
            }
            result[pair.Current] = row;
        }
        return result;
    }

    private static ExportError Completed(StatementTransaction row, string source, string message) =>
        new("EXPORT_FIELD_COMPLETED", message, row.Sequence, SourceField: source);
    private static decimal? Magnitude(StatementTransaction row) => row.Debit.HasValue != row.Credit.HasValue
        && !(row.Debit < 0) && !(row.Credit < 0) ? row.Debit ?? row.Credit : null;
    private static decimal? MatchAmount(StatementTransaction row)
    {
        var magnitude = Magnitude(row);
        if (row.Debit.HasValue || row.Credit.HasValue)
            return magnitude.HasValue && (!row.Amount.HasValue || row.Amount == magnitude) ? magnitude : null;
        return row.Amount >= 0 ? row.Amount : null;
    }
    private static string Text(string? text) => Regex.Replace(text ?? "", @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim().ToUpperInvariant();
    private static void Add(Dictionary<(string, decimal), List<int>> lookup, string? value, decimal amount, int index)
    {
        var key = Text(value);
        if (key.Length == 0)
            return;
        if (!lookup.TryGetValue((key, amount), out var indices))
            lookup[(key, amount)] = indices = [];
        indices.Add(index);
    }
    private static bool Compatible(StatementTransaction row, StatementTransaction candidate) =>
        (!row.Date.HasValue || row.Date == candidate.Date) && (!row.ValueDate.HasValue || row.ValueDate == candidate.ValueDate)
        && (!row.Debit.HasValue || row.Debit == candidate.Debit) && (!row.Credit.HasValue || row.Credit == candidate.Credit)
        && (!row.Amount.HasValue || row.Amount == MatchAmount(candidate))
        && (string.IsNullOrWhiteSpace(row.Direction) || row.Direction == candidate.Direction)
        && (string.IsNullOrWhiteSpace(row.Description) || Text(row.Description) == Text(candidate.Description))
        && (string.IsNullOrWhiteSpace(row.Reference) || Text(row.Reference) == Text(candidate.Reference));
    private static bool Missing(StatementTransaction row, string source) => source switch
    {
        "Date" => !row.Date.HasValue,
        "ValueDate" => !row.ValueDate.HasValue,
        "Reference" => string.IsNullOrWhiteSpace(row.Reference),
        _ => string.IsNullOrWhiteSpace(row.Description)
    };
}
