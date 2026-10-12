using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;

namespace BankStatementConverter.Tests;

public class PdfStatementMetadataTests
{
    private static PdfTextDocument Document(params string[] lines) => new("PADDLE_OCR",
        [new PdfTextPage(1,600,800,lines.Select((line,i)=>new PdfTextBox(line,10,580,20+i*20,30+i*20)).ToList())]);
    private static BankStatement Missing => ExportFixtures.Statement with {
        BankName="BMCE Bank",BankCode="662354",StatementNumber=null,TotalDebit=null,TotalCredit=null,BalanceConvention=null };

    [Fact] public void CompletesCanonicalCodeNumberTotalsAndConventionWithoutMutatingInput()
    {
        var s=Missing;
        var result=PdfStatementMetadata.Complete(new(s,[],Document("BMCE BANK")));
        Assert.Equal("BMCE",result.Statement.BankCode);Assert.Equal("RBBMCE092026",result.Statement.StatementNumber);
        Assert.Equal(125.50m,result.Statement.TotalDebit);Assert.Equal(12500m,result.Statement.TotalCredit);
        Assert.Equal("CREDIT_INCREASES",result.Statement.BalanceConvention);
        Assert.Contains(result.Warnings,w=>w.Code=="PDF_TOTALS_COMPUTED");
        Assert.Contains(result.Warnings,w=>w.Code=="PDF_STATEMENT_NUMBER_GENERATED");
        Assert.Null(s.StatementNumber);Assert.Null(s.TotalDebit);Assert.Same(s.Transactions,result.Statement.Transactions);
    }
    [Fact] public void ReadsExplicitNumberAndPrintedTotalsAndPreservesDiscrepancies()
    {
        var result=PdfStatementMetadata.Complete(new(Missing,[],Document("Relevé N° : ABC-009", "Date Débit Crédit", "TOTAL 125,50 12 499,00")));
        Assert.Equal("ABC-009",result.Statement.StatementNumber);Assert.Equal(12499m,result.Statement.TotalCredit);
        Assert.Contains(new StatementValidator().Validate(result.Statement).Errors,e=>e.SourceField=="TotalCredit");
    }
    [Fact] public void PrintedTotalsIncludingOpeningBalanceAreConvertedToMovementTotals()
    {
        var result=PdfStatementMetadata.Complete(new(Missing,[],Document("Date Débit Crédit","TOTAL 125,50 13 500,00")));
        Assert.Equal(12500m,result.Statement.TotalCredit);
        Assert.Contains(result.Warnings,w=>w.Code=="PDF_TOTALS_INCLUDE_OPENING_BALANCE");
        Assert.Empty(new StatementValidator().Validate(result.Statement).Errors);
    }
    [Fact] public void ManualValuesRemainAuthoritativeWhenCompletingOrAdapting()
    {
        var manual=Missing with {BankCode="CUSTOM",StatementNumber="MANUEL",PeriodStart=new(2026,8,1),
            TotalDebit=40m,TotalCredit=50m,BalanceConvention="DEBIT_INCREASES",OpeningBalance=25m,ClosingBalance=35m};
        var result=PdfStatementMetadata.Complete(new(Missing,[],Document("Relevé N° ABC", "TOTAL DEBIT 125,50")),manual).Statement;
        Assert.Equal(manual.BankCode,result.BankCode);Assert.Equal(manual.StatementNumber,result.StatementNumber);
        Assert.Equal(manual.PeriodStart,result.PeriodStart);Assert.Equal(manual.TotalDebit,result.TotalDebit);
        Assert.Equal(manual.TotalCredit,result.TotalCredit);Assert.Equal(manual.BalanceConvention,result.BalanceConvention);
        Assert.Equal(manual.OpeningBalance,result.OpeningBalance);Assert.Equal(manual.ClosingBalance,result.ClosingBalance);
    }
    [Fact] public void MissingOrUnreadableMovementsDoNotProduceGuessedTotalsOrConvention()
    {
        var result=PdfStatementMetadata.Complete(new(Missing with {Transactions=[]},[],Document("BMCE BANK"))).Statement;
        Assert.Null(result.TotalDebit);Assert.Null(result.TotalCredit);Assert.Null(result.BalanceConvention);
        var incomplete=Missing with {Transactions=[Missing.Transactions[0] with {Credit=null,Amount=null}]};
        result=PdfStatementMetadata.Complete(new(incomplete,[],Document("BMCE BANK"))).Statement;
        Assert.Null(result.TotalCredit);Assert.Null(result.BalanceConvention);
    }
    [Fact] public void OppositeBalanceConventionIsDetectedOnlyWhenItsEquationMatches()
    {
        var s=Missing with {ClosingBalance=1000m+125.50m-12500m};
        Assert.Equal("DEBIT_INCREASES",PdfStatementMetadata.Complete(new(s,[],Document("BMCE BANK"))).Statement.BalanceConvention);
        Assert.Null(PdfStatementMetadata.Complete(new(s with {ClosingBalance=42m},[],Document("BMCE BANK"))).Statement.BalanceConvention);
    }
}
