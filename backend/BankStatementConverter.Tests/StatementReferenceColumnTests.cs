using BankStatementConverter.Application.Exports;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;

namespace BankStatementConverter.Tests;

public class StatementReferenceColumnTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DescriptionAndReferenceRemainSeparateOnEitherSide(bool referenceBeforeDescription)
    {
        var boxes = new List<PdfTextBox>();
        void Text(string value, double x, double y) => boxes.Add(new(value, x, x + value.Length * 3, y, y + 8));
        var descriptionX = referenceBeforeDescription ? 240 : 220;
        var referenceX = referenceBeforeDescription ? 160 : 355;
        var labelX = referenceBeforeDescription ? 220 : 170;
        Text("Liste des opérations", 30, 30);
        Text("Date d’opération", 20, 100);
        Text("Date Valeur", 100, 100);
        Text("Libellé de l’opération", descriptionX, 100);
        Text("Référence", referenceX, 100);
        Text("Débit", 435, 100);
        Text("Crédit", 535, 100);
        Text("09/10/2026", 20, 130);
        Text("09/10/2026", 100, 130);
        Text("VIR. REÇU CLIENT", labelX, 130);
        Text("823HXB", referenceX, 130);
        Text("1 000 000,00", 535, 130);
        Text("02/10/2026", 20, 180);
        Text("30/09/2026", 100, 180);
        Text("REGLEMENT PRELEVEMENT", labelX, 180);
        Text("EN FAVEUR DE MAROC LEASING", labelX, 194);
        Text("261001", referenceX, 180);
        Text("326,00", 435, 180);
        var document = new PdfTextDocument("NATIVE_PDF", [new(1, 640, 800, boxes)]);
        var result = new StatementPdfExtractor().Normalize(document, "Banque Populaire", "BP", "MAD", default);
        Assert.Equal(2, result.Statement.Transactions.Count);
        Assert.Equal("VIR. REÇU CLIENT", result.Statement.Transactions[0].Description);
        Assert.Equal("823HXB", result.Statement.Transactions[0].Reference);
        Assert.Equal("REGLEMENT PRELEVEMENT\nEN FAVEUR DE MAROC LEASING", result.Statement.Transactions[1].Description);
        Assert.Equal("261001", result.Statement.Transactions[1].Reference);
        Assert.Equal(1000000m, result.Statement.Transactions[0].Credit);
        Assert.Equal(326m, result.Statement.Transactions[1].Debit);
    }
}
