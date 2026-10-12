using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Authentication;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BankStatementConverter.Tests;

public class PaddleTextDocumentTests
{
    private static PdfTextDocument Source()=>new StatementPdfExtractor().ReadDocument(PaddleAiStudioTests.Pdf(),default);
    private static string Jsonl(PdfTextDocument source)=>JsonSerializer.Serialize(new { result=new {
        dataInfo=new {pages=source.Pages.Select(p=>new {width=p.Width*3,height=p.Height*3})},
        ocrResults=source.Pages.Select(p=>new {prunedResult=new {
            rec_texts=p.Boxes.Select(b=>b.Text), rec_scores=p.Boxes.Select(_=>0.99),
            rec_polys=p.Boxes.Select(b=>new []{new []{b.Left*3,b.Top*3},new []{b.Right*3,b.Top*3},new []{b.Right*3,b.Bottom*3},new []{b.Left*3,b.Bottom*3}})
        }})
    }});
    [Fact] public void OcrPolygonsScaleToPdfPointsAndPreserveFinancialValues()
    {
        var source=Source();var raw=PaddleTextDocumentParser.Parse(Jsonl(source),source);
        Assert.Equal("PADDLE_OCR_V6",raw.Method);Assert.Equal(2,raw.Pages.Count);
        Assert.All(raw.Pages,p=>{Assert.NotNull(p.RawResult);Assert.Null(p.Tables);});
        Assert.Equal(source.Pages[0].Boxes[0].Left,raw.Pages[0].Boxes[0].Left,6);
        Assert.Equal(0.99,raw.Pages[0].Boxes[0].Confidence);
        var extractor=new StatementPdfExtractor();
        var expected=extractor.Normalize(source,"BMCE","BMCE","MAD",default).Statement;
        var actual=extractor.Normalize(raw,"BMCE","BMCE","MAD",default).Statement;
        Assert.Equal(36,actual.Transactions.Count);Assert.Equal(expected.OpeningBalance,actual.OpeningBalance);Assert.Equal(expected.ClosingBalance,actual.ClosingBalance);
        foreach(var (a,e) in actual.Transactions.Zip(expected.Transactions))
        {Assert.Equal(e.Date,a.Date);Assert.Equal(e.ValueDate,a.ValueDate);Assert.Equal(e.Debit,a.Debit);Assert.Equal(e.Credit,a.Credit);}
    }
    [Theory]
    [InlineData("\"rec_scores\":[0.99", "\"rec_scores\":[1.99")]
    [InlineData("\"rec_polys\":[", "\"rec_polys\":[[[0,0],[0,0],[0,0],[0,0]],")]
    public void InvalidConfidenceAndMismatchedArraysAreRejected(string from,string to)
    {var source=Source();Assert.Throws<AppException>(()=>PaddleTextDocumentParser.Parse(Jsonl(source).Replace(from,to),source));}
    [Fact] public void MissingPagesAreRejected()
    {var source=Source();Assert.Throws<AppException>(()=>PaddleTextDocumentParser.Parse(Jsonl(source with {Pages=[source.Pages[0]]}),source));}
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> action):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>action(request);}
    [Fact] public async Task CloudModelUsesOcrOptionsAndOcrResultParser()
    {
        var calls=0;
        using var handler=new Handler(async request=>{
            var body=++calls switch {
                1=>"""{"code":0,"data":{"jobId":"test"}}""",
                2=>"""{"code":0,"data":{"state":"done","extractProgress":{"extractedPages":2},"resultUrl":{"jsonUrl":"https://results.example/test.jsonl"}}}""",
                _=>Jsonl(Source())
            };
            if(calls==1){var content=await request.Content!.ReadAsStringAsync();Assert.Contains("PP-OCRv6",content);Assert.Contains("useTextlineOrientation",content);Assert.DoesNotContain("useChartRecognition",content);}
            if(calls==3) Assert.Null(request.Headers.Authorization);
            return new(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"application/json")};
        });
        using var http=new HttpClient(handler);
        var cfg=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Ocr:CloudToken"]="private-test-token",["Ocr:CloudModel"]="PP-OCRv6"}).Build();
        var result=await new PaddleAiStudioClient(http,cfg,new()).ExtractAsync(PaddleAiStudioTests.Pdf(),default);
        Assert.Equal("PADDLE_OCR_V6",result.Method);Assert.Equal(3,calls);
    }
    [Fact] public async Task TlsFailuresExplainNetworkFilterWithoutLeakingDiagnostics()
    {
        using var handler=new Handler(_=>Task.FromException<HttpResponseMessage>(new HttpRequestException(HttpRequestError.SecureConnectionError,"private diagnostic",new AuthenticationException("private certificate details"))));
        using var http=new HttpClient(handler);
        var cfg=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Ocr:CloudToken"]="private-test-token"}).Build();
        var error=await Assert.ThrowsAsync<AppException>(()=>new PaddleAiStudioClient(http,cfg,new()).ExtractAsync(PaddleAiStudioTests.Pdf(),default));
        Assert.Equal(503,error.Status);Assert.Contains("certificat",error.Message);Assert.DoesNotContain("private",error.Message);
    }
}
