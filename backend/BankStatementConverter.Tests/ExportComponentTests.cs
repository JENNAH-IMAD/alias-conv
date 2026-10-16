using System.Text;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.VisualBasic.FileIO;
using Xunit;
namespace BankStatementConverter.Tests;

public class ExportSourceResolverTests
{
    [Fact]
    public void MapsExplicitSourcesWithoutInventingOrMutatingValues()
    {
        var resolver = new ExportSourceResolver();
        var s = ExportFixtures.Statement;
        var t = s.Transactions[0];
        Assert.Equal(t.Date, resolver.Resolve("Date", s, t).Value);
        Assert.Equal(t.Amount, resolver.Resolve("Amount", s, t).Value);
        Assert.Equal(s.BankCode, resolver.Resolve("BankCode", s, t).Value);
        Assert.Equal(s.StatementNumber, resolver.Resolve("StatementNumber", s, t).Value);
        Assert.Equal(s.Currency, resolver.Resolve("Currency", s, t).Value);
        Assert.Equal(1, resolver.Resolve("Sequence", s, t).Value);
        Assert.Equal(s.OpeningBalance, resolver.Resolve("OpeningBalance", s, t).Value);
        Assert.Equal(s.ClosingBalance, resolver.Resolve("ClosingBalance", s, t).Value);
        foreach (var source in new[] { "Journal", "Account", "Analytic" })
        {
            Assert.True(resolver.Resolve(source, s, t).IsKnown);
            Assert.Null(resolver.Resolve(source, s, t).Value);
        }
        Assert.Equal("EXPLICIT", resolver.Resolve("Journal", s with
        {
            Journal = "EXPLICIT"
        }, t).Value);
        Assert.Equal("TRANSACTION", resolver.Resolve("Journal", s with
        {
            Journal = "EXPLICIT"
        }, t with
        {
            Journal = "TRANSACTION"
        }).Value);
        Assert.False(resolver.Resolve("Inconnue", s, t).IsKnown);
    }
    [Fact]
    public void MagnitudeFallbackPreservesZeroAndDoesNotInferSigns()
    {
        var r = new ExportSourceResolver();
        var s = ExportFixtures.Statement;
        var t = new StatementTransaction { Debit = 0m };
        Assert.Equal(0m, r.Resolve("Amount", s, t).Value);
        Assert.Null(t.Amount);
        Assert.Equal(12m, r.Resolve("Amount", s, t with
        {
            Debit = null,
            Credit = 12m
        }).Value);
        Assert.Null(r.Resolve("Amount", s, t with
        {
            Debit = null
        }).Value);
        Assert.Null(r.Resolve("Amount", s, t with
        {
            Credit = 1m
        }).Value);
    }
}
public class ExportDateFormatterTests
{
    [Theory]
    [InlineData("dd/MM/yyyy", "01/09/2026")]
    [InlineData("yyyyMMdd", "20260901")]
    [InlineData("ddMMyyyy", "01092026")]
    [InlineData("ddMMyy", "010926")]
    [InlineData("yyMMdd", "260901")]
    [InlineData("yyyy-MM-dd", "2026-09-01")]
    [InlineData("dd-MM-yyyy", "01-09-2026")]
    [InlineData("MM/dd/yyyy", "09/01/2026")]
    [InlineData(null, "2026-09-01")]
    public void FormatsIndependentlyOfSystemCulture(string? format, string expected) => Assert.Equal(expected, new ExportDateFormatter().Format(new(2026, 9, 1), format));
    [Fact] public void RejectsUnknownFormat() => Assert.Throws<ArgumentException>(() => new ExportDateFormatter().Format(new(2026, 9, 1), "random"));
}
public class ExportNumberFormatterTests
{
    [Theory]
    [InlineData("fr-FR", "0.00", "1234,50")]
    [InlineData("en-US", "0.00", "1234.50")]
    [InlineData("en-US", "0.000", "1234.500")]
    [InlineData("en-US", "0.##", "1234.5")]
    public void UsesDecimalExplicitCultureAndNoThousands(string culture, string format, string expected) => Assert.Equal(expected, new ExportNumberFormatter().Format(1234.50m, format, culture));
    [Fact]
    public void RejectsInvalidFormatAndCulture()
    {
        var f = new ExportNumberFormatter();
        Assert.Throws<ArgumentException>(() => f.Format(1m, "N2", "en-US"));
        Assert.Throws<ArgumentException>(() => f.Format(1m, null, "unknown"));
        Assert.Equal("0,00", f.Format(0m, null, "fr-FR"));
        Assert.Equal("-1.25", f.Format(-1.25m, null, "en-US"));
    }
}
public class CsvExportWriterTests
{
    [Theory]
    [InlineData(";")]
    [InlineData(",")]
    [InlineData("\t")]
    [InlineData("|")]
    [InlineData(" ")]
    public void RoundTripsDelimiterQuotesNewlinesAndEmptyCellsThroughIndependentCsvParser(string delimiter)
    {
        var original = "VIREMENT CLIENT; REF \"ABC\"\nDEUXIEME LIGNE";
        var text = new CsvExportWriter().Write(["Libellé", "Vide"], [[original + delimiter + "X", ""]], delimiter, true);
        using var parser = new TextFieldParser(new StringReader(text)) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(delimiter);
        Assert.Equal(new[] { "Libellé", "Vide" }, parser.ReadFields());
        Assert.Equal(new[] { original + delimiter + "X", "" }, parser.ReadFields());
        Assert.True(parser.EndOfData);
    }
}
public class DelimitedTextWriterTests
{
    [Fact]
    public void UsesActualTabsAndHeaderSetting()
    {
        var writer = new DelimitedTextExportWriter();
        Assert.Equal("A\tB\r\n1\t2\r\n", writer.Write(["A", "B"], [["1", "2"]], "\t", true));
        Assert.Equal("1|2\r\n", writer.Write(["A", "B"], [["1", "2"]], "|", false));
    }
}
public class ExportFileNameBuilderTests
{
    [Fact]
    public void MasksAccountAndRejectsPathCharacters()
    {
        var s = ExportFixtures.Statement;
        var name = new ExportFileNameBuilder().Build(s with
        {
            BankCode = "../../BOA\r\n"
        }, ExportFixtures.X3(false));
        Assert.Equal("BOA_1234_2026-09_SAGEX3.txt", name);
        Assert.DoesNotContain(s.AccountNumber!, name);
        Assert.DoesNotContain("1234", new ExportFileNameBuilder().Build(s with
        {
            AccountNumber = "1234"
        }, ExportFixtures.Csv()));
        Assert.StartsWith("Alias_", new ExportFileNameBuilder().Build(s with
        {
            BankCode = s.AccountNumber
        }, ExportFixtures.Csv()));
    }
}
public class ExportEncodingTests
{
    [Fact]
    public async Task SageAsciiTransliteratesAccentsAndUnsupportedCharactersStillHaveDiagnostics()
    {
        var s = ExportFixtures.Statement;
        var accented = s with
        {
            Transactions = [s.Transactions[0] with { Description = "RÈGLEMENT" }, s.Transactions[1]]
        };
        var ascii = await ExportFixtures.Engine.GenerateAsync(accented, ExportFixtures.X3(true), default);
        Assert.Empty(ascii.Errors);
        Assert.Contains("REGLEMENT", Encoding.ASCII.GetString(ascii.Content));
        Assert.Contains(ascii.Warnings, w => w.Code == "SAGE_ASCII_NORMALIZATION");
        Assert.Equal("RÈGLEMENT", accented.Transactions[0].Description);
        var windows = await ExportFixtures.Engine.GenerateAsync(accented, ExportFixtures.Sage(false), default);
        Assert.Empty(windows.Errors);
        Assert.Contains((byte)0xC8, windows.Content);
        var unsupported = s with
        {
            Transactions = [s.Transactions[0] with { Description = "مبلغ" }, s.Transactions[1]]
        };
        var issue = Assert.Single((await ExportFixtures.Engine.GenerateAsync(unsupported, ExportFixtures.X3(true), default)).Errors, e => e.Code == "ENCODING_ERROR");
        Assert.Equal(1, issue.TransactionSequence);
        Assert.Equal("Description", issue.SourceField);
        Assert.Contains((await ExportFixtures.Engine.GenerateAsync(unsupported, ExportFixtures.Sage(false), default)).Errors, e => e.Code == "ENCODING_ERROR");
        var utf = await ExportFixtures.Engine.GenerateAsync(unsupported, ExportFixtures.Csv(), default);
        Assert.Empty(utf.Errors);
        Assert.False(utf.Content.Take(3).SequenceEqual(new byte[] { 239, 187, 191 }));
        Assert.Contains("مبلغ", Encoding.UTF8.GetString(utf.Content));
    }
}
