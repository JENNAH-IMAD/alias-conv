using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;

namespace BankStatementConverter.Infrastructure;

public static class ExcelDownload
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Create(Stream source, string encoding, string delimiter)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = new StreamReader(source, Encoding.GetEncoding(encoding), true, 4096, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false, WhiteSpaceChars = [], Delimiter = delimiter });
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Operations");
        int row = 0, columns = 0;
        while (csv.Read())
        {
            var record = csv.Parser.Record!;
            Validation.Require(++row <= 100000 && record.Length <= 50, "Export Excel trop volumineux.");
            columns = Math.Max(columns, record.Length);
            for (int i = 0; i < record.Length; i++)
            {
                var cell = sheet.Cell(row, i + 1);
                var value = record[i];
                // Preserve account numbers, references and formula-like labels as literal text.
                cell.Value = value;
                if (Regex.IsMatch(value, @"^-?\d+[.,]\d{2}$") && decimal.TryParse(value.Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount))
                { cell.Value = (double)amount; cell.Style.NumberFormat.Format = "#,##0.00"; }
                else if (DateTime.TryParseExact(value, new[] { "yyyy-MM-dd", "dd/MM/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                { cell.Value = date; cell.Style.DateFormat.Format = "dd/mm/yyyy"; }
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            }
        }
        if (row > 0)
        {
            // Fixed bounds avoid a dependency on installed fonts in Linux containers.
            for (int col = 1; col <= columns; col++)
            {
                var longest = sheet.Column(col).CellsUsed().Max(c => c.GetString().Length);
                sheet.Column(col).Width = Math.Clamp(longest + 3, 16, 70);
                sheet.Column(col).Style.Alignment.WrapText = true;
            }
            for (int r = 1; r <= row; r++)
                sheet.Row(r).Height = Math.Max(22, sheet.Row(r).CellsUsed().Max(c => Math.Ceiling(c.GetString().Length / Math.Max(1, sheet.Column(c.Address.ColumnNumber).Width - 3))) * 16 + 6);
        }
        using var output = new MemoryStream();
        book.SaveAs(output);
        return output.ToArray();
    }
}

