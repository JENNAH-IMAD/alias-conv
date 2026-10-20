using System.Text;
using System.Text.Json;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using HtmlAgilityPack;

namespace BankStatementConverter.Infrastructure.Exports;

// Keep the provider result intact. Tables are decoded as text, never executed as HTML.
public static class PaddleDocumentParser
{
    public static PdfTextDocument Parse(string jsonl, PdfTextDocument source)
    {
        var pages = new List<PdfTextPage>();
        foreach (var line in jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            using var json = JsonDocument.Parse(line);
            var result = json.RootElement.GetProperty("result");
            foreach (var page in result.GetProperty("layoutParsingResults").EnumerateArray())
            {
                if (pages.Count >= source.Pages.Count)
                    throw Invalid();
                var original = source.Pages[pages.Count];
                var pruned = page.GetProperty("prunedResult");
                var width = pruned.GetProperty("width").GetDouble();
                var height = pruned.GetProperty("height").GetDouble();
                if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
                    throw Invalid();
                var boxes = new List<PdfTextBox>();
                var tables = new List<PdfStructuredTable>();
                foreach (var block in pruned.GetProperty("parsing_res_list").EnumerateArray())
                {
                    var content = block.GetProperty("block_content").GetString() ?? "";
                    if (content.Length > 2000000 || boxes.Count >= 100000)
                        throw Invalid();
                    var bbox = block.GetProperty("block_bbox").EnumerateArray().Select(v => v.GetDouble()).ToArray();
                    if (bbox.Length != 4 || bbox.Any(v => !double.IsFinite(v)) || bbox[0] > bbox[2] || bbox[1] > bbox[3])
                        throw Invalid();
                    // A block box is not a cell box. Stage 2 uses the actual table cells below.
                    var blockTables = Tables(content);
                    boxes.Add(new(PlainText(content), bbox[0] * original.Width / width, bbox[2] * original.Width / width,
                        bbox[1] * original.Height / height, bbox[3] * original.Height / height, null,
                        blockTables.Count == 1 ? blockTables[0] : null));
                    tables.AddRange(blockTables);
                }
                var markdown = page.TryGetProperty("markdown", out var md) && md.TryGetProperty("text", out var text) ? text.GetString() : null;
                if (markdown?.Length > 8000000)
                    throw Invalid();
                if (tables.Count == 0 && markdown != null)
                    tables.AddRange(Tables(markdown));
                pages.Add(new(original.Number, original.Width, original.Height, boxes, markdown, tables, page.Clone()));
            }
        }
        if (pages.Count != source.Pages.Count || pages.Count == 0)
            throw new AppException(502, "PaddleOCR n’a pas renvoyé toutes les pages du PDF. Aucun import partiel n’a été enregistré.");
        return new("PADDLE_OCR_VL_1_6", pages);
    }

    public static string PlainText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html.Replace(@"\n", "\n"));
        var builder = new StringBuilder();
        void Visit(HtmlNode node)
        {
            if (node.Name is "script" or "style")
                return;
            if (node.NodeType == HtmlNodeType.Text)
                builder.Append(HtmlEntity.DeEntitize(node.InnerText));
            else if (node.Name == "br")
                builder.Append('\n');
            else
            {
                foreach (var child in node.ChildNodes)
                    Visit(child);
                if (node.Name is "tr" or "p" or "div")
                    builder.Append('\n');
                else if (node.Name is "td" or "th")
                    builder.Append(' ');
            }
        }
        Visit(doc.DocumentNode);
        return builder.ToString().Replace('\u00a0', ' ').Trim();
    }

    public static IReadOnlyList<PdfStructuredTable> Tables(string html)
    {
        if (!html.Contains("<table", StringComparison.OrdinalIgnoreCase))
            return [];
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var results = new List<PdfStructuredTable>();
        foreach (var table in doc.DocumentNode.SelectNodes("//table") ?? new HtmlNodeCollection(null))
        {
            var rows = new List<IReadOnlyList<string>>();
            var occupied = new Dictionary<(int Row, int Column), string>();
            foreach (var tr in table.SelectNodes(".//tr") ?? new HtmlNodeCollection(null))
            {
                if (tr.Ancestors("table").FirstOrDefault() != table)
                    continue;
                var row = rows.Count;
                var column = 0;
                foreach (var cell in tr.ChildNodes.Where(n => n.Name is "td" or "th"))
                {
                    while (occupied.ContainsKey((row, column)))
                        column++;
                    var rowspan = cell.GetAttributeValue("rowspan", 1);
                    var colspan = cell.GetAttributeValue("colspan", 1);
                    if (rowspan is < 1 or > 200 || colspan is < 1 or > 64 || column + colspan > 64)
                        throw Invalid();
                    var value = PlainText(cell.InnerHtml);
                    for (var r = 0; r < rowspan; r++)
                        for (var c = 0; c < colspan; c++)
                            if (!occupied.TryAdd((row + r, column + c), value))
                                throw Invalid();
                    column += colspan;
                }
                var count = occupied.Keys.Where(k => k.Row == row).Select(k => k.Column + 1).DefaultIfEmpty().Max();
                rows.Add(Enumerable.Range(0, count).Select(c => occupied.GetValueOrDefault((row, c), "")).ToList());
                foreach (var key in occupied.Keys.Where(k => k.Row == row).ToList())
                    occupied.Remove(key);
            }
            if (rows.Count > 0)
                results.Add(new(rows));
        }
        return results;
    }
    internal static AppException Invalid() => new(502, "La réponse structurée de PaddleOCR est invalide.");
}
