using System.Text;
using BankStatementConverter.Infrastructure;
using ClosedXML.Excel;
using Xunit;

namespace BankStatementConverter.Tests;

public class ExcelDownloadTests
{
    [Theory]
    [InlineData("SAGE100")]
    [InlineData("SAGEX3")]
    [InlineData("CUSTOMCSV")]
    public void ExcelPreservesColumnsAccentsAndLiteralValues(string type)
    {
        var template = Seed.DefaultExport(type);
        template.Fields = [new() { FieldName = "Date", SourceField = "Date", Position = 0 }, new() { FieldName = "Libellé", SourceField = "Description", Position = 1 }, new() { FieldName = "Débit", SourceField = "Debit", Position = 2 }, new() { FieldName = "Compte", SourceField = "Account", Position = 3 }];
        TransactionExporter exporter = type switch { "SAGE100" => new Sage100Exporter(), "SAGEX3" => new SageX3Exporter(), _ => new CustomCsvExporter() };
        var raw = exporter.Export([new() { Date = new(2026, 6, 2), Description = "=Échéance; \"été\"", Debit = 1280.50m, Account = "001234" }], template);
        using var archived = new MemoryStream(raw);
        using var source = new MemoryStream(ExportDownload.WithoutHeader(archived, template.Encoding, template.Delimiter, 1));
        using var xlsx = new MemoryStream(ExcelDownload.Create(source, template.Encoding, template.Delimiter));
        using var workbook = new XLWorkbook(xlsx);
        var sheet = workbook.Worksheet(1);
        Assert.Equal(4, sheet.LastColumnUsed()!.ColumnNumber());
        Assert.Equal(1, sheet.LastRowUsed()!.RowNumber());

        Assert.Equal("=Échéance; \"été\"", sheet.Cell(1, 2).GetString());
        Assert.False(sheet.Cell(1, 2).HasFormula);
        Assert.Equal(1280.50m, sheet.Cell(1, 3).GetValue<decimal>());
        Assert.Equal("001234", sheet.Cell(1, 4).GetString());
        Assert.Equal(new DateTime(2026, 6, 2), sheet.Cell(1, 1).GetDateTime());
        Assert.False(sheet.AutoFilter.IsEnabled);
    }
}

