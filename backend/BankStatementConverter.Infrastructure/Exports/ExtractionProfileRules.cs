using System.Globalization;
using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
namespace BankStatementConverter.Infrastructure.Exports;

public static class ExtractionProfileRules
{
    public static readonly Dictionary<string, string> Labels = new() { ["Date"] = "Date opération", ["ValueDate"] = "Date valeur", ["Description"] = "Libellé", ["Debit"] = "Débit", ["Credit"] = "Crédit", ["OpeningBalance"] = "Solde initial", ["ClosingBalance"] = "Solde final" };
    public static string Fold(string text) => new string(text.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).Select(char.ToUpperInvariant).ToArray()).Trim();
    public static void ValidateAliases(Dictionary<string, string[]> aliases)
    {
        Validation.Require(aliases != null && aliases.Count <= Labels.Count, "Liste de synonymes invalide.");
        var used = new HashSet<string>();
        foreach (var (field, terms) in aliases!)
        {
            Validation.Require(Labels.ContainsKey(field) && terms != null && terms.Length <= 50, "Champ ou nombre de synonymes invalide.");
            foreach (var term in terms!)
            {
                Validation.Require(!string.IsNullOrWhiteSpace(term) && term.Length <= 100, "Synonyme vide ou trop long.");
                Validation.Require(used.Add(Fold(term)), "Un synonyme ne peut désigner plusieurs champs.");
                var standard = StatementFieldNames.Column(term);
                Validation.Require(standard == null || standard == field, "Ce synonyme désigne déjà un autre champ standard.");
                var balance = StatementFieldNames.Balance(term);
                Validation.Require(balance == null || balance == field, "Ce synonyme désigne déjà un autre solde standard.");
            }
        }
    }
    public static PdfTextDocument ApplyAliases(PdfTextDocument raw, Dictionary<string, string[]> aliases)
    {
        ValidateAliases(aliases);
        string Balance(string text)
        {
            foreach (var field in new[] { "OpeningBalance", "ClosingBalance" })
                foreach (var term in aliases.GetValueOrDefault(field) ?? [])
                    if (Fold(text).StartsWith(Fold(term) + " ", StringComparison.Ordinal) || Fold(text) == Fold(term))
                        return Labels[field] + text[term.Length..];
            return text;
        }
        string Caption(string text)
        {
            foreach (var (field, terms) in aliases.Where(x => x.Key is not "OpeningBalance" and not "ClosingBalance"))
                if (terms.Any(term => Fold(term) == Fold(text)))
                    return Labels[field];
            return text;
        }
        IReadOnlyList<PdfTextBox> Boxes(PdfTextPage page)
        {
            var replacements = new Dictionary<PdfTextBox, string>();
            var removed = new HashSet<PdfTextBox>();
            foreach (var group in page.Boxes.GroupBy(box => Math.Round(box.Top / 3)))
            {
                var line = group.OrderBy(box => box.Left).ToList();
                foreach (var field in new[] { "OpeningBalance", "ClosingBalance" })
                    foreach (var term in aliases.GetValueOrDefault(field) ?? [])
                        for (var count = 1; count <= Math.Min(line.Count, 10); count++)
                            if (Fold(string.Join(" ", line.Take(count).Select(box => box.Text))) == Fold(term))
                            {
                                replacements[line[0]] = Labels[field];
                                foreach (var box in line.Skip(1).Take(count - 1))
                                    removed.Add(box);
                                break;
                            }
            }
            return page.Boxes.Where(box => !removed.Contains(box)).Select(box => box with { Text = replacements.GetValueOrDefault(box) ?? Balance(box.Text) }).ToList();
        }
        return raw with
        {
            Pages = raw.Pages.Select(page => page with
            {
                Boxes = Boxes(page),
                Tables = page.Tables?.Select(table => table with
                {
                    Rows = table.Rows.Select(row =>
                    {
                        var isHeader = row.Count(cell => StatementFieldNames.Column(Caption(cell)) != null) >= 3 && !row.Any(cell => StatementValueParser.Money(cell).HasValue);
                        return (IReadOnlyList<string>)row.Select(cell => isHeader ? Caption(cell) : Balance(cell)).ToList();
                    }).ToList()
                }).ToList()
            }).ToList()
        };
    }
}
