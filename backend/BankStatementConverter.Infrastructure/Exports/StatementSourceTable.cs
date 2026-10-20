using System.Text.RegularExpressions;
using BankStatementConverter.Application.Exports;

namespace BankStatementConverter.Infrastructure.Exports;

public sealed partial class StatementPdfExtractor
{
    // Rebuild the printed table for presentation only. Never use export-model columns here.
    internal static PdfTextPage StructureSourcePage(PdfTextPage page)
    {
        static string Quote(string value) => value.IndexOfAny(['\t', '\r', '\n', '"']) < 0
            ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
        static string Tsv(PdfStructuredTable table) => string.Join('\n', table.Rows.Select(r => string.Join('\t', r.Select(Quote))));
        PdfTextPage Flow(string text, IReadOnlyList<PdfStructuredTable> tables) => page with
        {
            Boxes = [new(text, 0, page.Width, 0, page.Height, null)], Markdown = text, Tables = tables
        };
        var sourceTables = page.Tables ?? page.Boxes.Where(b => b.Table != null).Select(b => b.Table!).ToArray();
        if (sourceTables.Count > 0)
        {
            // Preserve reading order of OCR text blocks and their real cells; do not shrink tables into tiny text.
            var parts = new List<string>();
            var found = new List<PdfStructuredTable>();
            foreach (var box in page.Boxes.OrderBy(b => b.Top).ThenBy(b => b.Left))
            {
                var table = box.Table ?? sourceTables.FirstOrDefault(t =>
                    string.Join('\n', t.Rows.Select(r => string.Join(' ', r) + " ")).Trim() == box.Text.Trim());
                if (table == null) parts.Add(box.Text);
                else { parts.Add(Tsv(table)); found.Add(table); }
            }
            return found.Count == sourceTables.Count ? Flow(string.Join("\n\n", parts), found) : page;
        }
        var words = PageTokens(page, true).ToArray();
        var header = FindHeader(words);
        if (header == null) return page;
        var captions = new[] { header.Date, header.ValueDate, header.Reference, header.Description, header.Debit, header.Credit }
            .Where(c => c != null).Select(c => c!).OrderBy(c => c.Left).ToArray();
        var lines = Lines(words);
        var body = lines.Where(l => l.Y < header.Y - 12 && l.Y > page.Height * .06).ToArray();
        // Do not restructure a page without dated movements underneath the captions.
        var anchors = body.Where(l => l.Words.Any(w => Math.Abs(w.Left - header.Date.Left) < 28
            && Regex.IsMatch(w.Text, @"^\d{2}[/.-]\d{2}(?:[/.-]\d{2,4})?$", RegexOptions.None, RegexTimeout))).ToArray();
        if (anchors.Length == 0) return page;
        var starts = captions.Select(c => c.Left).ToArray();
        bool Identifier(Token w) => Regex.IsMatch(w.Text, @"^(?=.*\d)[\p{L}\p{N}_./-]+$", RegexOptions.None, RegexTimeout)
            && !Regex.IsMatch(w.Text, @"^\d{2}[/.-]\d{2}", RegexOptions.None, RegexTimeout) && !Money(w.Text).HasValue;
        for (var c = 0; c < captions.Length; c++)
        {
            var caption = captions[c];
            if (caption == header.Description) continue;
            var aligned = anchors.SelectMany(l => l.Words).Where(w => Math.Abs(w.Left - caption.Left) < 28
                && (caption == header.Reference ? Identifier(w)
                    : caption == header.Date || caption == header.ValueDate ? Regex.IsMatch(w.Text, @"^\d{2}[/.-]\d{2}", RegexOptions.None, RegexTimeout)
                    : Regex.IsMatch(w.Text, @"^[+\-−(]?\s*\d", RegexOptions.None, RegexTimeout))).ToArray();
            starts[c] = aligned.Length > 0 ? aligned.Min(w => w.Left) - 2 : caption.Left - 6;
        }
        var descriptionIndex = Array.IndexOf(captions, header.Description);
        if (descriptionIndex > 0)
        {
            var previous = captions[descriptionIndex - 1];
            var endings = anchors.SelectMany(l => l.Words).Where(w => Math.Abs(w.Left - previous.Left) < 28
                && (previous == header.Reference ? Identifier(w) : Regex.IsMatch(w.Text, @"^\d{2}[/.-]\d{2}", RegexOptions.None, RegexTimeout)))
                .Select(w => w.Right).ToArray();
            if (endings.Length > 0) starts[descriptionIndex] = endings.Max() + 2;
        }
        var narrativeStarts = anchors.SelectMany(l => l.Words).Where(w => w.Left > starts[Math.Max(0, descriptionIndex - 1)]
            && (descriptionIndex == starts.Length - 1 || w.Left < starts[descriptionIndex + 1])
            && !Regex.IsMatch(w.Text, @"^\d{2}[/.-]\d{2}", RegexOptions.None, RegexTimeout)
            && !Identifier(w) && (Regex.IsMatch(w.Text, @"\p{L}", RegexOptions.None, RegexTimeout) || w.Text.Contains('*')))
            .Select(w => w.Left).ToArray();
        if (narrativeStarts.Length > 0) starts[descriptionIndex] = narrativeStarts.Min() - 1;
        if (starts.Zip(starts.Skip(1)).Any(pair => pair.First >= pair.Second)) return page;
        int Column(Token word)
        {
            for (var c = starts.Length - 1; c >= 0; c--)
                if (word.Left >= starts[c]) return c;
            return 0;
        }
        string[] Cells(IEnumerable<Line> rows)
        {
            var cells = Enumerable.Range(0, starts.Length).Select(_ => new List<string>()).ToArray();
            foreach (var row in rows)
                for (var c = 0; c < starts.Length; c++)
                {
                    var text = string.Join(' ', row.Words.Where(w => Column(w) == c).Select(w => w.Text));
                    if (text.Length > 0) cells[c].Add(text);
                }
            return cells.Select(cell => string.Join('\n', cell)).ToArray();
        }
        var headerLines = lines.Where(l => Math.Abs(l.Y - header.Y) <= 12).ToArray();
        // Captions use their printed alignment; data cells use the observed column starts.
        var headings = new string[captions.Length];
        for (var c = 0; c < captions.Length; c++)
            headings[c] = string.Join(' ', headerLines.SelectMany(l => l.Words)
                .Where(w => w.Left >= captions[c].Left - 2 && (c == captions.Length - 1 || w.Left < captions[c + 1].Left - 2))
                .Select(w => w.Text));
        // An additional/unknown heading must not be merged into one of our banking fields.
        // Keep the original layout rather than silently reducing the source column count.
        if (headings.Any(heading => StatementFieldNames.Column(heading) == null)) return page;
        var output = new List<IReadOnlyList<string>> { headings };
        foreach (var row in body)
        {
            var cells = Cells([row]);
            var continuation = output.Count > 1 && cells.Where((_, c) => c != descriptionIndex).All(string.IsNullOrEmpty);
            if (continuation)
            {
                var previous = output[^1].ToArray();
                previous[descriptionIndex] += "\n" + cells[descriptionIndex];
                output[^1] = previous;
            }
            else output.Add(cells);
        }
        var structured = new PdfStructuredTable(output);
        var before = string.Join('\n', lines.Where(l => l.Y > header.Y + 12).Select(l => l.Text));
        var after = string.Join('\n', lines.Where(l => l.Y <= page.Height * .06 && l.Y < header.Y - 12).Select(l => l.Text));
        return Flow(string.Join("\n\n", new[] { before, Tsv(structured), after }.Where(s => s.Length > 0)), [structured]);
    }
}
