using System.Text.RegularExpressions;
using BankStatementConverter.Application.Exports;

namespace BankStatementConverter.Infrastructure.Exports;

public sealed partial class StatementPdfExtractor
{
    // Rebuild the printed table for presentation only. Never use export-model columns here.
    internal static PdfTextPage StructureSourcePage(PdfTextPage page, bool native = true)
    {
        static string Quote(string value) => value.IndexOfAny(['\t', '\r', '\n', '"']) < 0
            ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
        static string Tsv(PdfStructuredTable table) => string.Join('\n', table.Rows.Select(r => string.Join('\t', r.Select(Quote))));
        PdfTextPage Flow(string text, IReadOnlyList<PdfStructuredTable> tables) => page with
        {
            Boxes = [new(text, 0, page.Width, 0, page.Height, null)],
            Markdown = text,
            Tables = tables
        };
        var sourceTables = page.Tables ?? page.Boxes.Where(b => b.Table != null).Select(b => b.Table!).ToArray();
        if (sourceTables.Count > 0)
        {
            if (sourceTables.All(t => t.Bounds is { } b && double.IsFinite(b.Left) && double.IsFinite(b.Right)
                && double.IsFinite(b.Top) && double.IsFinite(b.Bottom) && b.Left >= 0 && b.Top >= 0
                && b.Right <= page.Width && b.Bottom <= page.Height && b.Right > b.Left && b.Bottom > b.Top))
            {
                var blocks = page.Boxes.Where(box => !sourceTables.Any(t =>
                {
                    var bounds = t.Bounds!;
                    var x = (box.Left + box.Right) / 2;
                    var y = (box.Top + box.Bottom) / 2;
                    return x >= bounds.Left && x <= bounds.Right && y >= bounds.Top && y <= bounds.Bottom;
                })).Select(box => (box.Top, box.Left, Text: box.Text))
                    .Concat(sourceTables.Select(t => (t.Bounds!.Top, t.Bounds.Left, Text: Tsv(t))))
                    .OrderBy(block => block.Top).ThenBy(block => block.Left).Select(block => block.Text);
                return Flow(string.Join("\n\n", blocks), sourceTables);
            }
            // Preserve reading order of OCR text blocks and their real cells; do not shrink tables into tiny text.
            var parts = new List<string>();
            var found = new List<PdfStructuredTable>();
            foreach (var box in page.Boxes.OrderBy(b => b.Top).ThenBy(b => b.Left))
            {
                var table = box.Table ?? sourceTables.FirstOrDefault(t =>
                    string.Join('\n', t.Rows.Select(r => string.Join(' ', r) + " ")).Trim() == box.Text.Trim());
                if (table == null)
                    parts.Add(box.Text);
                else
                {
                    parts.Add(Tsv(table));
                    found.Add(table);
                }
            }
            return found.Count == sourceTables.Count ? Flow(string.Join("\n\n", parts), found) : page;
        }
        var words = PageTokens(page, native).ToArray();
        var header = FindHeader(words);
        if (header == null)
            return page;
        var lines = Lines(words);
        var knownCaptions = new[] { header.Date, header.ValueDate, header.Reference, header.Description, header.Debit, header.Credit }
            .Where(c => c != null).Select(c => c!).OrderBy(c => c.Left).ToArray();
        // Retain additional printed columns (balance, currency, cheque number, etc.).
        // A caption outside the known captions must also have aligned data underneath it.
        var extraCaptions = words.Where(w => Math.Abs(w.Y - header.Description.Y) <= 4
            && Regex.IsMatch(w.Text, @"\p{L}", RegexOptions.None, RegexTimeout)
            && knownCaptions.All(c => w != c && (w.Right < c.Left - 12 || w.Left > c.Right + 12))
            && words.Count(data => data.Y < header.Y - 12 && Math.Abs(data.Left - w.Left) < 12) >= 2).ToArray();
        var captions = knownCaptions.Concat(extraCaptions).OrderBy(c => c.Left).ToArray();
        bool OwnsDate(Token word, Token caption) => caption == header.Date
            ? header.ValueDate == null || Math.Abs(word.Left - caption.Left) <= Math.Abs(word.Left - header.ValueDate.Left)
            : Math.Abs(word.Left - caption.Left) < Math.Abs(word.Left - header.Date.Left);
        var tableBottom = lines.LastOrDefault(l => l.Y < header.Y - 3 && StatementFieldNames.Balance(l.Text) == "ClosingBalance")?.Y - 3
            ?? page.Height * .06;
        var body = lines.Where(l => l.Y < header.Y - 3 && l.Y > tableBottom).ToArray();
        // Do not restructure a page without dated movements underneath the captions.
        var anchors = body.Where(l => l.Words.Any(w => Math.Abs(w.Left - header.Date.Left) < 28
            && OwnsDate(w, header.Date)
            && Regex.IsMatch(w.Text, @"^(?:\d{1,2}(?:[/.-]|\s+)\d{1,2}(?:(?:[/.-]|\s+)\d{2,4})?|\d{4}[-/.]\d{1,2}[-/.]\d{1,2}|\d{4}|\d{8})$", RegexOptions.None, RegexTimeout))).ToArray();
        if (anchors.Length == 0)
            return page;
        // Some scans place page numbering far below the last movement but before totals.
        // Keep that printed text outside the grid instead of appending it to a date cell.
        var pageFooters = body.SelectMany(l => l.Words).Where(w => w.Y < anchors.Min(a => a.Y) - 20
            && w.Left < header.Description.Left && Has(w.Text, @"^PAGE\b|^\d{3,}/\d{1,3}$")).ToHashSet();
        var starts = captions.Select(c => c.Left).ToArray();
        bool Identifier(Token w) => Regex.IsMatch(w.Text, @"^(?=.*\d)[\p{L}\p{N}_./-]+$", RegexOptions.None, RegexTimeout)
            && !Regex.IsMatch(w.Text, @"^(?:\d{1,2}(?:[/.-]|\s+)\d{1,2}|\d{4}[-/.]\d{1,2}[-/.]\d{1,2})", RegexOptions.None, RegexTimeout) && !Money(w.Text).HasValue;
        for (var c = 0; c < captions.Length; c++)
        {
            var caption = captions[c];
            if (caption == header.Description)
                continue;
            var aligned = (caption == header.Debit || caption == header.Credit ? body : anchors).SelectMany(l => l.Words).Where(w => Math.Abs(w.Left - caption.Left) < 28
                && (caption == header.Reference ? Identifier(w)
                    : caption == header.Date || caption == header.ValueDate ? OwnsDate(w, caption) && Regex.IsMatch(w.Text, @"^(?:\d{1,2}(?:[/.-]|\s+)\d{1,2}|\d{4}[-/.]\d{1,2}[-/.]\d{1,2}|\d{4}(?:\d{4})?$)", RegexOptions.None, RegexTimeout)
                    : Regex.IsMatch(w.Text, @"^[+\-−(]?\s*\d", RegexOptions.None, RegexTimeout))).ToArray();
            starts[c] = aligned.Length > 0 ? aligned.Min(w => w.Left) - 2 : caption.Left - 6;
        }
        var descriptionIndex = Array.IndexOf(captions, header.Description);
        if (descriptionIndex > 0)
        {
            var previous = captions[descriptionIndex - 1];
            var endings = anchors.SelectMany(l => l.Words).Where(w => Math.Abs(w.Left - previous.Left) < 28
                && (previous == header.Reference ? Identifier(w) : Regex.IsMatch(w.Text, @"^(?:\d{1,2}(?:[/.-]|\s+)\d{1,2}|\d{4}[-/.]\d{1,2}[-/.]\d{1,2})", RegexOptions.None, RegexTimeout)))
                .Select(w => w.Right).ToArray();
            if (endings.Length > 0)
                starts[descriptionIndex] = endings.Max() + 2;
        }
        var narrativeStarts = anchors.SelectMany(l => l.Words).Where(w => w.Left > starts[Math.Max(0, descriptionIndex - 1)]
            && (descriptionIndex == starts.Length - 1 || w.Left < starts[descriptionIndex + 1])
            && !Regex.IsMatch(w.Text, @"^(?:\d{1,2}(?:[/.-]|\s+)\d{1,2}|\d{4}[-/.]\d{1,2}[-/.]\d{1,2})", RegexOptions.None, RegexTimeout)
            && !Identifier(w) && (Regex.IsMatch(w.Text, @"\p{L}", RegexOptions.None, RegexTimeout) || w.Text.Contains('*')))
            .Select(w => w.Left).ToArray();
        if (narrativeStarts.Length > 0)
            starts[descriptionIndex] = narrativeStarts.Min() - 1;
        if (starts.Zip(starts.Skip(1)).Any(pair => pair.First >= pair.Second))
            return page;
        int Column(Token word)
        {
            for (var c = starts.Length - 1; c >= 0; c--)
                if (word.Left >= starts[c])
                    return c;
            return 0;
        }
        string[] Cells(IEnumerable<Line> rows)
        {
            var cells = Enumerable.Range(0, starts.Length).Select(_ => new List<string>()).ToArray();
            foreach (var row in rows)
                for (var c = 0; c < starts.Length; c++)
                {
                    var text = string.Join(' ', row.Words.Where(w => !pageFooters.Contains(w) && Column(w) == c).Select(w => w.Text));
                    if (text.Length > 0)
                        cells[c].Add(text);
                }
            return cells.Select(cell => string.Join('\n', cell)).ToArray();
        }
        var headerLines = lines.Where(l => l.Y >= header.Y - 3 && l.Y <= header.Y + 12).ToArray();
        // Captions use their printed alignment; data cells use the observed column starts.
        var headings = new string[captions.Length];
        for (var c = 0; c < captions.Length; c++)
            headings[c] = string.Join(' ', headerLines.SelectMany(l => l.Words)
                .Where(w => w.Left >= captions[c].Left - 2 && (c == captions.Length - 1 || w.Left < captions[c + 1].Left - 2))
                .Select(w => w.Text));
        for (var c = 0; c < captions.Length; c++)
        {
            // Bilingual captions can contain separate Arabic OCR artefacts. Use the actual
            // detected caption instead of abandoning the table and redrawing tiny text.
            if (extraCaptions.Contains(captions[c]) || StatementFieldNames.Column(headings[c]) == null)
                headings[c] = captions[c].Text;
            if (captions[c] == header.Date && StatementFieldNames.Column(headings[c]) != "Date")
                headings[c] = "Date " + headings[c];
        }
        var output = new List<IReadOnlyList<string>> { headings };
        // One bank movement can span several OCR baselines. Anchor physical rows on
        // their dates and balance/total labels, preserving wrapped narratives in one cell.
        var rowStarts = new List<double>();
        foreach (var row in anchors.Concat(body.Where(l => StatementFieldNames.IsBalance(l.Text)
            || Has(l.Text, @"^\s*TOTAL\b")
            || l.Words.Any(w => w.Left >= header.MoneyStart && Money(w.Text).HasValue
                && Regex.IsMatch(w.Text, @"[,.]\d{2}$", RegexOptions.None, RegexTimeout)))).OrderByDescending(l => l.Y))
            if (rowStarts.Count == 0 || rowStarts[^1] - row.Y > 5)
                rowStarts.Add(row.Y);
        var leading = body.Where(l => l.Y > rowStarts[0] + 3).ToArray();
        for (var index = 0; index < rowStarts.Count; index++)
        {
            var upper = rowStarts[index] + 3;
            var lower = index + 1 < rowStarts.Count ? rowStarts[index + 1] + 3 : double.MinValue;
            output.Add(Cells(body.Where(l => l.Y <= upper && l.Y > lower)));
        }
        var structured = new PdfStructuredTable(output);
        var before = string.Join('\n', lines.Where(l => l.Y > header.Y + 12).Concat(leading).Select(l => l.Text));
        var after = string.Join('\n', Lines(pageFooters).Concat(lines.Where(l => l.Y <= tableBottom && l.Y < header.Y - 3)).Select(l => l.Text));
        return Flow(string.Join("\n\n", new[] { before, Tsv(structured), after }.Where(s => s.Length > 0)), [structured]);
    }
}
