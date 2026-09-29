using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Xunit;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Core;

namespace BankStatementConverter.Tests;

public class AutomaticStatementTests
{
    public static IEnumerable<object[]> Examples()
    {
        yield return ["releve_bancaire_attijariwafa_maquette_test.pdf", 27, 36683.93m, 37323.79m];
        yield return ["releve_test_01_attijariwafa.pdf", 16, 28612.90m, 55750m];
        yield return ["releve_test_02_attijariwafa.pdf", 16, 18446.55m, 29300m];
        yield return ["releve_test_03_attijariwafa.pdf", 16, 67025.75m, 146550m];
        yield return ["releve_banque_populaire_exemple_01.pdf", 42, 116916.29m, 437720.67m];
        yield return ["releve_banque_populaire_exemple_02.pdf", 42, 78583.32m, 421469.89m];
        yield return ["releve_banque_populaire_exemple_03.pdf", 42, 97750.73m, 414528.53m];
        yield return ["releve_banque_populaire_exemple_04.pdf", 42, 73584.98m, 445041.31m];
        yield return ["releve_bmce_exemple_01.pdf", 36, 29439.51m, 42068.97m];
        yield return ["releve_bmce_exemple_02.pdf", 36, 24657.31m, 39219.40m];
        yield return ["releve_bmce_exemple_03.pdf", 36, 26797.34m, 39816.46m];
        yield return ["releve_bmce_exemple_04.pdf", 36, 24166.73m, 37867.61m];
        yield return ["releve_credit_agricole_exemple_01.pdf", 22, 6764.76m, 22715.44m];
        yield return ["releve_credit_agricole_exemple_02.pdf", 22, 7487.24m, 18207.55m];
        yield return ["releve_credit_agricole_exemple_03.pdf", 22, 5684.83m, 21715.04m];
        yield return ["releve_credit_agricole_exemple_04.pdf", 22, 7376.16m, 11203m];
        yield return ["releve_societe_generale_exemple_01.pdf", 18, 10643.75m, 29941.71m];
        yield return ["releve_societe_generale_exemple_02.pdf", 18, 14775.15m, 21892.06m];
        yield return ["releve_societe_generale_exemple_03.pdf", 18, 13923.64m, 28692.44m];
        yield return ["releve_societe_generale_exemple_04.pdf", 18, 13168.18m, 22142.16m];
    }

    [Theory, MemberData(nameof(Examples))]
    public void ExtractsSuppliedStatementsAndExports(string filename, int count, decimal debits, decimal credits)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !root.GetDirectories("relv*").Any()) root = root.Parent;
        Assert.NotNull(root);
        using var stream = File.OpenRead(Path.Combine(root.GetDirectories("relv*").Single().FullName, filename));
        var rows = new AutomaticStatementParser().Parse(stream);
        Assert.Equal(count, rows.Count);
        Assert.Equal(debits, rows.Sum(t => t.Debit));
        Assert.Equal(credits, rows.Sum(t => t.Credit));
        Assert.All(rows, t => { Assert.NotNull(t.ValueDate); Assert.NotNull(t.Balance); Assert.NotEmpty(t.Description); Assert.Equal(2026, t.Date.Year); Assert.DoesNotContain("Origine", t.Description); t.Account = "512000"; t.Journal = "BQ"; });
        for (int i = 1; i < rows.Count; i++) Assert.Equal(rows[i - 1].Balance + rows[i].Credit - rows[i].Debit, rows[i].Balance);
        if (filename.Contains("populaire")) Assert.Contains(rows, t => t.Description == "RECUPERATION FRAIS D ENVOI D ECHELLES ET AVIS D OPERATIONS");
        foreach (ITransactionExporter exporter in new ITransactionExporter[] { new Sage100Exporter(), new SageX3Exporter(), new CustomCsvExporter() })
            Assert.NotEmpty(exporter.Export(rows, Seed.DefaultExport(exporter.Type)));
    }

    [Fact]
    public void RejectsUnreadablePdf() => Assert.Throws<AppException>(() => new AutomaticStatementParser().Parse(new MemoryStream([1, 2, 3])));

    private static MemoryStream Statement(string date = "02/01", string valueDate = "31/12", string debit = "10,00", string credit = "", string closing = "90,00", string extra = "", bool blankPage = false)
    {
        var pdf = new PdfDocumentBuilder();
        var font = pdf.AddStandard14Font(Standard14Font.Helvetica);
        var page = pdf.AddPage(600, 800);
        void Text(string s, double x, double y) { if (s.Length > 0) page.AddText(s, 9, new PdfPoint(x, y), font); }
        Text("Du 01/01/2026 au 31/01/2026", 20, 750);
        Text("SOLDE INITIAL 100,00", 20, 720);
        double[] xs = [20, 95, 330, 410, 490, 580];
        foreach (var x in xs) page.DrawLine(new(x, 600), new(x, 700));
        foreach (var y in new[] { 600, 675, 700 }) page.DrawLine(new(20, y), new(580, y));
        string[] headers = ["Date", "Libelle", "Date Valeur", "Debit", "Credit"];
        for (int i = 0; i < headers.Length; i++) Text(headers[i], xs[i] + 3, 685);
        Text(date, 23, 650); Text("ACHAT", 98, 650); Text(valueDate, 333, 650); Text(debit, 413, 650); Text(credit, 493, 650);
        if (extra.Length > 0) { Text("illisible", 23, 642); Text(extra, 413, 642); }
        if (closing.Length > 0) Text("SOLDE FINAL " + closing, 20, 570);
        if (blankPage) pdf.AddPage(600, 800);
        return new MemoryStream(pdf.Build());
    }

    [Fact]
    public void InfersPreviousYearForValueDate()
    {
        using var pdf = Statement();
        var row = Assert.Single(new AutomaticStatementParser().Parse(pdf));
        Assert.Equal(new DateOnly(2026, 1, 2), row.Date);
        Assert.Equal(new DateOnly(2025, 12, 31), row.ValueDate);
    }

    [Theory]
    [InlineData("31/02", "10,00", "", "90,00", "", false)]
    [InlineData("02/01", "10,00", "20,00", "110,00", "", false)]
    [InlineData("02/01", "10,00", "", "90,01", "", false)]
    [InlineData("02/01", "10,00", "", "", "", false)]
    [InlineData("02/01", "10,00", "", "90,00", "7,00", false)]
    [InlineData("02/01", "10,00", "", "90,00", "", true)]
    [InlineData("02/01", "1O,00", "", "90,00", "", false)]
    [InlineData("02/02", "10,00", "", "90,00", "", false)]
    public void RejectsAmbiguousOrIncompleteStatements(string date, string debit, string credit, string closing, string extra, bool blank)
    {
        using var pdf = Statement(date: date, debit: debit, credit: credit, closing: closing, extra: extra, blankPage: blank);
        Assert.Throws<AppException>(() => new AutomaticStatementParser().Parse(pdf));
    }
}
