using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace BankStatementConverter.Infrastructure;

public static class ExportDownload
{
    // Stored archives retain their metadata row for the review screen. Downloads contain operations only.
    // Use the recorded operation count, never a date/text heuristic that could remove a real operation.
    public static byte[] WithoutHeader(Stream source, string encoding, string delimiter, int transactionCount)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        var bytes = buffer.ToArray();
        buffer.Position = 0;
        using var reader = new StreamReader(buffer, Encoding.GetEncoding(encoding), true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) {
            HasHeaderRecord = false, IgnoreBlankLines = false, WhiteSpaceChars = [], Delimiter = delimiter
        });
        int records = 0;
        string firstRecord = "";
        while (csv.Read()) { if (records++ == 0) firstRecord = csv.Parser.RawRecord; }
        if (records != (long)transactionCount + 1 || records == 0) return bytes;
        var actualEncoding = reader.CurrentEncoding;
        var preamble = actualEncoding.GetPreamble();
        var bomLength = preamble.Length > 0 && bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        var offset = bomLength + actualEncoding.GetByteCount(firstRecord);
        // Copy the original bytes verbatim: amounts, dates, quoting, separators and line endings stay identical.
        return bytes[..bomLength].Concat(bytes[offset..]).ToArray();
    }
}
