using System.Globalization;
using System.Text.RegularExpressions;
using BankStatementConverter.Application.Exports;

namespace BankStatementConverter.Infrastructure.Exports;

internal static class PdfReadingComparisonBuilder
{
    internal static PdfReadingComparison Build(PdfTextPage primary, PdfTextPage secondary,
        PdfStatementExtraction? first, PdfStatementExtraction? second, string accepted)
    {
        var differences = new List<PdfFieldDifference>();
        static string? Value(object? value) => value is IFormattable formatted
            ? formatted.ToString(value is DateOnly ? "yyyy-MM-dd" : null, CultureInfo.InvariantCulture) : value?.ToString();
        static bool Missing(string? value) => string.IsNullOrWhiteSpace(value);
        void Compare(int? row, string field, object? a, object? b)
        {
            var left = Value(a);
            var right = Value(b);
            string Fold(string? s) => Regex.Replace(s?.Trim() ?? "", @"\s+", " ").ToUpperInvariant();
            if (Fold(left) == Fold(right))
                return;
            differences.Add(new(row, field, left, right, Missing(left) ? "completed" : Missing(right) ? "missing" : "conflict"));
        }
        var before = first?.Statement.Transactions ?? [];
        var after = second?.Statement.Transactions ?? [];
        if (before.Count != after.Count)
            differences.Add(new(null, "RowCount", before.Count.ToString(), after.Count.ToString(), before.Count == 0 ? "completed" : after.Count == 0 ? "missing" : "conflict"));
        else
            for (var i = 0; i < before.Count; i++)
            {
                Compare(i + 1, "Date", before[i].Date, after[i].Date);
                Compare(i + 1, "ValueDate", before[i].ValueDate, after[i].ValueDate);
                Compare(i + 1, "Description", before[i].Description, after[i].Description);
                Compare(i + 1, "Reference", before[i].Reference, after[i].Reference);
                Compare(i + 1, "Debit", before[i].Debit, after[i].Debit);
                Compare(i + 1, "Credit", before[i].Credit, after[i].Credit);
            }
        Compare(null, "OpeningBalance", first?.Statement.OpeningBalance, second?.Statement.OpeningBalance);
        Compare(null, "ClosingBalance", first?.Statement.ClosingBalance, second?.Statement.ClosingBalance);
        return new(primary.Number, primary.ReadingMethod ?? "NATIVE_PDF", secondary.ReadingMethod ?? "PADDLE_OCR",
            primary.Model, secondary.Model, accepted, differences);
    }

    internal static IReadOnlyList<ExportError> ReviewErrors(PdfTextDocument? document, IReadOnlyList<ExportError> warnings)
    {
        if (document == null || document.ReadingsReviewed)
            return [];
        var conflicts = document.Pages.Any(p => p.Comparison?.Differences.Any(d => d.Kind == "conflict") == true);
        // Preserve mandatory review for warnings stored by retired extraction engines.
        var unsupported = warnings.Any(w => w.Code.StartsWith("EXTRACTION_AI_", StringComparison.Ordinal)
            || w.Code == "EXTRACTION_ENGINE_DISAGREEMENT");
        return conflicts || unsupported
            ? [new("READING_REVIEW_REQUIRED", "Des lectures différentes ou des valeurs sans confirmation locale nécessitent une vérification. Comparez les valeurs au PDF original, corrigez-les puis confirmez la lecture affichée.")]
            : [];
    }
}
