using BankStatementConverter.Application.Exports;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;
namespace BankStatementConverter.Tests;

public class StatementFieldNamesTests
{
    [Fact]
    public void ShortPrintedPeriodCompletesDatesAndRepairsStrictOcrGroupingWithWarning()
    {
        var table = new PdfStructuredTable([
            new[] {"Date opération", "Date valeur", "Libellé", "Débit", "Crédit"},
            new[] {"29/08", "01/09", "TEST", "", "25,972,30"}
        ]);
        var raw = new PdfTextDocument("PADDLE_OCR", [new(1, 600, 800, [new("Relevé du 01/09/25 au 30/09/25", 10, 500, 10, 20)], Tables: [table])]);
        var result = StructuredStatementNormalizer.Normalize(raw, "TEST", "TEST", "MAD", default);
        var row = Assert.Single(result.Statement.Transactions);
        Assert.Equal(new DateOnly(2025, 8, 29), row.Date);
        Assert.Equal(new DateOnly(2025, 9, 1), row.ValueDate);
        Assert.Equal(25972.30m, row.Credit);
        Assert.Contains(result.Warnings, w => w.Code == "PDF_AMOUNT_GROUPING_REPAIRED");
        Assert.Equal("25,972,30", table.Rows[1][4]);
    }
    [Theory]
    [InlineData("بيان $ \\text{Libellé} $ opération", "Description")]
    [InlineData("في مدينة Débit", "Debit")]
    [InlineData("DATES Opération", "Date")]
    [InlineData("DATES OPER", "Date")]
    [InlineData("DATES Valeur", "ValueDate")]
    [InlineData("OPERATION-REFERENCE", "Description")]
    public void BilingualAndExpandedCaptionsKeepTheirMeaning(string caption, string field)
        => Assert.Equal(field, StatementFieldNames.Column(caption));

    [Fact]
    public void TwoLevelBilingualHeadersDoNotLoseOrCreateTransactions()
    {
        var table = new PdfStructuredTable([
            new[] { "DATES", "DATES", "OPERATION-REFERENCE", "DEBIT", "CREDIT" },
            new[] { "Opération", "Valeur", "OPERATION-REFERENCE", "DEBIT", "CREDIT" },
            new[] { "10/03/2026", "11/03/2026", "VIREMENT TEST", "", "50,00" }
        ]);
        var document = new PdfTextDocument("PADDLE_OCR", [new(1, 600, 800, [], Tables: [table])]);
        var transaction = Assert.Single(StructuredStatementNormalizer.Normalize(document, "TEST", "TEST", "MAD", default).Statement.Transactions);
        Assert.Equal(new DateOnly(2026, 3, 10), transaction.Date);
        Assert.Equal(new DateOnly(2026, 3, 11), transaction.ValueDate);
        Assert.Equal("VIREMENT TEST", transaction.Description);
        Assert.Equal(50m, transaction.Credit);
        Assert.Null(transaction.Debit);
    }
    [Theory]
    [InlineData("Date d’écriture", "Date de valeur", "Désignation", "Sorties", "Entrées", "Solde d’ouverture", "Solde de clôture")]
    [InlineData("Posting date", "Value date", "Transaction details", "Money out", "Money in", "Opening balance", "Closing balance")]
    [InlineData("Date d'opér.", "Valeur", "Libellé", "Débit Dirhams", "Crédit Dirhams", "Ancien solde", "Nouveau solde")]
    [InlineData("Date de l’opération", "Dt valeur", "Nature de l’opération", "Mouvement débit", "Mouvement crédit", "Solde reporté", "Solde de fin de période")]
    [InlineData("Date d’exécution", "Date d’effet", "Statement narrative", "Dr", "Cr", "Solde antérieur", "Solde final de la période")]
    [InlineData("Date mouvt", "Value date", "Détail opération", "Montant sortant", "Montant entrant", "Solde de départ", "Solde de clôture")]
    [InlineData("Dt opération", "Date valeur", "Objet", "Paiement", "Encaissement", "Solde initial de la période", "Solde fin")]
    [InlineData("Date de saisie", "Date de valeur", "Libellé / Réf.", "Sens débiteur", "Sens créditeur", "Solde début", "Nouveau solde")]
    [InlineData("Date comptable", "Date de valeur", "Transaction", "Débiteur", "Créditeur", "Solde précédent", "Solde final")]
    public void StructuredSynonymsPreserveDistinctDatesAmountsAndBalances(string date, string value, string label, string debit, string credit, string opening, string closing)
    {
        var html = "<table>" + string.Join("", new string[][] {
            [date,value,label,debit,credit],
            ["01/03/2026","01/03/2026",opening,"","100,00"],
            ["10/03/2026","11/03/2026","VIREMENT","","50,00"],
            ["31/03/2026","31/03/2026",closing,"","150,00"]
        }.Select(row => "<tr>" + string.Join("", row.Select(cell => "<td>" + cell + "</td>")) + "</tr>")) + "</table>";
        var document = new PdfTextDocument("PADDLE_OCR", [new(1, 600, 800, [], Tables: PaddleDocumentParser.Tables(html))]);
        var statement = StructuredStatementNormalizer.Normalize(document, "TEST", "TEST", "MAD", default).Statement;
        var transaction = Assert.Single(statement.Transactions);
        Assert.Equal(new DateOnly(2026, 3, 10), transaction.Date);
        Assert.Equal(new DateOnly(2026, 3, 11), transaction.ValueDate);
        Assert.Equal(50m, transaction.Credit);
        Assert.Null(transaction.Debit);
        Assert.Equal(100m, statement.OpeningBalance);
        Assert.Equal(150m, statement.ClosingBalance);
    }
    [Theory]
    [InlineData("Posting date", "Value date", "Transaction details", "Money out", "Money in")]
    [InlineData("Date opé", "Dt valeur", "Nature de l’opération", "Montant sortant", "Montant entrant")]
    [InlineData("Date mouvement", "Date d’effet", "Libellé / Réf.", "Débit (en DH)", "Crédit (en DHS)")]
    public void NativeMultiwordCaptionsRecognizeDifferentDateMeanings(string date, string value, string label, string debit, string credit)
    {
        var boxes = new List<PdfTextBox>();
        void Add(string text, double x, double y) => boxes.Add(new(text, x, x + text.Length * 3, y, y + 8));
        var headers = new[] { date, value, label, debit, credit };
        var row = new[] { "10/03/2026", "11/03/2026", "TEST TRANSFER", "", "50,00" };
        var positions = new[] { 30d, 130d, 250d, 450d, 550d };
        for (var i = 0; i < 5; i++)
        {
            Add(headers[i], positions[i], 80);
            if (row[i] != "")
                Add(row[i], positions[i], 110);
        }
        Add("Opening balance 01/03/2026 100,00", 30, 30);
        Add("Closing balance 31/03/2026 150,00", 30, 160);
        var statement = new StatementPdfExtractor().Normalize(new("NATIVE_PDF", [new(1, 650, 800, boxes)]), "TEST", "TEST", "MAD", default).Statement;
        var transaction = Assert.Single(statement.Transactions);
        Assert.Equal(new DateOnly(2026, 3, 10), transaction.Date);
        Assert.Equal(new DateOnly(2026, 3, 11), transaction.ValueDate);
        Assert.Equal(50m, transaction.Credit);
        Assert.Equal("TEST TRANSFER", transaction.Description);
        Assert.Equal(100m, statement.OpeningBalance);
        Assert.Equal(150m, statement.ClosingBalance);
    }
    [Theory]
    [InlineData("Date opération valeur")]
    [InlineData("Débit Crédit")]
    [InlineData("Montant")]
    [InlineData("Solde créditeur")]
    public void AmbiguousNamesDoNotGuessAColumn(string name) => Assert.Null(StatementFieldNames.Column(name));
    [Fact]
    public void AmbiguousBalanceDoesNotGuessOpeningOrClosing()
    {
        Assert.Null(StatementFieldNames.Balance("Solde créditeur"));
        Assert.Null(StatementFieldNames.Balance("Solde initial et final"));
        Assert.Null(StatementFieldNames.Balance("Report page solde initial"));
        Assert.Null(StatementFieldNames.Balance("Solde reporté de page"));
        Assert.Null(StatementFieldNames.Balance("Solde au 31/03/2026"));
        Assert.Null(StatementFieldNames.Balance("Solde actuel"));
        Assert.Null(StatementFieldNames.Balance("Solde disponible"));
        Assert.Null(StatementFieldNames.Balance("Solde final disponible"));
    }

    [Theory]
    [InlineData("Date de comptabilisation")]
    [InlineData("Date d’enregistrement")]
    [InlineData("Date comptable")]
    public void BookingDatesNeverSilentlyBecomeValueDates(string name)
    {
        Assert.Equal("Date", StatementFieldNames.Column(name));
        var rows = new List<IReadOnlyList<string>> {
            new[]{"Date opération",name,"Libellé","Débit","Crédit"},
            new[]{"10/03/2026","11/03/2026","VIREMENT","","50,00"}
        };
        var raw = new PdfTextDocument("PADDLE_OCR", [new(1, 650, 800, [], Tables: [new(rows)])]);
        Assert.Throws<BankStatementConverter.Application.AppException>(() => StructuredStatementNormalizer.Normalize(raw, "TEST", "TEST", "MAD", default));
    }

    [Fact]
    public void ReferenceColumnIsPreservedSeparatelyFromNarrative()
    {
        var rows = new List<IReadOnlyList<string>> {
            new[]{"Date transaction","Dt valeur","Désignation","Réf.","Dr","Cr"},
            new[]{"10/03/2026","11/03/2026","PAIEMENT CLIENT","R-123","25,00",""}
        };
        var document = new PdfTextDocument("PADDLE_OCR", [new(1, 650, 800, [], Tables: [new(rows)])]);
        var transaction = Assert.Single(StructuredStatementNormalizer.Normalize(document, "TEST", "TEST", "MAD", default).Statement.Transactions);
        Assert.Equal("PAIEMENT CLIENT", transaction.Description);
        Assert.Equal("R-123", transaction.Reference);
        Assert.Equal(25m, transaction.Debit);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AvailableFundsCannotSupplyAMissingClosingBalance(bool structured)
    {
        var boxes = new List<PdfTextBox> {
            new("Solde antérieur 01/03/2026 100,00",30,240,30,38),
            new("Solde disponible au 31/03/2026 1 150,00",30,260,170,178)
        };
        var rows = new List<IReadOnlyList<string>> {
            new[]{"Date opération","Date valeur","Libellé","Débit","Crédit"},
            new[]{"10/03/2026","11/03/2026","VIREMENT","","50,00"}
        };
        if (structured)
        {
            rows.Add(new[] { "31/03/2026", "", "Solde disponible", "", "1 150,00" });
        }
        else
        {
            var positions = new[] { 30d, 130d, 250d, 450d, 550d };
            for (var r = 0; r < rows.Count; r++)
                for (var c = 0; c < rows[r].Count; c++)
                    if (rows[r][c] != "")
                        boxes.Add(new(rows[r][c], positions[c], positions[c] + rows[r][c].Length * 3, 80 + r * 30, 88 + r * 30));
        }
        var raw = new PdfTextDocument(structured ? "PADDLE_OCR" : "NATIVE_PDF", [new(1, 650, 800, boxes, Tables: structured ? [new(rows)] : null)]);
        var statement = (structured ? StructuredStatementNormalizer.Normalize(raw, "TEST", "TEST", "MAD", default) : new StatementPdfExtractor().Normalize(raw, "TEST", "TEST", "MAD", default)).Statement;
        Assert.Equal(100m, statement.OpeningBalance);
        Assert.Null(statement.ClosingBalance);
        Assert.Equal(50m, Assert.Single(statement.Transactions).Credit);
    }
}
