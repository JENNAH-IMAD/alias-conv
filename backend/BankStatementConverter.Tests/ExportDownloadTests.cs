using System.Text;
using BankStatementConverter.Infrastructure;
using Xunit;

namespace BankStatementConverter.Tests;
public class ExportDownloadTests
{
    [Theory]
    [InlineData("utf-8", ";", "\r\n")]
    [InlineData("windows-1252", ";", "\n")]
    [InlineData("utf-8", "\t", "\r\n")]
    public void RemovesOnlyHeaderAndPreservesEveryDataByte(string encoding, string delimiter, string newline)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var codec = Encoding.GetEncoding(encoding);
        var header = string.Join(delimiter, "Date opération", "Date valeur", "Libellé", "Montant débit", "Montant crédit") + newline;
        var data = string.Join(delimiter, "01/01/2026", "31/12/2025", "\"Échéance; été\nseconde ligne\"", "165,10", "0,00") + newline;
        using var stream = new MemoryStream(codec.GetBytes(header + data));
        Assert.Equal(codec.GetBytes(data), ExportDownload.WithoutHeader(stream, encoding, delimiter, 1));
    }
    [Fact]
    public void HeaderlessExportKeepsFirstOperationEvenWhenFirstColumnIsNotADate()
    {
        var bytes = Encoding.UTF8.GetBytes("BQ;001234;01/01/2026;1250,50\r\nBQ;001234;02/01/2026;0,00\r\n");
        using var stream = new MemoryStream(bytes);
        Assert.Equal(bytes, ExportDownload.WithoutHeader(stream, "utf-8", ";", 2));
    }
    [Fact]
    public void BomAndQuotedMultilineHeaderArePreservedCorrectly()
    {
        var bom = Encoding.UTF8.GetPreamble();
        var data = Encoding.UTF8.GetBytes("00123;0,00\r\n");
        using var stream = new MemoryStream(bom.Concat(Encoding.UTF8.GetBytes("\"Compte\nclient\";Montant\r\n")).Concat(data).ToArray());
        Assert.Equal(bom.Concat(data).ToArray(), ExportDownload.WithoutHeader(stream, "utf-8", ";", 1));
    }
}
