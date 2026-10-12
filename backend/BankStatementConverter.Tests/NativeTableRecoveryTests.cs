using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Infrastructure.Exports;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;
namespace BankStatementConverter.Tests;

public class NativeTableRecoveryTests
{
    // Entirely fictional fixture; body text is selectable but captions are absent like image headings.
    internal static byte[] Pdf()
    {
        var pdf=new PdfDocumentBuilder();var font=pdf.AddStandard14Font(Standard14Font.Courier);
        for(var number=1;number<=2;number++)
        {
            var page=pdf.AddPage(PageSize.A4);
            void Text(string value,double x,double top)=>page.AddText(value,8,new PdfPoint(x,842-top-8),font);
            void Amount(string value,double right,double top)=>Text(value,right-value.Length*4.8,top);
            void Operation(string day,double top,string label,string? debit,string? credit)
            {
                Text(day,45,top);Text("09",58,top);Text(label,82,top);Text(day,318,top);Text("09",331,top);
                if(debit!=null) Amount(debit,430,top);if(credit!=null) Amount(credit,517,top);
            }
            Text("SPECIMEN FICTIF",40,30);
            if(number==1)
            {
                Text("31",45,140);Text("08",58,140);Text("SOLDE",82,140);Amount("100,00",430,140);
                Operation("01",160,"PAIEMENT TEST SOUS REF","50,00",null);
                Text("REFERENCE FICTIVE",82,170);
                Operation("02",190,"VIREMENT RECU CLIENT",null,"200,00");
                Amount("50,00",430,600);Amount("200,00",517,600);
            }
            else
            {
                Text("REPORT",82,140);Amount("50,00",430,140);Amount("200,00",517,140);
                Operation("30",160,"FRAIS DE COMPTE","10,00",null);
                Amount("60,00",430,600);Amount("200,00",517,600);Amount("40,00",517,620);
            }
        }
        return pdf.Build();
    }
    internal static PdfTextDocument Ocr()
    {
        var tables=PaddleDocumentParser.Tables("""<table><tr><td>Date d'opér.</td><td>Libellé</td><td>Valeur</td><td>Débit</td><td>Crédit</td></tr><tr><td>30 09</td><td>REPORT\nFRAIS DE COMPTE</td><td>30 09</td><td>50,00\n10,00</td><td>200,00</td></tr><tr><td></td><td>Nouveau solde</td><td></td><td></td><td>40,00</td></tr></table>""");
        var boxes=new[]{new PdfTextBox("DU 01 09 2026 AU 30 09 2026",40,500,50,60),new("Tableau lu",40,500,100,650)};
        return new("PADDLE_OCR_VL_1_6",[new(1,595,842,boxes,Tables:tables),new(2,595,842,boxes,Tables:tables)]);
    }
    internal static PdfTextDocument Document()=>Ocr() with {NativePages=new StatementPdfExtractor().ReadDocument(Pdf(),default).Pages};
    [Fact] public async Task DamagedOcrRowsRecoverAllBodyRowsAndDownloadX3WithBothSignedBalances()
    {
        var raw=Document();var result=new StatementPdfExtractor().Normalize(raw,"TEST","BMCE","MAD",default);
        var s=result.Statement;
        Assert.Equal(3,s.Transactions.Count);Assert.Equal(-100m,s.OpeningBalance);Assert.Equal(40m,s.ClosingBalance);
        Assert.Equal(new DateOnly(2026,8,31),s.OpeningBalanceDate);Assert.Equal(new DateOnly(2026,9,30),s.ClosingBalanceDate);
        Assert.Equal(60m,s.TotalDebit);Assert.Equal(200m,s.TotalCredit);Assert.Equal("CREDIT_INCREASES",s.BalanceConvention);
        Assert.Equal("PAIEMENT TEST SOUS REF\nREFERENCE FICTIVE",s.Transactions[0].Description);
        Assert.All(s.Transactions,t=>{Assert.NotNull(t.Date);Assert.NotNull(t.ValueDate);});
        Assert.Contains(result.Warnings,w=>w.Code=="PDF_NATIVE_RECOVERY");Assert.Same(raw,result.RawDocument);
        Assert.Empty(new StatementValidator().Validate(s).Errors);
        var export=await ExportFixtures.Engine.GenerateAsync(s with {IsValidated=true},ExportFixtures.X3(true),default);
        Assert.Empty(export.Errors);
        var lines=Encoding.ASCII.GetString(export.Content).Split("\r\n",StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(5,lines.Length);Assert.Equal("20260831;20260831;100.00;1;BMCE;MAD;RBBMCE082026;1;1",lines[0]);
        Assert.Equal("20260930;20260930;40.00;-1;BMCE;MAD;RBBMCE092026;3;5",lines[^1]);
    }
    [Fact] public void RecoveryRejectsMissingRowsIncorrectTotalsAndMissingValueDates()
    {
        var raw=Document();var first=raw.NativePages![0];
        var missing=first with {Boxes=first.Boxes.Where(b=>b.Top<159 || b.Top>179).ToList()};
        Assert.Throws<AppException>(()=>NativeTableRecovery.TryNormalize(raw with {NativePages=[missing,raw.NativePages[1]]},"TEST","BMCE","MAD",default));
        var badTotals=raw.NativePages[1] with {Boxes=raw.NativePages[1].Boxes.Select(b=>b.Text=="60,00"?b with {Text="61,00"}:b).ToList()};
        Assert.Throws<AppException>(()=>NativeTableRecovery.TryNormalize(raw with {NativePages=[first,badTotals]},"TEST","BMCE","MAD",default));
        var noValue=first with {Boxes=first.Boxes.Where(b=>!(b.Left>310 && b.Left<350 && b.Top>155 && b.Top<175)).ToList()};
        Assert.Throws<AppException>(()=>NativeTableRecovery.TryNormalize(raw with {NativePages=[noValue,raw.NativePages[1]]},"TEST","BMCE","MAD",default));
    }
    [Fact] public void ScansWithoutSelectableRowsStayBlockedAndMergedAmountsAreNotGuessed()
    {
        Assert.Null(NativeTableRecovery.TryNormalize(Ocr(),"TEST","BMCE","MAD",default));
        Assert.Throws<AppException>(()=>StructuredStatementNormalizer.Normalize(Ocr(),"TEST","BMCE","MAD",default));
    }
}
