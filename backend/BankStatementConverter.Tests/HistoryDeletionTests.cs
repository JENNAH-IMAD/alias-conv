using System.Net;
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
using Npgsql;
using Xunit;

namespace BankStatementConverter.Tests;

public class HistoryDeletionTests
{
    [Fact, Trait("Category", "Integration")]
    public async Task OnlyAdminDeletesIndividualSelectionsOrFilteredHistoryWithoutRemovingBusinessRecords()
    {
        var configured = Environment.GetEnvironmentVariable("TEST_DATABASE_URL");
        if (string.IsNullOrWhiteSpace(configured))
        {
            using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(StatementPdfExtractorTests.RepositoryRoot(), "backend/BankStatementConverter.API/appsettings.Development.json")));
            configured = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("Default").GetString();
        }
        var name = "alias_history_delete_test_" + Guid.NewGuid().ToString("N");
        var storage = Path.Combine(Path.GetTempPath(), name);
        var connection = new NpgsqlConnectionStringBuilder(configured) { Database = name, Pooling = false };
        await using var adminDb = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(configured) { Database = "postgres", Pooling = false }.ConnectionString);
        await adminDb.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", adminDb))
            await create.ExecuteNonQueryAsync();
        try
        {
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Testing").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connection.ConnectionString,
                ["Database:AutoMigrate"] = "true",
                ["Seed:DemoData"] = "false",
                ["ArchiveCleanup:Enabled"] = "false",
                ["Storage:Root"] = storage
            })));
            HttpClient Client()
            {
                var client = factory.CreateClient();
                client.DefaultRequestHeaders.Add("X-Alias-Request", "1");
                return client;
            }
            using var admin = Client();
            using var user = Client();
            using var anonymous = Client();
            const string password = "Fictional-password-2026";
            (await admin.PostAsJsonAsync("/api/auth/setup", new SetupInput("Admin test", "admin@example.test", password))).EnsureSuccessStatusCode();
            (await admin.PostAsJsonAsync("/api/users", new UserInput("User test", "user@example.test", "User", true, Permissions.All, password))).EnsureSuccessStatusCode();
            (await user.PostAsJsonAsync("/api/auth/login", new LoginInput("user@example.test", password))).EnsureSuccessStatusCode();
            var statement = new StatementImport { BankName = "Test", NormalizedJson = "{}" };
            var clientId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var events = Enumerable.Range(0, 25).Select(i => new StatementActivity { StatementId = statement.Id, ClientId = clientId, BankAccountId = accountId, Action = "IMPORTED", FileName = $"test-{i}.pdf" }).ToArray();
            var other = new StatementActivity { StatementId = Guid.NewGuid(), Action = "MODIFIED" };
            var profile = new ExtractionProfile { Name = "Modèle test" };
            var profileEvents = Enumerable.Range(0, 3).Select(_ => new ExtractionProfileActivity { ProfileId = profile.Id, ProfileName = profile.Name, Action = "UPDATED", SnapshotJson = "{}" }).ToArray();
            var export = new ExportJob { StatementId = statement.Id, StorageKey = "exports/preserved.csv" };
            Directory.CreateDirectory(Path.Combine(storage, "exports"));
            await File.WriteAllTextAsync(Path.Combine(storage, "exports/preserved.csv"), "fichier conservé");
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.StatementImports.Add(statement);
                db.ExtractionProfiles.Add(profile);
                db.ExportJobs.Add(export);
                db.StatementActivities.AddRange(events);
                db.StatementActivities.Add(other);
                db.ExtractionProfileActivities.AddRange(profileEvents);
                await db.SaveChangesAsync();
            }
            var path = "/api/history/statements/delete-selection";
            var one = new HistoryDeletionRequest { Ids = [events[0].Id] };
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(path, one)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync(path, one)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/history/profiles/delete-selection", new HistoryDeletionRequest { All = true })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(path, new HistoryDeletionRequest())).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(path, new HistoryDeletionRequest { All = true, Ids = [events[0].Id] })).StatusCode);
            async Task<int> Delete(string url, HistoryDeletionRequest input)
            {
                var response = await admin.PostAsJsonAsync(url, input);
                response.EnsureSuccessStatusCode();
                return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("deleted").GetInt32();
            }
            Assert.Equal(1, await Delete(path, one));
            Assert.Equal(0, await Delete(path, one));
            Assert.Equal(2, await Delete(path, new()
            {
                Ids = [events[1].Id, events[2].Id, events[2].Id]
            }));
            Assert.Equal(0, await Delete(path, new()
            {
                Ids = [other.Id],
                ClientId = clientId
            }));
            Assert.Equal(21, await Delete(path, new()
            {
                All = true,
                StatementId = statement.Id,
                ClientId = clientId,
                BankAccountId = accountId,
                ExcludeIds = [events[3].Id]
            }));
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // Also retain the automatic import event, whose client/account filters do not match.
                Assert.Equal(3, await db.StatementActivities.CountAsync());
                Assert.True(await db.StatementActivities.AnyAsync(x => x.Id == other.Id));
            }
            Assert.Equal(3, await Delete(path, new()
            {
                All = true
            }));
            Assert.Equal(1, await Delete("/api/history/profiles/delete-selection", new()
            {
                Ids = [profileEvents[0].Id]
            }));
            Assert.Equal(2, await Delete("/api/history/profiles/delete-selection", new()
            {
                All = true
            }));
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Empty(await db.StatementActivities.ToListAsync());
                Assert.Empty(await db.ExtractionProfileActivities.ToListAsync());
                Assert.True(await db.StatementImports.AnyAsync(x => x.Id == statement.Id));
                Assert.True(await db.ExtractionProfiles.AnyAsync(x => x.Id == profile.Id));
                Assert.True(await db.ExportJobs.AnyAsync(x => x.Id == export.Id));
                Assert.Empty(await db.PendingFileDeletions.ToListAsync());
            }
            Assert.True(File.Exists(Path.Combine(storage, "exports/preserved.csv")));
        }
        finally
        {
            Assert.StartsWith("alias_history_delete_test_", name);
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", adminDb);
            await drop.ExecuteNonQueryAsync();
            Assert.Equal(Path.Combine(Path.GetTempPath(), name), Path.GetFullPath(storage));
            if (Directory.Exists(storage))
                Directory.Delete(storage, true);
        }
    }
}
