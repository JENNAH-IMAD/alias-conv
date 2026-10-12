using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BankStatementConverter.API;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace BankStatementConverter.Tests;
public sealed class OcrSettingsTests
{
    private sealed class Handler(Action<HttpRequestMessage> observe):HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
            observe(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized){Content=new StringContent("{\"code\":401}")});
        }
    }
    [Fact,Trait("Category","Integration")]
    public async Task OnlyAdminCanSaveEncryptedTokenAndNewConversionsUseTheSettingsWithoutRestart()
    {
        var configured=Environment.GetEnvironmentVariable("TEST_DATABASE_URL");
        if(string.IsNullOrWhiteSpace(configured)) {
            using var settings=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(StatementPdfExtractorTests.RepositoryRoot(),"backend/BankStatementConverter.API/appsettings.Development.json")));
            configured=settings.RootElement.GetProperty("ConnectionStrings").GetProperty("Default").GetString();
        }
        var name="alias_ocr_settings_test_"+Guid.NewGuid().ToString("N");var storage=Path.Combine(Path.GetTempPath(),name);
        var cs=new NpgsqlConnectionStringBuilder(configured){Database=name,Pooling=false};
        await using var admin=new NpgsqlConnection(new NpgsqlConnectionStringBuilder(configured){Database="postgres",Pooling=false}.ConnectionString);
        await admin.OpenAsync();await using(var create=new NpgsqlCommand($"CREATE DATABASE \"{name}\"",admin))await create.ExecuteNonQueryAsync();
        try {
            string? observedToken=null;
            await using var factory=new WebApplicationFactory<Program>().WithWebHostBuilder(builder=>builder.UseEnvironment("Testing").ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{
                ["ConnectionStrings:Default"]=cs.ConnectionString,["Database:AutoMigrate"]="true",["Seed:DemoData"]="false",["ArchiveCleanup:Enabled"]="false",["Storage:Root"]=storage,["Ocr:Provider"]="LOCAL",["Ocr:CloudToken"]="",["Ocr:CloudModel"]="PP-OCRv6"
            })).ConfigureServices(services=>services.AddHttpClient("paddle-cloud").ConfigurePrimaryHttpMessageHandler(()=>new Handler(request=>observedToken=request.Headers.Authorization?.Parameter))));
            using var http=factory.CreateClient();http.DefaultRequestHeaders.Add("X-Alias-Request","1");
            Assert.Equal(HttpStatusCode.Unauthorized,(await http.GetAsync("/api/settings/ocr")).StatusCode);
            (await http.PostAsJsonAsync("/api/auth/setup",new SetupInput("Admin OCR","admin@example.test","Fictional-password-2026"))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest,(await http.PutAsJsonAsync("/api/settings/ocr",new OcrSettingsInput("AI_STUDIO","PP-OCRv6"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,(await http.PutAsJsonAsync("/api/settings/ocr",new OcrSettingsInput("LOCAL","PP-OCRv6",BaseUrl:"file:///etc/passwd"))).StatusCode);
            const string token="fictional-token-for-ocr-settings-test";
            using var saved=await http.PutAsJsonAsync("/api/settings/ocr",new OcrSettingsInput("AI_STUDIO","PaddleOCR-VL-1.6",token));saved.EnsureSuccessStatusCode();
            var json=await saved.Content.ReadAsStringAsync();Assert.DoesNotContain(token,json);Assert.DoesNotContain("cloudToken",json);Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("hasToken").GetBoolean());
            var protectedFile=Path.Combine(storage,".settings","ocr.protected");Assert.DoesNotContain(token,await File.ReadAllTextAsync(protectedFile));
            using(var scope=factory.Services.CreateScope()) {
                var services=scope.ServiceProvider;
                var restored=new OcrSettingsStore(services.GetRequiredService<IConfiguration>(),services.GetRequiredService<IWebHostEnvironment>(),services.GetRequiredService<IDataProtectionProvider>());
                Assert.Equal(token,restored.Snapshot.CloudToken);
                var error=await Assert.ThrowsAsync<AppException>(()=>services.GetRequiredService<IPdfOcrClient>().ExtractAsync(NativeTableRecoveryTests.Pdf(),default));
                Assert.Equal(503,error.Status);Assert.Equal(token,observedToken);
            }
            (await http.PutAsJsonAsync("/api/settings/ocr",new OcrSettingsInput("AI_STUDIO","PP-OCRv6"))).EnsureSuccessStatusCode();
            Assert.Equal(token,factory.Services.GetRequiredService<OcrSettingsStore>().Snapshot.CloudToken);
            (await http.PostAsJsonAsync("/api/users",new UserInput("Lecteur OCR","reader@example.test","User",true,Permissions.Reader,"Fictional-password-2026"))).EnsureSuccessStatusCode();
            using var reader=factory.CreateClient();reader.DefaultRequestHeaders.Add("X-Alias-Request","1");
            (await reader.PostAsJsonAsync("/api/auth/login",new LoginInput("reader@example.test","Fictional-password-2026"))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Forbidden,(await reader.GetAsync("/api/settings/ocr")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await reader.PutAsJsonAsync("/api/settings/ocr",new OcrSettingsInput("LOCAL","PP-OCRv6"))).StatusCode);
        } finally {
            await using var drop=new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();
            if(Directory.Exists(storage))Directory.Delete(storage,true);
        }
    }
}
