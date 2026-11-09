using System.Globalization;
using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Exports;

public static class StatementExtracts
{
    public static byte[] Csv(StatementExtract extract)
    {
        static string Cell(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        static string Amount(decimal? value) => value?.ToString("0.00##########################", CultureInfo.GetCultureInfo("fr-FR")) ?? "";
        var text = new StringBuilder("Date;Date valeur;Libellé;Débit;Crédit;Devise\r\n");
        foreach (var row in extract.Transactions)
            text.AppendLine(string.Join(';', Cell(row.Date?.ToString("dd/MM/yyyy")), Cell(row.ValueDate?.ToString("dd/MM/yyyy")), Cell(row.Description), Cell(Amount(row.Debit)), Cell(Amount(row.Credit)), Cell(extract.Currency)));
        text.AppendLine(string.Join(';', "", "", Cell("TOTAL"), Cell(Amount(extract.TotalDebit)), Cell(Amount(extract.TotalCredit)), Cell(extract.Currency)));
        text.AppendLine(string.Join(';', "", "", Cell("SOLDE (CRÉDIT - DÉBIT)"), "", Cell(Amount(extract.Balance)), Cell(extract.Currency)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
    }

    public static string Normalize(string text) => string.Concat(string.Concat(text.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        .Normalize(NormalizationForm.FormC).ToLowerInvariant().Select(c => char.IsWhiteSpace(c) ? ' ' : c));

    public static BankStatement ForExport(BankStatement statement, Guid? extractId)
    {
        if (!extractId.HasValue)
        {
            // An imported extract is already a source document: convert every supplied row.
            // A saved filter is an optional, explicitly selected subset of that document.
            return statement;
        }
        var extract = statement.Extracts.SingleOrDefault(x => x.Id == extractId) ?? throw new AppException(404, "Extrait introuvable.");
        decimal balance = 0;
        var rows = extract.Transactions.Select(t => t with { Balance = balance += statement.BalanceConvention == "DEBIT_INCREASES" ? (t.Debit ?? 0) - (t.Credit ?? 0) : (t.Credit ?? 0) - (t.Debit ?? 0) }).ToList();
        return statement with
        {
            ConversionType = "EXTRACT",
            Transactions = rows,
            Currency = extract.Currency,
            TotalDebit = extract.TotalDebit,
            TotalCredit = extract.TotalCredit,
            OpeningBalance = 0,
            ClosingBalance = balance,
            BalanceConvention = statement.BalanceConvention,
            OpeningBalanceDescription = "Début de l’extrait",
            ClosingBalanceDescription = "Solde de l’extrait"
        };
    }

    // Match the exact formatted export cells, never inferred source-field labels.
    // Balance rows do not select transactions; column indices also support repeated labels.
    public static StatementExtract CreateByColumns(BankStatement statement, ExportPreview preview,
        IReadOnlyList<ExportColumnFilter> filters, int version, Guid templateId, string fingerprint)
    {
        Validation.Require(filters.All(f => f != null && f.Value != null), "Filtre de colonne invalide.");
        var active = filters.Select(f => f with { Value = f.Value.Trim() }).Where(f => f.Value.Length > 0).ToArray();
        Validation.Require(active.Length > 0 && active.Length <= preview.Columns.Count
            && active.Select(f => f.ColumnIndex).Distinct().Count() == active.Length
            && active.All(f => f.ColumnIndex >= 0 && f.ColumnIndex < preview.Columns.Count && f.Value.Length <= 2000),
            "Vérifiez les filtres et les colonnes du modèle sélectionné.");
        var criteria = active.Select(f => f with { Value = Normalize(f.Value) }).ToArray();
        var sequences = new HashSet<int>();
        for (var r = 0; r < preview.Rows.Count; r++)
        {
            if (preview.RowKinds?[r] is not (null or "TRANSACTION") || preview.RowSequences[r] is not int sequence)
                continue;
            var cells = preview.Rows[r];
            var indices = preview.RowColumnIndices?[r] ?? Enumerable.Range(0, cells.Length).ToArray();
            if (criteria.All(f =>
            {
                var cell = Array.IndexOf(indices, f.ColumnIndex);
                return cell >= 0 && Normalize(cells[cell]).Contains(f.Value, StringComparison.Ordinal);
            }))
                sequences.Add(sequence);
        }
        var rows = statement.Transactions.Where(t => sequences.Contains(t.Sequence)).ToList();
        Validation.Require(rows.Count > 0, "Aucun mouvement ne correspond aux filtres des colonnes.");
        var debit = rows.Sum(t => t.Debit ?? 0);
        var credit = rows.Sum(t => t.Credit ?? 0);
        var label = string.Join(" · ", active.Select(f => $"{preview.Columns[f.ColumnIndex]} : {f.Value}"));
        return new(Guid.NewGuid(), label, DateTime.UtcNow, version, statement.Currency, rows,
            debit, credit, credit - debit, "", templateId, active, fingerprint);
    }

    public static StatementExtract Create(BankStatement statement, string filterText, int version, string excludeText = "")
    {
        var text = filterText.Trim();
        var excluded = excludeText.Trim();
        Validation.Require(text.Length <= 2000 && excluded.Length <= 2000 && (text.Length > 0 || excluded.Length > 0), "Saisissez un texte à inclure ou à exclure (2 000 caractères maximum par champ).");
        var filter = Normalize(text);
        var exclusions = excluded.Split([',', ';', '\n', '\r'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(Normalize).ToArray();
        Validation.Require(text.Length > 0 || exclusions.Length > 0, "Saisissez un texte à inclure ou à exclure.");
        var rows = statement.Transactions.Where(t =>
        {
            var description = Normalize(t.Description ?? "");
            return description.Contains(filter, StringComparison.Ordinal) && !exclusions.Any(e => description.Contains(e, StringComparison.Ordinal));
        }).ToList();
        Validation.Require(rows.Count > 0, "Aucun mouvement ne correspond au texte saisi.");
        var debit = rows.Sum(t => t.Debit ?? 0);
        var credit = rows.Sum(t => t.Credit ?? 0);
        return new(Guid.NewGuid(), text, DateTime.UtcNow, version, statement.Currency, rows, debit, credit, credit - debit, excluded);
    }
}
