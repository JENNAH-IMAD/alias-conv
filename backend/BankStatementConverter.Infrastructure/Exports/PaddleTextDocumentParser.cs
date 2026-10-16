using System.Text.Json;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;

namespace BankStatementConverter.Infrastructure.Exports;

// PP-OCRv6 returns recognized text polygons, rather than VL's HTML table cells.
public static class PaddleTextDocumentParser
{
    public static PdfTextDocument Parse(string jsonl, PdfTextDocument source)
    {
        var pages = new List<PdfTextPage>();
        foreach (var line in jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            using var json = JsonDocument.Parse(line);
            var result = json.RootElement.GetProperty("result");
            var localIndex = 0;
            foreach (var page in result.GetProperty("ocrResults").EnumerateArray())
            {
                if (pages.Count >= source.Pages.Count)
                    throw PaddleDocumentParser.Invalid();
                var original = source.Pages[pages.Count];
                var pruned = page.GetProperty("prunedResult");
                // Provider dimensions are required; text extents cannot determine the page scale.
                var dimensions = pruned.TryGetProperty("width", out _) ? pruned
                    : result.GetProperty("dataInfo").GetProperty("pages")[localIndex];
                var width = dimensions.GetProperty("width").GetDouble();
                var height = dimensions.GetProperty("height").GetDouble();
                if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
                    throw PaddleDocumentParser.Invalid();
                var texts = pruned.GetProperty("rec_texts");
                var scores = pruned.GetProperty("rec_scores");
                var polys = pruned.GetProperty("rec_polys");
                if (texts.GetArrayLength() > 100000 || texts.GetArrayLength() != scores.GetArrayLength() || texts.GetArrayLength() != polys.GetArrayLength())
                    throw PaddleDocumentParser.Invalid();
                var boxes = new List<PdfTextBox>();
                for (var i = 0; i < texts.GetArrayLength(); i++)
                {
                    var text = texts[i].GetString() ?? throw PaddleDocumentParser.Invalid();
                    var score = scores[i].GetDouble();
                    var points = polys[i].EnumerateArray().Select(p => p.EnumerateArray().Select(v => v.GetDouble()).ToArray()).ToArray();
                    if (text.Length > 10000 || !double.IsFinite(score) || score is < 0 or > 1 || points.Length != 4
                        || points.Any(p => p.Length != 2 || p.Any(v => !double.IsFinite(v))))
                        throw PaddleDocumentParser.Invalid();
                    if (string.IsNullOrWhiteSpace(text))
                        continue;
                    var left = points.Min(p => p[0]);
                    var right = points.Max(p => p[0]);
                    var top = points.Min(p => p[1]);
                    var bottom = points.Max(p => p[1]);
                    if (left < 0 || top < 0 || right > width || bottom > height)
                        throw PaddleDocumentParser.Invalid();
                    boxes.Add(new(text, left * original.Width / width, right * original.Width / width,
                        top * original.Height / height, bottom * original.Height / height, score));
                }
                if (pages.Sum(p => p.Boxes.Count) + boxes.Count > 500000)
                    throw PaddleDocumentParser.Invalid();
                pages.Add(new(original.Number, original.Width, original.Height, boxes, RawResult: page.Clone()));
                localIndex++;
            }
        }
        if (pages.Count != source.Pages.Count || pages.Count == 0)
            throw new AppException(502, "PaddleOCR n’a pas renvoyé toutes les pages du PDF. Aucun import partiel n’a été enregistré.");
        return new("PADDLE_OCR_V6", pages);
    }
}
