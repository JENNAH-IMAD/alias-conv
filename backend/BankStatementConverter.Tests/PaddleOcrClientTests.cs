using System.Net;
using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace BankStatementConverter.Tests;

public class PaddleOcrClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handle(request, ct);
    }
    [Fact]
    public async Task ReadsActualServiceSchemaAndUsesMultipartWithoutBankMetadata()
    {
        using var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal("http://127.0.0.1:8091/extract", request.RequestUri!.ToString());
            Assert.Contains("multipart/form-data", request.Content!.Headers.ContentType!.ToString());
            var body = await request.Content.ReadAsStringAsync(ct);
            Assert.Contains("releve.pdf", body);
            Assert.DoesNotContain("bankName", body);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"method":"PADDLE_OCR","pages":[{"number":1,"width":595,"height":842,"boxes":[{"text":"VIREMENT","left":10,"right":60,"top":25,"bottom":35,"confidence":0.92}]}]}""", Encoding.UTF8, "application/json")
            };
        });
        using var http = new HttpClient(handler);
        var result = await new PaddleOcrClient(http, new ConfigurationBuilder().Build()).ExtractAsync("%PDF-test"u8.ToArray(), default);
        Assert.Equal("PADDLE_OCR", result.Method);
        Assert.Equal(0.92, Assert.Single(Assert.Single(result.Pages).Boxes).Confidence);
    }
    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"pages\":[]}")]
    [InlineData("{\"pages\":[{\"number\":1,\"width\":595,\"height\":842,\"boxes\":[{\"text\":\"x\",\"confidence\":2}]}]}")]
    public async Task InvalidResponsesAreReportedWithoutLeakingTheResponseBody(string body)
    {
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
        using var http = new HttpClient(handler);
        var exception = await Assert.ThrowsAsync<AppException>(() => new PaddleOcrClient(http, new ConfigurationBuilder().Build()).ExtractAsync("%PDF-test"u8.ToArray(), default));
        Assert.Equal(502, exception.Status);
        Assert.Equal("La réponse du moteur OCR est invalide.", exception.Message);
    }
    [Fact]
    public async Task UnreachableServiceHasAnActionableErrorAndCancellationIsPreserved()
    {
        using var handler = new Handler((_, _) => throw new HttpRequestException("internal diagnostic"));
        using var http = new HttpClient(handler);
        var client = new PaddleOcrClient(http, new ConfigurationBuilder().Build());
        var exception = await Assert.ThrowsAsync<AppException>(() => client.ExtractAsync("%PDF-test"u8.ToArray(), default));
        Assert.Equal(503, exception.Status);
        Assert.Contains("services/ocr/start.ps1", exception.Message);
        Assert.DoesNotContain("internal diagnostic", exception.Message);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ExtractAsync("%PDF-test"u8.ToArray(), cancellation.Token));
    }
}
