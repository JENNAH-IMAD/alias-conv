using System.Text;
using System.Text.Json;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;
namespace BankStatementConverter.Tests;
public class ExportEngineTests
{
    [Fact] public async Task MissingRequiredBlocksFinalOptionalNullDiffersFromZero()
    {
        var s=ExportFixtures.Statement; var t=ExportFixtures.Sage(false); var missing=s with { Transactions=[s.Transactions[0] with {ValueDate=null},s.Transactions[1]] };
        var result=await ExportFixtures.Engine.GenerateAsync(missing,t,default);
        Assert.Contains(result.Errors,e=>e.Code=="REQUIRED_VALUE_MISSING" && e.TransactionSequence==1 && e.SourceField=="ValueDate"); Assert.Empty(result.Content);
        t.Fields.Single(f=>f.SourceField=="ValueDate").Required=false;
        Assert.Empty((await ExportFixtures.Engine.GenerateAsync(missing,t,default)).Errors);
        var preview=await ExportFixtures.Engine.PreviewAsync(s with {Transactions=[s.Transactions[0] with {Debit=null},s.Transactions[1] with {Debit=0m,Amount=0m}],TotalDebit=null,ClosingBalance=null},ExportFixtures.Csv(),default);
        Assert.Equal("",preview.Rows[0][2]); Assert.Equal("0,00",preview.Rows[1][2]);
    }
    [Fact] public async Task UnavailableExplicitSourcesRespectRequired()
    {
        var t=ExportFixtures.Csv(); t.Fields=[new() {FieldName="Compte",SourceField="Account",Position=0,Required=true}];
        var result=await ExportFixtures.Engine.GenerateAsync(ExportFixtures.Statement,t,default);
        Assert.Contains(result.Errors,e=>e.Code=="UNRESOLVED_SOURCE"); Assert.Empty(result.Content);
        t.Fields.First().Required=false; var preview=await ExportFixtures.Engine.PreviewAsync(ExportFixtures.Statement,t,default); Assert.Empty(preview.Errors); Assert.Equal("",preview.Rows[0][0]);
    }
    [Fact] public async Task PositionsAreSortedWithoutFillingGapsAndInputsRemainUnchanged()
    {
        var s=ExportFixtures.Statement; var t=ExportFixtures.Csv();
        t.Fields=[new() {FieldName="Montant",SourceField="Amount",Position=8},new() {FieldName="Libellé",SourceField="Description",Position=0},new() {FieldName="Sens",SourceField="Direction",Position=4}];
        var beforeS=JsonSerializer.Serialize(s); var beforeT=JsonSerializer.Serialize(t);
        var preview=await ExportFixtures.Engine.PreviewAsync(s,t,default); var output=await ExportFixtures.Engine.GenerateAsync(s,t,default);
        Assert.Equal(new[] {"Libellé","Sens","Montant"},preview.Columns); Assert.Equal(3,preview.Rows[0].Length); Assert.Empty(output.Errors);
        Assert.Equal(beforeS,JsonSerializer.Serialize(s)); Assert.Equal(beforeT,JsonSerializer.Serialize(t));
    }
    [Fact] public async Task ValidationAndInactiveTemplateGateFinalButReviewPreviewWorks()
    {
        var s=ExportFixtures.Statement with {IsValidated=false}; var t=ExportFixtures.Csv();
        var preview=await ExportFixtures.Engine.PreviewAsync(s,t,default); Assert.False(preview.IsValidated); Assert.NotEmpty(preview.Rows);
        Assert.Contains(preview.Warnings,w=>w.Code=="STATEMENT_NOT_VALIDATED");
        Assert.Contains((await ExportFixtures.Engine.GenerateAsync(s,t,default)).Errors,e=>e.Code=="STATEMENT_NOT_VALIDATED");
        t.IsActive=false; Assert.Contains((await ExportFixtures.Engine.GenerateAsync(ExportFixtures.Statement,t,default)).Errors,e=>e.Code=="EXPORT_TEMPLATE_INACTIVE");
    }
    [Fact] public async Task PreviewScansAllRowsEvenAfterFiftyAndMatchesFinalErrors()
    {
        var s=ExportFixtures.Statement with {Transactions=Enumerable.Range(1,5000).Select(i=>new StatementTransaction {Sequence=i,Date=new(2026,9,1),ValueDate=i==4999 ? null : new(2026,9,1),Description="TEST",Credit=1m,Direction="CREDIT"}).ToList(),TotalDebit=null,TotalCredit=null,OpeningBalance=null,ClosingBalance=null};
        var preview=await ExportFixtures.Engine.PreviewAsync(s,ExportFixtures.Sage(false),default);
        Assert.Equal(5000,preview.TotalRows); Assert.Equal(50,preview.Rows.Count); Assert.Contains(preview.Errors,e=>e.TransactionSequence==4999);
        var result=await ExportFixtures.Engine.GenerateAsync(s,ExportFixtures.Sage(false),default); Assert.Equal(preview.Errors,result.Errors); Assert.Empty(result.Content);
    }
    [Fact] public async Task X3RequiresBothBalancesAndEmitsBalanceRowsInTheExistingColumns()
    {
        var s=ExportFixtures.Statement; var t=ExportFixtures.X3(false);
        var preview=await ExportFixtures.Engine.PreviewAsync(s,t,default); Assert.Equal(10,preview.Columns.Count); Assert.Equal(4,preview.Rows.Count);
        Assert.Equal(new[] {"OPENING_BALANCE","TRANSACTION","TRANSACTION","CLOSING_BALANCE"},preview.RowKinds);
        Assert.Equal(new int?[] {null,1,2,null},preview.RowSequences); Assert.Equal(4,preview.TotalRows);
        Assert.Equal(new[] {"1","2","2","3"},preview.Rows.Select(r=>r[r.Length-2]));
        Assert.Equal(new[] {"1","2","3","4"},preview.Rows.Select(r=>r[^1]));
        Assert.Equal(new[] {"-1","1","-1","-1"},preview.Rows.Select((r,i)=>r[preview.RowKinds![i]=="TRANSACTION"?4:3]));
        Assert.Equal(new[] {9,10,10,9},preview.Rows.Select(r=>r.Length));
        Assert.Equal(new[] {0,1,3,4,5,6,7,8,9},preview.RowColumnIndices![0]); Assert.Equal(preview.RowColumnIndices[0],preview.RowColumnIndices[^1]);
        Assert.All(preview.Rows,r=>Assert.Equal("RBBOA092026",r[r.Length-3]));
        Assert.DoesNotContain(preview.Warnings,w=>w.Code=="BALANCES_NOT_MAPPED");
        Assert.Contains((await ExportFixtures.Engine.GenerateAsync(s with {OpeningBalance=null},t,default)).Errors,e=>e.Code=="OPENING_BALANCE_MISSING");
        Assert.Contains((await ExportFixtures.Engine.GenerateAsync(s with {ClosingBalance=null},t,default)).Errors,e=>e.Code=="CLOSING_BALANCE_MISSING");
        Assert.Contains(new StatementValidator().Validate(s with {ClosingBalance=0m}).Errors,e=>e.Code=="BALANCE_MISMATCH");
    }
    [Fact] public async Task AmbiguousAmountsAndInvalidDirectionsDoNotGetSilentlyCorrected()
    {
        var s=ExportFixtures.Statement;
        var ambiguous=s with {Transactions=[s.Transactions[0] with {Debit=0m},s.Transactions[1]]};
        Assert.Contains((await ExportFixtures.Engine.GenerateAsync(ambiguous,ExportFixtures.Csv(),default)).Errors,e=>e.Code=="AMBIGUOUS_AMOUNT");
        var invalid=s with {Transactions=[s.Transactions[0] with {Direction="VIREMENT"},s.Transactions[1]]};
        Assert.Contains((await ExportFixtures.Engine.GenerateAsync(invalid,ExportFixtures.X3(false),default)).Errors,e=>e.Code=="INVALID_DIRECTION");
        Assert.Equal("CREDIT",s.Transactions[0].Direction);
    }
}
