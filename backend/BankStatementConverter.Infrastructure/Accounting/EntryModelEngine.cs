using System.Globalization;
using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Accounting;

public static class EntryModelEngine
{
    public static string Normalize(string value)
    {
        var text = new string(value.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : ' ').ToArray());
        return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
    public static bool Matches(string description, string keyword) => Normalize(keyword) is { Length: > 0 } key
        && (" " + Normalize(description) + " ").Contains(" " + key + " ", StringComparison.Ordinal);

    public static IReadOnlyList<EntryModelRow> Evaluate(
        BankStatement source, BankAccount bank, IReadOnlyList<AccountingEntryModel> models,
        IReadOnlyDictionary<Guid, Guid?>? decisions = null)
    {
        decisions ??= new Dictionary<Guid, Guid?>();
        Validation.Require(decisions.Keys.All(id => source.Transactions.Any(t => t.Id == id)), "Une décision concerne un mouvement absent de la source.");
        var rows = new List<EntryModelRow>();
        foreach (var t in source.Transactions.OrderBy(t => t.Sequence).ThenBy(t => t.Id))
        {
            var candidates = models.Where(m => m.Active && m.BankAccountId == bank.Id)
                .Select(m => new EntryModelCandidate(m.Id, m.Name, m.AccountCode, m.TaxRate,
                    m.Keywords.Where(k => Matches(t.Description ?? "", k)).ToArray()))
                .Where(c => c.Keywords.Count > 0).OrderBy(c => c.Id).ToArray();
            var decided = decisions.TryGetValue(t.Id, out var chosen);
            Validation.Require(!decided || chosen == null || candidates.Any(c => c.Id == chosen), "La décision doit désigner un modèle correspondant au libellé.");
            var selected = decided ? candidates.SingleOrDefault(c => c.Id == chosen) : candidates.Length == 1 ? candidates[0] : null;
            var raw = StatementAmounts.Normalize(t);
            var rawTotal = raw.Debit.HasValue != raw.Credit.HasValue ? raw.Debit ?? raw.Credit
                : !raw.Debit.HasValue && !raw.Credit.HasValue && raw.Direction is "DEBIT" or "CREDIT" ? raw.Amount : null;
            var total = rawTotal is > 0 && AccountingEngine.Money(rawTotal.Value) ? rawTotal : null;
            var status = decided && chosen == null ? "EXCLUDED" : candidates.Length == 0 ? "UNMATCHED"
                : selected == null ? "AMBIGUOUS" : total == null || t.Date == null ? "INVALID" : "MATCHED";
            var net = status == "MATCHED" ? decimal.Round(total!.Value / (1 + selected!.TaxRate / 100), 2, MidpointRounding.AwayFromZero) : (decimal?)null;
            var tax = net.HasValue ? total - net : null;
            rows.Add(new(t.Id, t.Sequence, t.Date, t.Description ?? "", status, candidates, selected?.Id, total, net, tax,
                status switch { "AMBIGUOUS" => "Plusieurs modèles correspondent : choisissez le modèle ou excluez ce mouvement.",
                    "INVALID" => "Date, montant ou convention débit/crédit à vérifier dans la source.",
                    "UNMATCHED" => "Aucun mot configuré trouvé : mouvement non retenu.", "EXCLUDED" => "Mouvement exclu explicitement.", _ => "Correspondance à confirmer." }));
        }
        return rows;
    }
}
