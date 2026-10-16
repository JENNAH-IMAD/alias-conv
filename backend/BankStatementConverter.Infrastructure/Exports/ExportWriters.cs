using System.Text;
using BankStatementConverter.Application.Exports;
namespace BankStatementConverter.Infrastructure.Exports;

// Both formats use explicit delimited-field escaping. No implicit Sage record layout.
internal static class EscapedDelimitedWriter
{
    public static string Write(IReadOnlyList<string> columns, IReadOnlyList<string[]> rows, string delimiter, bool header)
    {
        if (delimiter is not (";" or "," or "\t" or "|" or " "))
            throw new ArgumentException("Séparateur invalide.");
        var text = new StringBuilder();
        void Row(IReadOnlyList<string> cells)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                if (i > 0)
                    text.Append(delimiter);
                var cell = cells[i];
                var quote = cell.Contains(delimiter, StringComparison.Ordinal) || cell.IndexOfAny(['"', '\r', '\n']) >= 0;
                if (quote)
                    text.Append('"');
                foreach (var character in cell)
                {
                    if (character == '"')
                        text.Append('"');
                    text.Append(character);
                }
                if (quote)
                    text.Append('"');
            }
            text.Append("\r\n");
        }
        if (header)
            Row(columns);
        foreach (var row in rows)
            Row(row);
        return text.ToString();
    }
}
public sealed class CsvExportWriter : IExportWriter
{
    public string Write(IReadOnlyList<string> columns, IReadOnlyList<string[]> rows, string delimiter, bool includeHeader)
        => EscapedDelimitedWriter.Write(columns, rows, delimiter, includeHeader);
}
public sealed class DelimitedTextExportWriter : IExportWriter
{
    public string Write(IReadOnlyList<string> columns, IReadOnlyList<string[]> rows, string delimiter, bool includeHeader)
        => EscapedDelimitedWriter.Write(columns, rows, delimiter, includeHeader);
}
