using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BankStatementConverter.API;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace BankStatementConverter.Tests;

public sealed class MultiUserConversionTests
{
    private sealed class ControlledOcr : IPdfOcrClient
    {
        public readonly OcrWorkQueueTests.HeldOcr Gate = new();
        public async Task<PdfTextDocument> ExtractAsync(byte[] content, CancellationToken ct)
        {
            await Gate.ExtractAsync(content, ct);
            return new StatementPdfExtractor().ReadDocument(content, ct);
        }
    }

    [Fact, Trait("Category", "Integration")]
    public async Task ThreeUsersImportAndExportWithIndependentSessionsProgressAndQueue()
    {
        var configured = Environment.GetEnvironmentVariable("TEST_DATABASE_URL");
        if (string.IsNullOrWhiteSpace(configured))
        {
            using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(StatementPdfExtractorTests.RepositoryRoot(), "backend/BankStatementConverter.API/appsettings.Development.json")));
            configured = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("Default").GetString();
        }
        var name = "alias_multiuser_test_" + Guid.NewGuid().ToString("N");
        var storage = Path.Combine(Path.GetTempPath(), name);
        var testCs = new NpgsqlConnectionStringBuilder(configured) { Database = name, Pooling = false };
        await using var databaseAdmin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(configured) { Database = "postgres", Pooling = false }.ConnectionString);
        await databaseAdmin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", databaseAdmin))
            await create.ExecuteNonQueryAsync();
        try
        {
            var ocr = new ControlledOcr();
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing")
                .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = testCs.ConnectionString,
                    ["Database:AutoMigrate"] = "true",
                    ["Seed:DemoData"] = "false",
                    ["ArchiveCleanup:Enabled"] = "false",
                    ["Storage:Root"] = storage,
                    ["Ocr:Provider"] = "LOCAL",
                    ["Ocr:PreferNativeText"] = "false",
                    ["Ocr:MaxConcurrentJobs"] = "2"
                })).ConfigureServices(services => services.AddScoped<IPdfOcrClient>(s => new QueuedPdfOcrClient(ocr, s.GetRequiredService<OcrWorkQueue>(), s.GetRequiredService<IProcessingProgress>()))));
            HttpClient Client()
            {
                var c = factory.CreateClient();
                c.DefaultRequestHeaders.Add("X-Alias-Request", "1");
                return c;
            }
            using var first = Client();
            using var second = Client();
            using var third = Client();
            var clients = new[] { first, second, third };
            var names = new[] { "Admin parallèle", "Utilisateur deux", "Utilisateur trois" };
            const string password = "Fictional-password-2026";
            (await first.PostAsJsonAsync("/api/auth/setup", new SetupInput(names[0], "admin@example.test", password))).EnsureSuccessStatusCode();
            for (var i = 1; i < 3; i++)
            {
                (await first.PostAsJsonAsync("/api/users", new UserInput(names[i], $"user{i}@example.test", "User", true, Permissions.Accountant, password))).EnsureSuccessStatusCode();
            }
            await Task.WhenAll(Enumerable.Range(1, 2).Select(async i =>
            {
                using var login = await clients[i].PostAsJsonAsync("/api/auth/login", new LoginInput($"user{i}@example.test", password));
                login.EnsureSuccessStatusCode();
            }));
            // Logging out another browser for the same account must not revoke this browser.
            using (var anotherBrowser = Client())
            {
                (await anotherBrowser.PostAsJsonAsync("/api/auth/login", new LoginInput("admin@example.test", password))).EnsureSuccessStatusCode();
                (await anotherBrowser.PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();
                Assert.Equal(HttpStatusCode.Unauthorized, (await anotherBrowser.GetAsync("/api/clients")).StatusCode);
            }
            var banks = (await first.GetFromJsonAsync<PageResult<BankDto>>("/api/banks?pageSize=100"))!;
            var bank = banks.Items.First();
            var bytes = await File.ReadAllBytesAsync(StatementPdfExtractorTests.Fixture("releve_test_01_attijariwafa.pdf"));
            var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            async Task<StatementDetail> Import(int i)
            {
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(ids[i].ToString()), "processingId");
                form.Add(new StringContent(bank.Id.ToString()), "bankId");
                form.Add(new StringContent("MAD"), "currency");
                form.Add(new ByteArrayContent(bytes), "file", $"user-{i}.pdf");
                using var response = await clients[i].PostAsync("/api/statement-imports/pdf", form, timeout.Token);
                response.EnsureSuccessStatusCode();
                return (await response.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json, timeout.Token))!;
            }
            var one = Import(0);
            Assert.Equal(1, await ocr.Gate.Next());
            var two = Import(1);
            Assert.Equal(2, await ocr.Gate.Next());
            var three = Import(2);
            // Wait for admission, not for OCR completion; no timing-dependent sleeps in the provider.
            ProcessingStatus? queued = null;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                using var status = await third.GetAsync($"/api/statement-imports/processing/{ids[2]}", timeout.Token);
                if (status.IsSuccessStatusCode)
                    queued = await status.Content.ReadFromJsonAsync<ProcessingStatus>(cancellationToken: timeout.Token);
                if (queued?.Stage == "queued")
                    break;
                await Task.Delay(20, timeout.Token);
            }
            Assert.Equal("queued", queued?.Stage);
            Assert.Equal(2, ocr.Gate.Calls);
            for (var i = 0; i < 3; i++)
            {
                var session = await clients[i].GetFromJsonAsync<JsonElement>("/api/auth/session", timeout.Token);
                Assert.Equal(names[i], session.GetProperty("user").GetProperty("name").GetString());
                Assert.Equal(HttpStatusCode.NotFound, (await clients[i].GetAsync($"/api/statement-imports/processing/{ids[(i + 1) % 3]}", timeout.Token)).StatusCode);
                (await clients[i].GetAsync("/api/clients", timeout.Token)).EnsureSuccessStatusCode();
            }
            // A slow first user cannot hold up the other worker or the next queued user.
            ocr.Gate.Release(2);
            var detailTwo = await two;
            Assert.Equal(3, await ocr.Gate.Next());
            Assert.False(one.IsCompleted);
            ocr.Gate.Release(3);
            var detailThree = await three;
            Assert.False(one.IsCompleted);
            ocr.Gate.Release(1);
            var detailOne = await one;
            var details = new[] { detailOne, detailTwo, detailThree };
            Assert.Equal(3, details.Select(d => d.Id).Distinct().Count());
            for (var i = 0; i < 3; i++)
            {
                Assert.Equal(names[i], details[i].ImportedByName);
                Assert.Equal($"user-{i}.pdf", details[i].SourceFileName);
                Assert.Equal(16, details[i].Statement.Transactions.Count);
                Assert.Equal(bytes, await clients[i].GetByteArrayAsync($"/api/statement-imports/{details[i].Id}/original", timeout.Token));
                Assert.Equal("done", (await clients[i].GetFromJsonAsync<ProcessingStatus>($"/api/statement-imports/processing/{ids[i]}", timeout.Token))!.Stage);
            }
            var model = ExportFixtures.Csv();
            using var createdModel = await first.PostAsJsonAsync("/api/export-templates", new ExportInput(model.Name, model.Type, model.Description, model.FileExtension, model.Delimiter, model.Encoding, model.NumberCulture, model.IncludeHeader, model.IsActive,
                model.Fields.Select(f => new ExportFieldDto(f.FieldName, f.SourceField, f.Position, f.Format, f.Required)).ToList()), timeout.Token);
            createdModel.EnsureSuccessStatusCode();
            var template = (await createdModel.Content.ReadFromJsonAsync<ExportDto>(cancellationToken: timeout.Token))!;
            await Task.WhenAll(Enumerable.Range(0, 3).Select(async i =>
            {
                using var validation = await clients[i].PostAsJsonAsync($"/api/statement-imports/{details[i].Id}/validate", new StatementVersionRequest(details[i].Version), timeout.Token);
                validation.EnsureSuccessStatusCode();
                var validated = (await validation.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json, timeout.Token))!;
                using var export = await clients[i].PostAsJsonAsync($"/api/statement-imports/{validated.Id}/exports/{template.Id}", new ExportRequest(validated.Version), timeout.Token);
                export.EnsureSuccessStatusCode();
                Assert.NotEmpty(await export.Content.ReadAsByteArrayAsync(timeout.Token));
            }));
        }
        finally
        {
            Assert.StartsWith("alias_multiuser_test_", name);
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", databaseAdmin);
            await drop.ExecuteNonQueryAsync();
            Assert.Equal(Path.Combine(Path.GetTempPath(), name), Path.GetFullPath(storage));
            if (Directory.Exists(storage))
                Directory.Delete(storage, true);
        }
    }
}
