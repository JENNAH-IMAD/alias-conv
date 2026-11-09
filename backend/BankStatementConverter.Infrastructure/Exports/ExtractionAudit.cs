using System.Text.RegularExpressions;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;

namespace BankStatementConverter.Infrastructure.Exports;

// Evidence of omitted rows is blocking. Passing these checks is not a promise of financial accuracy.
public static class ExtractionAudit
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    private const string DatePattern = @"\b\d{1,2}(?:[/.-]|[ \t]+)\d{1,2}(?:(?:[/.-]|[ \t]+)\d{2,4})?\b";
    private const string AmountPattern = @"\d[\d \u00a0\u202f.,]*[,.]\d{2}(?!\d)";

    public static int ExpectedRows(PdfTextPage page)
    {
        if (page.Tables is { Count: > 0 })
            return page.Tables.Sum(table => table.Rows.Count(row => Movement(string.Join(" ", row))));
        var lines = new List<(double Top, List<PdfTextBox> Boxes)>();
        foreach (var box in page.Boxes.OrderBy(box => box.Top).ThenBy(box => box.Left))
        {
            if (lines.Count == 0 || Math.Abs(lines[^1].Top - box.Top) > 3)
                lines.Add((box.Top, []));
            lines[^1].Boxes.Add(box);
        }
        return lines.SelectMany(line => string.Join(" ", line.Boxes.OrderBy(box => box.Left).Select(box => box.Text)).Split('\n'))
            .Count(Movement);
    }

    private static bool Movement(string text)
    {
        if (StatementFieldNames.IsBalance(text) || Regex.IsMatch(text, @"(?i)\bTOTAL\b|\bREPORT\b|\bPERIODE\b|\bPÉRIODE\b", RegexOptions.None, Timeout))
            return false;
        return Regex.IsMatch(text.TrimStart(), @"^" + DatePattern, RegexOptions.None, Timeout)
            && Regex.IsMatch(text, AmountPattern, RegexOptions.None, Timeout);
    }

    public static IReadOnlyList<ExportError> Errors(BankStatement statement, PdfTextDocument document)
    {
        var errors = new List<ExportError>();
        if (document.SourcePageCount is > 0 && !Enumerable.Range(1, document.SourcePageCount.Value)
            .SequenceEqual(document.Pages.Select(page => page.Number).Order()))
            errors.Add(new("EXTRACTION_MISSING_PAGES", "La lecture ne couvre pas toutes les pages du PDF original. Relancez le traitement complet avant validation."));
        var unreliable = document.Pages.Where(page => page.HasUnreliableText).Select(page => page.Number).ToArray();
        if (unreliable.Length > 0)
            errors.Add(new("EXTRACTION_UNREADABLE_TEXT", $"Le texte interne des pages {string.Join(", ", unreliable)} n’a pas permis une lecture bancaire fiable. Une lecture OCR du rendu visuel est nécessaire avant validation."));
        var images = document.Pages.Where(page => page.RequiresImageReading).Select(page => page.Number).ToArray();
        if (images.Length > 0)
            errors.Add(new("EXTRACTION_UNREAD_IMAGE_AREAS", $"Les pages {string.Join(", ", images)} contiennent des zones en image dont la lecture n’a pas été résolue. Le texte sélectionnable ne suffit pas à confirmer tous les mouvements. Relancez la lecture OCR avant validation."));
        var unread = document.Pages.Where(page => page.Boxes.Count == 0 && page.Tables is not { Count: > 0 }).Select(page => page.Number).ToArray();
        if (unread.Length > 0)
            errors.Add(new("EXTRACTION_UNREAD_PAGES", $"Les pages {string.Join(", ", unread)} n’ont pas de lecture exploitable. Réimportez le PDF après vérification ; l’exhaustivité ne peut pas être validée."));
        var expected = document.Pages.Sum(ExpectedRows);
        if (expected > statement.Transactions.Count)
            errors.Add(new("EXTRACTION_MISSING_ROWS", $"Au moins {expected} lignes datées avec montant sont visibles dans la lecture, mais {statement.Transactions.Count} mouvements sont présents. Vérifiez les lignes manquantes avant validation."));
        if (statement.Transactions.Any(transaction => transaction.SourcePages is { Count: > 0 }))
        {
            foreach (var page in document.Pages)
            {
                var actual = statement.Transactions.Count(transaction => transaction.SourcePages?.Contains(page.Number) == true);
                if (ExpectedRows(page) > actual)
                    errors.Add(new("EXTRACTION_PAGE_MISSING_ROWS", $"La page {page.Number} contient des lignes non reprises dans les mouvements. Vérifiez cette page avant validation."));
            }
        }
        return errors;
    }

    internal static int RowDefects(PdfStatementExtraction? extraction, PdfTextPage page)
    {
        if (extraction == null || extraction.Statement.Transactions.Count == 0)
            return ExpectedRows(page) > 0 || page.Boxes.Count < 10 ? 10000 : 0;
        var rows = extraction.Statement.Transactions;
        return Math.Max(0, ExpectedRows(page) - rows.Count) * 10 + rows.Sum(row =>
            (row.Date.HasValue ? 0 : 1) + (string.IsNullOrWhiteSpace(row.Description) ? 1 : 0)
            + (row.Debit.HasValue != row.Credit.HasValue ? 0 : 2))
            + new StatementValidator().Validate(extraction.Statement).Errors.Count
            + extraction.Warnings.Count(w => w.Code is "PDF_DATE_UNREADABLE" or "PDF_TABLE_REPEATED_TOTAL");
    }
}
