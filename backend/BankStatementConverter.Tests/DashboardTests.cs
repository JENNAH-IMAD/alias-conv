using System.Net.Http.Json;
using System.Text.Json;
using BankStatementConverter.API;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace BankStatementConverter.Tests;

public sealed class DashboardTests
{
    [Fact, Trait("Category", "Integration")]
    public async Task DashboardUsesConversionDataPadsDaysKeepsRetainedHistoryAndProtectsDocumentDetails()
    {
        var configured = Environment.GetEnvironmentVariable("TEST_DATABASE_URL");
        if (string.IsNullOrWhiteSpace(configured))
        {
            using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(StatementPdfExtractorTests.RepositoryRoot(), "backend/BankStatementConverter.API/appsettings.Development.json")));
            configured = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("Default").GetString();
        }
        var name = "alias_dashboard_test_" + Guid.NewGuid().ToString("N");
        var storage = Path.Combine(Path.GetTempPath(), name);
        var cs = new NpgsqlConnectionStringBuilder(configured) { Database = name, Pooling = false };
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(configured) { Database = "postgres", Pooling = false }.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing")
                .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = cs.ConnectionString,
                    ["Database:AutoMigrate"] = "true",
                    ["Seed:DemoData"] = "false",
                    ["ArchiveCleanup:Enabled"] = "false",
                    ["Storage:Root"] = storage,
                    ["Ocr:Provider"] = "LOCAL"
                })).ConfigureServices(services =>
                {
                    // ClearAsync requires background cleanup to be stopped during this test.
                    foreach (var worker in services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ArchiveCleanupWorker)).ToArray())
                        services.Remove(worker);
                }));
            using var http = factory.CreateClient();
            http.DefaultRequestHeaders.Add("X-Alias-Request", "1");
            (await http.PostAsJsonAsync("/api/auth/setup", new SetupInput("Dashboard test", "admin@example.test", "Fictional-password-2026"))).EnsureSuccessStatusCode();
            var today = DateTime.UtcNow.Date;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var old = new StatementImport { BankName = "Banque test", CreatedAt = today.AddDays(-100), Status = "NEEDS_REVIEW" };
                var importedExtract = new StatementImport { BankName = "Autre banque", CreatedAt = today, Status = "NEEDS_REVIEW", NormalizedJson = "{\"conversionType\":\"EXTRACT\"}" };
                db.StatementImports.AddRange(old,
                    new StatementImport { BankName = "Banque test", SourceFileName = "recent.pdf", CreatedAt = today.AddHours(1), Status = "EXPORTED", TransactionCount = 12 },
                    importedExtract);
                await db.SaveChangesAsync();
                foreach (var entry in await db.StatementActivities.ToListAsync())
                    entry.CreatedAt = entry.StatementId == old.Id ? today.AddDays(-100) : today;
                db.StatementActivities.AddRange(
                    new StatementActivity { StatementId = Guid.NewGuid(), Action = "IMPORTED", CreatedAt = today.AddDays(-89) },
                    new StatementActivity { StatementId = Guid.NewGuid(), Action = "IMPORTED", CreatedAt = today.AddDays(-90) },
                    new StatementActivity { StatementId = Guid.NewGuid(), Action = "IMPORTED", CreatedAt = today.AddDays(1) });
                var files = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
                using var exportBytes = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("synthetic export"));
                var exportKey = await files.SaveAsync(exportBytes, "exports", "csv", default);
                db.ExportJobs.AddRange(new ExportJob { CreatedAt = today, StorageKey = exportKey, StatementId = importedExtract.Id }, new ExportJob { CreatedAt = today.AddDays(-100), TemplateSnapshotJson = "{\"extract\":{}}" }, new ExportJob { CreatedAt = today.AddDays(1), TemplateSnapshotJson = "{\"conversionType\":\"STATEMENT\"}" });
                db.ExtractionProfileActivities.Add(new()
                {
                    ProfileName = "Test",
                    Action = "CREATED"
                });
                await db.SaveChangesAsync();
            }
            var result = await http.GetFromJsonAsync<JsonElement>("/api/dashboard");
            Assert.Equal(3, result.GetProperty("statements").GetInt32());
            Assert.Equal(3, result.GetProperty("exports").GetInt32());
            Assert.Equal(2, result.GetProperty("statementDocuments").GetInt32());
            Assert.Equal(1, result.GetProperty("extracts").GetInt32());
            Assert.Equal(1, result.GetProperty("statementExports").GetInt32());
            Assert.Equal(2, result.GetProperty("extractExports").GetInt32());
            Assert.Equal(2, result.GetProperty("pendingReview").GetInt32());
            Assert.Equal(3, result.GetProperty("unassigned").GetInt32());
            Assert.Equal(3, result.GetProperty("recent").GetArrayLength());
            Assert.Equal("recent.pdf", result.GetProperty("recent")[0].GetProperty("sourceFileName").GetString());
            var days = result.GetProperty("activity").EnumerateArray().ToArray();
            Assert.Equal(90, days.Length);
            Assert.Equal(today.AddDays(-89).ToString("yyyy-MM-dd"), days[0].GetProperty("date").GetString());
            Assert.Equal(today.ToString("yyyy-MM-dd"), days[^1].GetProperty("date").GetString());
            Assert.Equal(3, days.Sum(day => day.GetProperty("imports").GetInt32()));
            Assert.Equal(1, days.Sum(day => day.GetProperty("exports").GetInt32()));
            Assert.Equal(0, days[1].GetProperty("imports").GetInt32());
            Assert.Equal(1, days[0].GetProperty("imports").GetInt32());
            var states = result.GetProperty("statuses").EnumerateArray().ToDictionary(x => x.GetProperty("status").GetString()!, x => x.GetProperty("count").GetInt32());
            Assert.Equal(2, states["NEEDS_REVIEW"]);
            Assert.Equal(1, states["EXPORTED"]);
            (await http.PostAsJsonAsync("/api/users", new UserInput("Statistiques", "stats@example.test", "User", true, ["dashboard.read"], "Fictional-password-2026"))).EnsureSuccessStatusCode();
            using var reader = factory.CreateClient();
            reader.DefaultRequestHeaders.Add("X-Alias-Request", "1");
            (await reader.PostAsJsonAsync("/api/auth/login", new LoginInput("stats@example.test", "Fictional-password-2026"))).EnsureSuccessStatusCode();
            var restricted = await reader.GetFromJsonAsync<JsonElement>("/api/dashboard");
            Assert.Equal(0, restricted.GetProperty("recent").GetArrayLength());
            Assert.Equal(0, restricted.GetProperty("activity").GetArrayLength());
            Assert.Equal(3, restricted.GetProperty("statements").GetInt32());
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var files = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
                var resultCleanup = await new HistoryCleanupService(db, files).ClearAsync(default);
                Assert.Equal(6, resultCleanup.StatementEvents);
                Assert.Equal(1, resultCleanup.ProfileEvents);
                Assert.Equal(3, resultCleanup.ExportRecords);
                Assert.Equal(1, resultCleanup.DeletedFiles);
                Assert.Equal(0, resultCleanup.PendingFiles);
                Assert.Empty(await db.StatementActivities.ToListAsync());
                Assert.Empty(await db.ExtractionProfileActivities.ToListAsync());
                Assert.Empty(await db.ExportJobs.ToListAsync());
                Assert.Equal(3, await db.StatementImports.CountAsync());
                db.StatementImports.Add(new()
                {
                    BankName = "Après nettoyage"
                });
                await db.SaveChangesAsync();
                Assert.Equal("IMPORTED", Assert.Single(await db.StatementActivities.ToListAsync()).Action);
                Assert.False(Directory.Exists(Path.Combine(storage, "exports")) && Directory.EnumerateFiles(Path.Combine(storage, "exports")).Any());
            }
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
            if (Directory.Exists(storage))
                Directory.Delete(storage, true);
        }
    }
}
