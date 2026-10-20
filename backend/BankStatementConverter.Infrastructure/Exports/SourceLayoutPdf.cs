using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace BankStatementConverter.Infrastructure.Exports;

public static partial class NormalizedStatementPdf
{
    // A faithful transcription is independent of the export model and of derived banking fields.
    // Never add a missing date, reference, balance, total or validation status to the source copy.
    public static byte[] GenerateSource(PdfTextDocument source)
    {
        _ = Fonts.Value;
        if (source.Pages.Count == 0)
            throw new AppException(422, "Aucune page source disponible pour reconstruire le PDF.");
        using var document = new PdfDocument();
        document.Info.Title = "Relevé bancaire - transcription structurée";
        var options = new XPdfFontOptions(PdfFontEncoding.Unicode, PdfFontEmbedding.EmbedCompleteFontFile);
        var font = new XFont("AliasNotoSans", 9, XFontStyleEx.Regular, options);
        foreach (var sourcePage in source.Pages.OrderBy(p => p.Number))
        {
            var original = StatementPdfExtractor.StructureSourcePage(sourcePage);
            if (!double.IsFinite(original.Width) || !double.IsFinite(original.Height) || original.Width <= 0 || original.Height <= 0)
                throw new AppException(422, "Dimensions de page source invalides.");
            PdfPage AddPage()
            {
                var page = document.AddPage();
                page.Width = XUnit.FromPoint(original.Width);
                page.Height = XUnit.FromPoint(original.Height);
                return page;
            }
            var boxes = original.Boxes.Where(b => !string.IsNullOrWhiteSpace(b.Text)).ToArray();
            if (boxes.Length == 0 && string.IsNullOrWhiteSpace(original.Markdown) && original.Tables?.Count is not > 0)
                throw new AppException(422, $"La page {original.Number} ne contient pas de transcription exploitable. Relancez sa lecture.");
            // AI transcripts explicitly use a full-page text container, without real coordinates.
            var positioned = boxes.Length > 0 && !(boxes.Length == 1 && boxes[0].Left == 0 && boxes[0].Top == 0
                && boxes[0].Right == original.Width && boxes[0].Bottom == original.Height);
            if (positioned)
            {
                using var graphics = XGraphics.FromPdfPage(AddPage());
                foreach (var box in boxes.OrderBy(b => b.Top).ThenBy(b => b.Left))
                {
                    if (!double.IsFinite(box.Left) || !double.IsFinite(box.Right) || !double.IsFinite(box.Top)
                        || !double.IsFinite(box.Bottom) || box.Left < 0 || box.Top < 0
                        || box.Right > original.Width || box.Bottom > original.Height || box.Right <= box.Left || box.Bottom <= box.Top)
                        throw new AppException(422, $"Position de texte invalide sur la page {original.Number}.");
                    // Old saved Paddle imports can recover the cell structure from the saved tables.
                    var table = box.Table ?? original.Tables?.FirstOrDefault(t =>
                        string.Join('\n', t.Rows.Select(r => string.Join(' ', r) + " ")).Trim() == box.Text.Trim());
                    if (table != null)
                    {
                        DrawSourceTable(graphics, box, table, options);
                        continue;
                    }
                    var lines = box.Text.Replace("\r", "").Split('\n');
                    var size = Math.Min(11, (box.Bottom - box.Top) / lines.Length * .8);
                    var cellFont = new XFont("AliasNotoSans", size, XFontStyleEx.Regular, options);
                    var width = lines.Max(line => graphics.MeasureString(line, cellFont).Width);
                    if (width > box.Right - box.Left)
                    {
                        size *= (box.Right - box.Left) / width;
                        cellFont = new XFont("AliasNotoSans", size, XFontStyleEx.Regular, options);
                    }
                    for (var index = 0; index < lines.Length; index++)
                        graphics.DrawString(lines[index], cellFont, XBrushes.Black,
                            new XPoint(box.Left, box.Top + size + index * (box.Bottom - box.Top) / lines.Length));
                }
                continue;
            }
            // Reflow coordinate-free transcriptions, keeping every printed field and table cell.
            var margin = Math.Min(28, original.Width / 12);
            var available = original.Width - margin * 2;
            XGraphics? flow = null;
            double y = 0;
            void NewPage()
            {
                flow?.Dispose();
                flow = XGraphics.FromPdfPage(AddPage());
                y = margin;
            }
            List<string> Wrap(string value, double width)
            {
                var result = new List<string>();
                foreach (var paragraph in value.Replace("\r", "").Split('\n'))
                {
                    var line = "";
                    foreach (var ch in paragraph)
                    {
                        if (line.Length > 0 && flow!.MeasureString(line + ch, font).Width > width)
                        {
                            var split = line.LastIndexOf(' ');
                            result.Add(split > 0 ? line[..split] : line);
                            line = split > 0 ? line[(split + 1)..] : "";
                        }
                        line += ch;
                    }
                    result.Add(line);
                }
                return result;
            }
            void Text(string text)
            {
                foreach (var line in Wrap(text, available))
                {
                    if (y + 13 > original.Height - margin) NewPage();
                    flow!.DrawString(line, font, XBrushes.Black, margin, y + 9);
                    y += 13;
                }
            }
            void Table(PdfStructuredTable table)
            {
                var count = table.Rows.Select(r => r.Count).DefaultIfEmpty().Max();
                if (count == 0) return;
                var header = table.Rows[0];
                var hasHeader = header.Count(cell => StatementFieldNames.Column(cell) != null) >= 2;
                var numeric = Enumerable.Range(0, count).Select(c => c < header.Count
                    && (StatementFieldNames.Column(header[c]) is "Debit" or "Credit"
                        || header[c].Contains("montant", StringComparison.OrdinalIgnoreCase)
                        || header[c].Contains("solde", StringComparison.OrdinalIgnoreCase))).ToArray();
                var weights = Enumerable.Range(0, count).Select(c => Math.Clamp(table.Rows
                    .Select(r => c < r.Count ? r[c].Split('\n').Max(s => s.Length) : 0).DefaultIfEmpty().Max(),
                        10, c < header.Count && StatementFieldNames.Column(header[c]) == "Description" ? 72 : 26)).ToArray();
                var widths = weights.Select(w => available * w / weights.Sum()).ToArray();
                var capacity = original.Height - margin * 2;
                List<string>[] Cells(IReadOnlyList<string> row) => Enumerable.Range(0, count)
                    .Select(c => Wrap(c < row.Count ? row[c] : "", Math.Max(1, widths[c] - 10))).ToArray();
                var heading = hasHeader ? Cells(header) : null;
                var headerHeight = heading == null ? 0 : heading.Max(c => c.Count) * 13 + 10;
                void Draw(List<string>[] cells, int first, int lines, bool isHeader, int rowIndex)
                {
                    var height = lines * 13 + 10;
                    double x = margin;
                    for (var c = 0; c < count; c++)
                    {
                        flow!.DrawRectangle(isHeader ? XBrushes.LightGray : rowIndex % 2 == 0 ? XBrushes.WhiteSmoke : XBrushes.White,
                            x, y, widths[c], height);
                        flow.DrawRectangle(XPens.LightGray, x, y, widths[c], height);
                        for (var line = first; line < Math.Min(first + lines, cells[c].Count); line++)
                        {
                            var text = cells[c][line];
                            var left = numeric[c] && !isHeader ? x + widths[c] - 5 - flow.MeasureString(text, font).Width : x + 5;
                            flow.DrawString(text, font, XBrushes.Black, left, y + 14 + (line - first) * 13);
                        }
                        x += widths[c];
                    }
                    y += height;
                }
                void NextTablePage()
                {
                    NewPage();
                    if (heading != null) Draw(heading, 0, heading.Max(c => c.Count), true, 0);
                }
                for (var index = 0; index < table.Rows.Count; index++)
                {
                    var cells = index == 0 && heading != null ? heading : Cells(table.Rows[index]);
                    var lines = cells.Max(c => c.Count);
                    var height = lines * 13 + 10;
                    if (height <= capacity - headerHeight && y + height > original.Height - margin)
                    {
                        if (index == 0) NewPage(); else NextTablePage();
                    }
                    for (var first = 0; first < lines;)
                    {
                        var fitting = (int)Math.Floor((original.Height - margin - y - 10) / 13);
                        if (fitting <= 0)
                        {
                            if (index == 0) NewPage(); else NextTablePage();
                            fitting = (int)Math.Floor((original.Height - margin - y - 10) / 13);
                            if (fitting <= 0) throw new AppException(422, "Le format de page ne permet pas de présenter ce tableau lisiblement.");
                        }
                        var take = Math.Min(fitting, lines - first);
                        Draw(cells, first, take, index == 0 && hasHeader, index);
                        first += take;
                        if (first < lines) NextTablePage();
                    }
                }
            }
            NewPage();
            try
            {
                var text = original.Markdown ?? string.Join('\n', boxes.Select(b => b.Text));
                // OpenRouterTranscription serializes tables as quoted TSV in reading order.
                static string Quote(string cell) => cell.IndexOfAny(['\t', '\r', '\n', '"']) < 0
                    ? cell : "\"" + cell.Replace("\"", "\"\"") + "\"";
                foreach (var table in original.Tables ?? [])
                {
                    if (boxes.Length == 0 && string.IsNullOrWhiteSpace(original.Markdown))
                    {
                        Table(table);
                        continue;
                    }
                    var tsv = string.Join('\n', table.Rows.Select(row => string.Join('\t', row.Select(Quote))));
                    var index = text.IndexOf(tsv, StringComparison.Ordinal);
                    if (tsv.Length == 0 || index < 0) continue; // Text already retains this table's transcription.
                    if (index > 0) Text(text[..index].TrimEnd('\n'));
                    Table(table);
                    text = text[(index + tsv.Length)..].TrimStart('\n');
                }
                if (text.Length > 0) Text(text);
            }
            finally { flow?.Dispose(); }
        }
        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }

    private static void DrawSourceTable(XGraphics graphics, PdfTextBox box, PdfStructuredTable table, XPdfFontOptions options)
    {
        var count = table.Rows.Select(r => r.Count).DefaultIfEmpty().Max();
        if (count == 0) return;
        var weights = Enumerable.Range(0, count).Select(c => Math.Clamp(table.Rows
            .Select(r => c < r.Count ? r[c].Length : 0).DefaultIfEmpty().Max(), 8, 40)).ToArray();
        var widths = weights.Select(w => (box.Right - box.Left) * w / weights.Sum()).ToArray();
        var size = 9d;
        List<string> Wrap(string value, double width, XFont font)
        {
            var result = new List<string>();
            foreach (var paragraph in value.Replace("\r", "").Split('\n'))
            {
                var line = "";
                foreach (var ch in paragraph)
                {
                    if (line.Length > 0 && graphics.MeasureString(line + ch, font).Width > width)
                    {
                        result.Add(line);
                        line = "";
                    }
                    line += ch;
                }
                result.Add(line);
            }
            return result;
        }
        // Fit the actual cells to the original table rectangle, without dropping or inventing cells.
        while (true)
        {
            var font = new XFont("AliasNotoSans", size, XFontStyleEx.Regular, options);
            var rows = table.Rows.Select(row => Enumerable.Range(0, count)
                .Select(c => Wrap(c < row.Count ? row[c] : "", Math.Max(1, widths[c] - size), font)).ToArray()).ToArray();
            var heights = rows.Select(row => row.Max(cell => cell.Count) * size * 1.4 + size).ToArray();
            if (heights.Sum() > box.Bottom - box.Top)
            {
                size *= .9;
                if (size < 3)
                    throw new AppException(422, "Le tableau source ne peut pas être reconstruit lisiblement. Vérifiez la lecture du document.");
                continue;
            }
            var y = box.Top;
            for (var row = 0; row < rows.Length; row++)
            {
                var x = box.Left;
                for (var column = 0; column < count; column++)
                {
                    graphics.DrawRectangle(XPens.LightGray, x, y, widths[column], heights[row]);
                    var lines = rows[row][column];
                    for (var line = 0; line < lines.Count; line++)
                        graphics.DrawString(lines[line], font, XBrushes.Black, x + size / 2, y + size * 1.4 * (line + 1));
                    x += widths[column];
                }
                y += heights[row];
            }
            return;
        }
    }
}
