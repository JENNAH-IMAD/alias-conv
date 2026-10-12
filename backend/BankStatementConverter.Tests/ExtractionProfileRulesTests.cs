using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Infrastructure.Exports;
using Xunit;
namespace BankStatementConverter.Tests;
public class ExtractionProfileRulesTests
{
 [Fact] public void AmbiguousAliasesAreRejected() {
  Assert.Throws<AppException>(()=>ExtractionProfileRules.ValidateAliases(new(){{"Date",["JOUR"]},{"ValueDate",["jour"]}}));
  Assert.Throws<AppException>(()=>ExtractionProfileRules.ValidateAliases(new(){{"Date",["Date valeur"]}}));
  Assert.Throws<AppException>(()=>ExtractionProfileRules.ValidateAliases(new(){{"ClosingBalance",["Solde initial"]}}));
 }
 [Fact] public void NativeBalanceAliasesJoinSplitWordsWithoutMovingAmounts() {
  var raw=new PdfTextDocument("NATIVE_PDF",[new(1,600,800,[new("Disponible",10,70,30,40),new("début",75,110,30,40),new("100,00",400,450,30,40)])]);
  var adapted=ExtractionProfileRules.ApplyAliases(raw,new(){{"OpeningBalance",["Disponible début"]}});
  Assert.Equal(2,adapted.Pages[0].Boxes.Count);Assert.Equal("Solde initial",adapted.Pages[0].Boxes[0].Text);
  Assert.Equal(400,adapted.Pages[0].Boxes[1].Left);Assert.Equal("100,00",adapted.Pages[0].Boxes[1].Text);
 }
 [Fact] public void CustomBalanceNamesPreserveAmountsAndTransactions() {
  var raw=new PdfTextDocument("OCR",[new(1,600,800,[new("Disponible début 01/03/2026 100,00",0,300,10,20)],Tables:[new(new List<IReadOnlyList<string>> {new[]{"Jour","Valeur","Détail","Débit","Crédit"},new[]{"10/03/2026","11/03/2026","JOUR CLIENT","","50,00"}})])]);
  var adapted=ExtractionProfileRules.ApplyAliases(raw,new(){{"Date",["Jour"]},{"Description",["Détail"]},{"OpeningBalance",["Disponible début"]}});
  Assert.Equal("Solde initial 01/03/2026 100,00",adapted.Pages[0].Boxes[0].Text);
  Assert.Equal("Date opération",adapted.Pages[0].Tables![0].Rows[0][0]);
  Assert.Equal("JOUR CLIENT",adapted.Pages[0].Tables![0].Rows[1][2]);
 }
}
