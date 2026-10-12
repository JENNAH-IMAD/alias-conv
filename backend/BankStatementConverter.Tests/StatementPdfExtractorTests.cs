using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;
namespace BankStatementConverter.Tests;
public class StatementPdfExtractorTests
{
    public static string Fixture(string name)
    {
        var root=RepositoryRoot();
        var directory=Directory.EnumerateDirectories(root).Single(p=>Path.GetFileName(p).StartsWith("relv",StringComparison.OrdinalIgnoreCase));
        return Path.Combine(directory,name);
    }
    internal static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName,"BankStatementConverter.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found for PDF fixtures.");
    }
    [Theory]
    [InlineData("releve_test_01_attijariwafa.pdf",16)]
    [InlineData("releve_banque_populaire_exemple_01.pdf",42)]
    [InlineData("releve_bmce_exemple_01.pdf",36)]
    [InlineData("releve_credit_agricole_exemple_01.pdf",22)]
    [InlineData("releve_societe_generale_exemple_01.pdf",18)]
    [InlineData("releve_test_02_attijariwafa.pdf",16)]
    [InlineData("releve_test_03_attijariwafa.pdf",16)]
    [InlineData("releve_banque_populaire_exemple_02.pdf",42)]
    [InlineData("releve_banque_populaire_exemple_03.pdf",42)]
    [InlineData("releve_banque_populaire_exemple_04.pdf",42)]
    [InlineData("releve_bmce_exemple_02.pdf",36)]
    [InlineData("releve_bmce_exemple_03.pdf",36)]
    [InlineData("releve_bmce_exemple_04.pdf",36)]
    [InlineData("releve_credit_agricole_exemple_02.pdf",22)]
    [InlineData("releve_credit_agricole_exemple_03.pdf",22)]
    [InlineData("releve_credit_agricole_exemple_04.pdf",22)]
    [InlineData("releve_societe_generale_exemple_02.pdf",18)]
    [InlineData("releve_societe_generale_exemple_03.pdf",18)]
    [InlineData("releve_societe_generale_exemple_04.pdf",18)]
    public void ReadsNativePdfColumnsDatesBalancesAndKeepsReviewRequired(string name,int expected)
    {
        var result=new StatementPdfExtractor().Extract(File.ReadAllBytes(Fixture(name)),"Banque choisie","TEST","MAD",default);
        var s=result.Statement;
        Assert.True(expected==s.Transactions.Count,$"Expected {expected}, actual {s.Transactions.Count}: "+string.Join(" / ",s.Transactions.Select(t=>$"{t.Sequence} {t.Date} {t.Description}")));
        Assert.All(s.Transactions,t=> {Assert.NotNull(t.Date);Assert.False(string.IsNullOrWhiteSpace(t.Description));Assert.True(t.Debit.HasValue != t.Credit.HasValue,$"Montant absent/ambigu : transaction {t.Sequence}");});
        Assert.True(s.OpeningBalance.HasValue && s.ClosingBalance.HasValue,$"Soldes {s.OpeningBalance} / {s.ClosingBalance}");
        Assert.NotNull(s.PeriodStart); Assert.NotNull(s.PeriodEnd);
        Assert.Contains(result.Warnings,w=>w.Code=="PDF_REVIEW_REQUIRED"); Assert.False(s.IsValidated);
        Assert.Null(s.RecordType);Assert.Null(s.Journal);Assert.Null(s.Account);Assert.Equal("CREDIT_INCREASES",s.BalanceConvention); Assert.NotNull(s.StatementNumber);
        Assert.Equal(s.Transactions.Sum(t=>t.Debit??0m),s.TotalDebit); Assert.Equal(s.Transactions.Sum(t=>t.Credit??0m),s.TotalCredit);
        Assert.Empty(new StatementValidator().Validate(s).Errors);
        Assert.Empty(new StatementValidator().Validate(s with {BalanceConvention="CREDIT_INCREASES"}).Errors);
    }
    [Fact] public void SmallAmountsRemainInTheirPrintedDebitColumnAndMissingAmountsBlockValidation()
    {
        var result=new StatementPdfExtractor().Extract(File.ReadAllBytes(Fixture("releve_bancaire_attijariwafa_maquette_test.pdf")),"Test","TEST","MAD",default);
        Assert.Equal(27,result.Statement.Transactions.Count);
        Assert.Equal(22m,result.Statement.Transactions[2].Debit);Assert.Null(result.Statement.Transactions[2].Credit);
        var missing=ExportFixtures.Statement with {Transactions=[ExportFixtures.Statement.Transactions[0] with {Debit=null,Credit=null,Amount=null}]};
        Assert.Contains(new StatementValidator().Validate(missing).Errors,e=>e.Code=="MISSING_TRANSACTION_AMOUNT");
    }
    [Fact] public void RejectsCorruptContentAndExplicitCurrencyConflicts()
    {
        Assert.Equal(422,Assert.Throws<AppException>(()=>new StatementPdfExtractor().Extract("%PDF-invalid"u8.ToArray(),"Test","TEST","MAD",default)).Status);
        Assert.Equal(422,Assert.Throws<AppException>(()=>new StatementPdfExtractor().Extract(File.ReadAllBytes(Fixture("releve_test_01_attijariwafa.pdf")),"Test","TEST","EUR",default)).Status);
    }
}
