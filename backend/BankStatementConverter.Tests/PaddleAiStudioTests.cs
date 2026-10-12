using System.Net;
using System.Text;
using System.Text.Json;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace BankStatementConverter.Tests;

public class PaddleAiStudioTests
{
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> action):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>action(request,ct);}
    private static HttpResponseMessage Json(string body,HttpStatusCode status=HttpStatusCode.OK)=>new(status){Content=new StringContent(body,Encoding.UTF8,"application/json")};
    private static IConfiguration Config(int timeout=600,int retries=0)=>new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Ocr:CloudToken"]="test-server-secret",["Ocr:TimeoutSeconds"]=timeout.ToString(),["Ocr:QueueRetryCount"]=retries.ToString()}).Build();
    internal static byte[] Pdf()=>File.ReadAllBytes(StatementPdfExtractorTests.Fixture("releve_bmce_exemple_01.pdf"));
    internal static string Jsonl()=>File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","paddle-vl-bmce.jsonl"));
    internal static PdfTextDocument Document()=>PaddleDocumentParser.Parse(Jsonl(),new StatementPdfExtractor().ReadDocument(Pdf(),default));

    [Fact] public async Task ConfirmedFullQueueRetriesBeforeCreatingOneJob()
    {
        var submissions=0;var polls=0;
        using var handler=new Handler(async (request,ct)=> {
            if(request.Method==HttpMethod.Post)
            {
                Assert.Contains("releve.pdf",await request.Content!.ReadAsStringAsync(ct));
                return ++submissions==1 ? Json("""{"code":10010}""",HttpStatusCode.BadRequest)
                    : Json("""{"code":0,"data":{"jobId":"accepted-once"}}""");
            }
            if(request.RequestUri!.Host=="results.example") return Json(Jsonl());
            polls++;
            return Json("""{"data":{"state":"done","resultUrl":{"jsonUrl":"https://results.example/test.jsonl"}}}""");
        });
        using var http=new HttpClient(handler);
        var result=await new PaddleAiStudioClient(http,Config(retries:1),new()).ExtractAsync(Pdf(),default);
        Assert.Equal(2,submissions);Assert.Equal(1,polls);Assert.Equal(2,result.Pages.Count);
    }

    [Fact] public async Task WaitingForQueueRespectsDeadline()
    {
        var calls=0;
        using var handler=new Handler((_,_)=> {calls++;return Task.FromResult(Json("""{"code":10010}"""));});
        using var http=new HttpClient(handler);
        var error=await Assert.ThrowsAsync<AppException>(()=>new PaddleAiStudioClient(http,Config(timeout:1,retries:3),new()).ExtractAsync(Pdf(),default));
        Assert.Equal(504,error.Status);Assert.Equal(1,calls);
    }

    [Fact] public async Task OfficialMultipartJobsAndSignedJsonlDownloadKeepBearerServerSide()
    {
        var calls=0;
        using var handler=new Handler(async (request,ct)=>{
            calls++;
            if(calls==1)
            {
                Assert.Equal(HttpMethod.Post,request.Method);Assert.EndsWith("/api/v2/ocr/jobs",request.RequestUri!.AbsoluteUri);
                Assert.Equal("test-server-secret",request.Headers.Authorization!.Parameter);
                var body=await request.Content!.ReadAsStringAsync(ct);Assert.Contains("PaddleOCR-VL-1.6",body);Assert.Contains("optionalPayload",body);Assert.Contains("releve.pdf",body);
                return Json("""{"code":0,"data":{"jobId":"job/with spaces"}}""");
            }
            if(calls==2)
            {
                Assert.Equal("test-server-secret",request.Headers.Authorization!.Parameter);
                Assert.Contains("job%2Fwith%20spaces",request.RequestUri!.AbsoluteUri);
                return Json("""{"data":{"state":"done","extractProgress":{"extractedPages":2},"resultUrl":{"jsonUrl":"https://results.example/test.jsonl"}}}""");
            }
            Assert.Equal("results.example",request.RequestUri!.Host);Assert.Null(request.Headers.Authorization);
            return Json(Jsonl());
        });
        using var http=new HttpClient(handler);
        var result=await new PaddleAiStudioClient(http,Config(),new()).ExtractAsync(Pdf(),default);
        Assert.Equal(3,calls);Assert.Equal("PADDLE_OCR_VL_1_6",result.Method);Assert.Equal(2,result.Pages.Count);
        Assert.All(result.Pages,p=>{Assert.NotNull(p.RawResult);Assert.NotEmpty(p.Tables!);Assert.All(p.Boxes,b=>Assert.Null(b.Confidence));});
        Assert.DoesNotContain("test-server-secret",JsonSerializer.Serialize(result));
    }
    [Theory][InlineData(401,503)][InlineData(403,503)][InlineData(429,429)][InlineData(400,502)][InlineData(500,503)]
    public async Task ProviderFailuresHaveUsefulMessagesWithoutLeakingBodies(int status,int expected)
    {
        using var handler=new Handler((_,_)=>Task.FromResult(Json("provider-secret-diagnostics",(HttpStatusCode)status)));
        using var http=new HttpClient(handler);
        var error=await Assert.ThrowsAsync<AppException>(()=>new PaddleAiStudioClient(http,Config(),new()).ExtractAsync(Pdf(),default));
        Assert.Equal(expected,error.Status);Assert.DoesNotContain("provider-secret",error.Message);
    }
    [Theory]
    [InlineData(10010,400,503,"file d’attente")]
    [InlineData(12001,403,429,"quota quotidien")]
    [InlineData(12002,429,429,"Trop de demandes")]
    [InlineData(10007,400,503,"modèle OCR")]
    [InlineData(10008,400,502,"paramètres")]
    [InlineData(10005,400,422,"lire ce PDF")]
    [InlineData(11003,200,422,"analyser ce PDF")]
    public async Task OfficialErrorCodesDistinguishServiceFailuresFromFileFailures(int code,int status,int expected,string message)
    {
        using var handler=new Handler((_,_)=>Task.FromResult(Json(JsonSerializer.Serialize(new {code,data=new {errorMsg="private provider diagnostics"}}),(HttpStatusCode)status)));
        using var http=new HttpClient(handler);
        var error=await Assert.ThrowsAsync<AppException>(()=>new PaddleAiStudioClient(http,Config(),new()).ExtractAsync(Pdf(),default));
        Assert.Equal(expected,error.Status);Assert.Contains(message,error.Message);Assert.DoesNotContain("private provider",error.Message);
    }
    [Theory]
    [InlineData("""{"data":{"state":"failed","errorMsg":"private provider message"}}""",422)]
    [InlineData("""{"data":{"state":"unexpected"}}""",502)]
    [InlineData("""{"data":{"state":"done","extractProgress":{"extractedPages":1}}}""",502)]
    [InlineData("""{"data":{"state":"done","resultUrl":{"jsonUrl":"http://localhost/secret"}}}""",502)]
    [InlineData("not-json",502)]
    public async Task MalformedFailedAndIncompleteJobsNeverProducePartialImports(string status,int expected)
    {
        var calls=0;using var handler=new Handler((_,_)=>Task.FromResult(Json(++calls==1?"""{"data":{"jobId":"test"}}""":status)));
        using var http=new HttpClient(handler);
        var error=await Assert.ThrowsAsync<AppException>(()=>new PaddleAiStudioClient(http,Config(),new()).ExtractAsync(Pdf(),default));
        Assert.Equal(expected,error.Status);Assert.Equal(2,calls);Assert.DoesNotContain("private provider",error.Message);
    }
    [Fact] public async Task PendingJobsRespectTheDeadlineAndCallerCancellation()
    {
        var calls=0;using var handler=new Handler((_,_)=>Task.FromResult(Json(++calls==1?"""{"data":{"jobId":"test"}}""":"""{"data":{"state":"pending"}}""")));
        using var http=new HttpClient(handler);var client=new PaddleAiStudioClient(http,Config(1),new());
        Assert.Equal(504,(await Assert.ThrowsAsync<AppException>(()=>client.ExtractAsync(Pdf(),default))).Status);
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>client.ExtractAsync(Pdf(),cancellation.Token));
    }
    [Fact] public async Task RunningJobsReportActualPageCountsBeforeCallerCancellation()
    {
        var calls=0;
        using var cancellation=new CancellationTokenSource();
        var reporter=new ProgressRecorder();
        using var handler=new Handler((_,_)=>
        {
            if(++calls==1) return Task.FromResult(Json("""{"data":{"jobId":"test"}}"""));
            cancellation.CancelAfter(30);
            return Task.FromResult(Json("""{"data":{"state":"running","extractProgress":{"extractedPages":1,"totalPages":2}}}"""));
        });
        using var http=new HttpClient(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new PaddleAiStudioClient(http,Config(),new(),reporter).ExtractAsync(Pdf(),cancellation.Token));
        Assert.Equal(("reading",47,1,2),reporter.Latest);
    }
    private sealed class ProgressRecorder : IProcessingProgress
    {
        public (string Stage,int Percent,int? Extracted,int? Total) Latest;
        public void Report(string stage,int percent,int? extractedPages=null,int? totalPages=null)=>Latest=(stage,percent,extractedPages,totalPages);
    }
    [Fact] public void RealFictionalBmceResponsePreservesThirtySixMovementsAndBothBalances()
    {
        var raw=Document();var extractor=new StatementPdfExtractor();var expected=extractor.Extract(Pdf(),"BMCE","BMCE","MAD",default).Statement;
        var actual=extractor.Normalize(raw,"BMCE","BMCE","MAD",default).Statement;
        Assert.Equal(36,actual.Transactions.Count);Assert.Equal(expected.OpeningBalance,actual.OpeningBalance);Assert.Equal(expected.ClosingBalance,actual.ClosingBalance);
        Assert.Equal(expected.PeriodStart,actual.PeriodStart);Assert.Equal(expected.PeriodEnd,actual.PeriodEnd);Assert.Equal("DEMO-000001",actual.AccountNumber);
        foreach(var pair in actual.Transactions.Zip(expected.Transactions))
        {
            Assert.Equal(pair.Second.Date,pair.First.Date);Assert.Equal(pair.Second.ValueDate,pair.First.ValueDate);
            Assert.Equal(pair.Second.Debit,pair.First.Debit);Assert.Equal(pair.Second.Credit,pair.First.Credit);
            Assert.Contains("Origine :",pair.First.Description); // Text amounts never populate a financial field.
        }
        Assert.Empty(new StatementValidator().Validate(actual with {BalanceConvention="CREDIT_INCREASES"}).Errors);
        Assert.Equal(JsonSerializer.Serialize(raw.Pages[0].RawResult),JsonSerializer.Serialize(JsonSerializer.Deserialize<PdfTextDocument>(JsonSerializer.Serialize(raw,StatementImportService.Json),StatementImportService.Json)!.Pages[0].RawResult));
    }
    [Fact] public void HtmlCellsPreserveSplitDatesMultilineLabelsAndMergedHeadersWithoutExecutingHtml()
    {
        var tables=PaddleDocumentParser.Tables("""<table><tr><th rowspan="2">Date</th><th rowspan="2">Date<br>valeur</th><th rowspan="2">Opération</th><th colspan="2">Montants</th></tr><tr><th>Débit</th><th>Crédit</th></tr><tr><td>01/03/20<br>26</td><td>28/02/20<br>26</td><td>VIR &amp; TEST\nOrigine : -999,99 MAD<script>alert(1)</script></td><td></td><td>1 129,00</td></tr></table>""");
        var page=new PdfTextPage(1,595,842,[new("Période du 01/03/2026 au 31/03/2026",0,500,0,20)],Tables:tables);
        var s=StructuredStatementNormalizer.Normalize(new("PADDLE_OCR_VL_1_6",[page]),"TEST","CA","MAD",default).Statement;
        var t=Assert.Single(s.Transactions);Assert.Equal(new DateOnly(2026,3,1),t.Date);Assert.Equal(new DateOnly(2026,2,28),t.ValueDate);
        Assert.Equal("VIR & TEST\nOrigine : -999,99 MAD",t.Description);Assert.Equal(1129m,t.Credit);Assert.Null(t.Debit);
    }
    [Fact] public void UnreadableAmountsAndMissingTablesDoNotBecomeMadeUpMovements()
    {
        var raw=Document();var rows=raw.Pages[0].Tables![0].Rows.Select(r=>r.ToArray()).ToList();rows[2][3]="illisible";
        var changed=raw with {Pages=raw.Pages.Select((p,i)=>i==0?p with {Tables=[new(rows)]}:p).ToList()};
        var result=StructuredStatementNormalizer.Normalize(changed,"BMCE","BMCE","MAD",default);
        Assert.Null(result.Statement.Transactions[0].Amount);Assert.Contains(result.Warnings,w=>w.Code=="PDF_AMOUNT_UNREADABLE");
        Assert.Contains(new StatementValidator().Validate(result.Statement).Errors,e=>e.Code=="MISSING_TRANSACTION_AMOUNT");
        Assert.Throws<AppException>(()=>StructuredStatementNormalizer.Normalize(raw with {Pages=[raw.Pages[0],raw.Pages[1] with {Tables=[]}]},"BMCE","BMCE","MAD",default));
    }
    [Fact] public async Task ExplicitCloudReadingUsesVlEvenForNativePdfAndKeepsTheRawResult()
    {
        var ocr=new AutomaticPdfExtractionTests.OcrStub {Result=Document()};
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Ocr:Provider"]="AI_STUDIO",["Ocr:PreferNativeText"]="false"}).Build();
        var result=await new AutomaticPdfExtraction(new(),ocr,config).ExtractAsync(Pdf(),"BMCE","BMCE","MAD",default);
        Assert.Equal(1,ocr.Calls);Assert.Equal(36,result.Statement.Transactions.Count);Assert.NotNull(result.RawDocument!.Pages[0].RawResult);
    }
}
