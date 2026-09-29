using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using Xunit;
namespace BankStatementConverter.Tests;

public class PipelineTests
{
    [Theory]
    [InlineData("1 250,50", "fr-FR", "1250.50")]
    [InlineData("1,250.50", "en-US", "1250.50")]
    [InlineData("1250.50", "fr-FR", "1250.50")]
    [InlineData("-1250.50", "en-US", "-1250.50")]
    [InlineData("1 250,50", "fr-FR", "1250.50")]
    [InlineData("1,250", "en-US", "1250")]
    public void AmountsAreNormalized(string value, string culture, string expected) => Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), AmountNormalizer.Parse(value, culture));
    [Theory]
    [InlineData("garbage")][InlineData("1,2,3")][InlineData("12,34.50")][InlineData("1.2345")]
    public void InvalidAmountsFail(string text) => Assert.Throws<AppException>(() => AmountNormalizer.Parse(text, "fr-FR"));
    [Fact] public void ParserNormalizesDatesAndReversals()
    {
        var rows = new DelimitedStatementParser().Parse("01/05/2026;Achat;25,50;;974,50;REF1\n02/05/2026;Remboursement;-10;;984,50;REF2", new());
        Assert.Equal(new DateOnly(2026,5,1), rows[0].Date); Assert.Equal(25.50m, rows[0].Debit); Assert.Equal("D", rows[0].Direction);
        Assert.Equal(10m, rows[1].Credit); Assert.Equal(0m, rows[1].Debit);
    }
    [Theory]
    [InlineData("31/02/2026;Achat;10;;100;R")][InlineData("01/05/2026;Achat;10;20;100;R")][InlineData("01/05/2026;Achat;invalid;;100;R")][InlineData("texte inattendu")][InlineData("")]
    public void InvalidRowsRejectEntireStatement(string text) => Assert.Throws<AppException>(() => new DelimitedStatementParser().Parse(text, new()));
    [Theory]
    [InlineData("SAGE100", "Date Compte Libellé Montant Sens Analytique")]
    [InlineData("SAGEX3", "Date;Journal;Compte;Libellé;Débit;Crédit;Référence")]
    [InlineData("CUSTOMCSV", "Date;Libellé;Débit;Crédit;Solde")]
    public void ExportColumnsAndEscaping(string type, string header)
    {
        ITransactionExporter exporter = type switch { "SAGE100" => new Sage100Exporter(), "SAGEX3" => new SageX3Exporter(), _ => new CustomCsvExporter() };
        var bytes = exporter.Export([new() {Date = new(2026,5,1), Description = "Achat; \"test\"", Debit = 25.5m, Account="512000", Journal="BQ", Reference="REF"}], Seed.DefaultExport(type));
        var text = Encoding.GetEncoding(type=="SAGE100"?"windows-1252":"utf-8").GetString(bytes); Assert.StartsWith(header + "\r\n", text); Assert.Contains("01/05/2026", text); Assert.Contains("25,50", text); Assert.Contains("\"\"test\"\"", text);
    }
    [Fact] public void RequiredExportFieldCannotBeMissing()
    { var template = Seed.DefaultExport("SAGE100"); template.Fields.First(x=>x.SourceField=="Analytic").Required=true; Assert.Throws<AppException>(() => new Sage100Exporter().Export([new(){Date=new(2026,5,1),Description="Achat"}],template)); }
    [Fact] public async Task FictionalPdfRunsThroughActualExtractionParserAndAllExporters()
    {
        await using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory,"Fixtures","fictional-statement.pdf"));
        var text = await new PdfExtractionService().ExtractAsync(stream, default);
        var transactions = new DelimitedStatementParser().Parse(text, new());
        Assert.Equal(2, transactions.Count); Assert.Equal(1250.50m, transactions[0].Credit); Assert.Equal(25.50m, transactions[1].Debit);
        foreach (ITransactionExporter exporter in new ITransactionExporter[]{new Sage100Exporter(),new SageX3Exporter(),new CustomCsvExporter()}) Assert.NotEmpty(exporter.Export(transactions,Seed.DefaultExport(exporter.Type)));
    }
}
