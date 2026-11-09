using System.Text.RegularExpressions;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;

namespace BankStatementConverter.Infrastructure.Exports;

// OCR can split a single bilingual caption into two physical cells. Only an explicit
// adjacent Arabic translation is coalesced; unrelated columns keep their positions.
internal static class BilingualStatementTables
{
    internal static PdfStructuredTable Coalesce(PdfStructuredTable table, List<ExportError> warnings)
    {
        var header = table.Rows.FirstOrDefault(row => StructuredStatementNormalizer.Header(row) != null);
        if (header == null)
            return table;
        var groups = new List<(int First, int? Second, string? Field)>();
        for (var c = 0; c < header.Count; c++)
        {
            var field = StatementFieldNames.Column(header[c]);
            var translated = field != null && c + 1 < header.Count
                && Regex.IsMatch(header[c + 1], @"\p{IsArabic}")
                && !Regex.IsMatch(header[c + 1], @"[A-Za-z0-9]");
            groups.Add((c, translated ? c + 1 : null, field));
            if (translated)
                c++;
        }
        if (groups.All(g => !g.Second.HasValue))
            return table;
        var totalValues = table.Rows.Where(row => row.Any(cell => Regex.IsMatch(cell, @"(?i)\bTOTAL\b")))
            .SelectMany(row => row).Select(cell => StatementValueParser.Money(cell)).Where(n => n.HasValue).ToHashSet();
        var repeated = groups.Where(g => g.Field == "Description" && g.Second.HasValue).Select(g => g.Second!.Value)
            .Where(c => table.Rows.Count(r => c < r.Count && StatementValueParser.Money(r[c]) is { } n && totalValues.Contains(n)) >= 2).ToHashSet();
        var rows = new List<IReadOnlyList<string>>();
        foreach (var row in table.Rows)
        {
            var cells = new List<string>();
            foreach (var group in groups)
            {
                var a = group.First < row.Count ? row[group.First].Trim() : "";
                var b = group.Second is { } c && c < row.Count ? row[c].Trim() : "";
                if (a == b || string.IsNullOrWhiteSpace(b))
                {
                    cells.Add(a);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(a))
                {
                    cells.Add(b);
                    continue;
                }
                if (group.Field == "Description" && group.Second is { } column && repeated.Contains(column)
                    && StatementValueParser.Money(b) is { } amount && totalValues.Contains(amount))
                {
                    if (!warnings.Any(w => w.Code == "PDF_TABLE_REPEATED_TOTAL"))
                        warnings.Add(new("PDF_TABLE_REPEATED_TOTAL", "L’OCR a répété un total dans plusieurs libellés. Ces répétitions sont écartées des libellés ; comparez les lignes au PDF original.", SourceField: "Description"));
                    cells.Add(a);
                    continue;
                }
                var first = StatementValueParser.Money(a);
                var second = StatementValueParser.Money(b);
                if (group.Field is "Debit" or "Credit" && (first.HasValue || second.HasValue))
                {
                    if (first.HasValue && first == second)
                    {
                        cells.Add(a);
                        continue;
                    }
                    throw new AppException(422, "Deux montants différents apparaissent sous un même en-tête bilingue. Relancez la lecture de cette page ou corrigez ses colonnes.");
                }
                cells.Add(a + "\n" + b);
            }
            rows.Add(cells);
        }
        if (!warnings.Any(w => w.Code == "PDF_BILINGUAL_COLUMNS"))
            warnings.Add(new("PDF_BILINGUAL_COLUMNS", "Les traductions d’en-têtes ont été regroupées avec leurs colonnes bancaires. Vérifiez les montants et les libellés sur le PDF original."));
        return new(rows);
    }
}
