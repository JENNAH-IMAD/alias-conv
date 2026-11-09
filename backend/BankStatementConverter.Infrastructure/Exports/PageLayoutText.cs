using System.Text;
using BankStatementConverter.Application.Exports;

namespace BankStatementConverter.Infrastructure.Exports;

internal static class PageLayoutText
{
    // Keep columns apart: identical values in different cells are not duplicates.
    public static string Generate(PdfTextPage page)
    {
        var rows = new List<List<PdfTextBox>>();
        foreach (var box in page.Boxes.Where(box => !string.IsNullOrWhiteSpace(box.Text)).OrderBy(box => (box.Top + box.Bottom) / 2).ThenBy(box => box.Left))
        {
            var center = (box.Top + box.Bottom) / 2;
            var previous = rows.LastOrDefault();
            if (previous == null || Math.Abs((previous[0].Top + previous[0].Bottom) / 2 - center) > Math.Max(2.6, Math.Min(previous[0].Bottom - previous[0].Top, box.Bottom - box.Top) * 0.55))
                rows.Add(previous = []);
            if (!previous.Any(other => other.Text == box.Text && Math.Abs(other.Left - box.Left) < 1 && Math.Abs(other.Right - box.Right) < 1))
                previous.Add(box);
        }
        var text = new StringBuilder();
        foreach (var row in rows)
        {
            text.AppendLine(string.Join("\t", row.OrderBy(box => box.Left)
                .Select(box => box.Text.Replace("\0", "", StringComparison.Ordinal))));
        }
        return text.ToString();
    }
}
