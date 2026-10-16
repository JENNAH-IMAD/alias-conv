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

public class WorkspaceUserDeletionTests
{
    [Fact]
    public async Task AdminDeletesUserAndSessionsWithoutDeletingHistoryOrAllowingSelfDeletion()
    {
        var configured = Environment.GetEnvironmentVariable("TEST_DATABASE_URL");
        if (string.IsNullOrWhiteSpace(configured))
        {
            using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(StatementPdfExtractorTests.RepositoryRoot(), "backend/BankStatementConverter.API/appsettings.Development.json")));
            configured = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("Default").GetString();
        }
        var name = "alias_user_delete_test_" + Guid.NewGuid().ToString("N");
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
            using var administrator = Client();
            using var collaborator = Client();
            using var anonymous = Client();
            const string password = "Fictional-password-2026";
            (await administrator.PostAsJsonAsync("/api/auth/setup", new SetupInput("Admin", "admin@example.test", password))).EnsureSuccessStatusCode();
            var created = await administrator.PostAsJsonAsync("/api/users", new UserInput("Collaborateur", "user@example.test", "User", true, Permissions.Reader, password));
            created.EnsureSuccessStatusCode();
            var user = (await created.Content.ReadFromJsonAsync<UserDto>())!;
            var users = (await administrator.GetFromJsonAsync<PageResult<UserDto>>("/api/users"))!;
            var admin = users.Items.Single(u => u.Role == "Admin");
            (await collaborator.PostAsJsonAsync("/api/auth/login", new LoginInput("user@example.test", password))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"/api/users/{user.Id}?version={user.Version}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await collaborator.DeleteAsync($"/api/users/{admin.Id}?version={admin.Version}")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await administrator.DeleteAsync($"/api/users/{admin.Id}?version={admin.Version}")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await administrator.DeleteAsync($"/api/users/{user.Id}?version=999")).StatusCode);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.StatementActivities.Add(new()
                {
                    StatementId = Guid.NewGuid(),
                    ActorId = user.Id.ToString(),
                    ActorName = user.Name,
                    Action = "IMPORTED",
                    FileName = "fictional.pdf",
                    Version = 1
                });
                await db.SaveChangesAsync();
            }
            Assert.Equal(HttpStatusCode.NoContent, (await administrator.DeleteAsync($"/api/users/{user.Id}?version={user.Version}")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await collaborator.GetAsync("/api/clients")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await collaborator.PostAsJsonAsync("/api/auth/login", new LoginInput("user@example.test", password))).StatusCode);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.False(await db.WorkspaceUsers.AnyAsync(u => u.Id == user.Id));
                Assert.False(await db.WorkspaceSessions.AnyAsync(s => s.UserId == user.Id));
                Assert.True(await db.StatementActivities.AnyAsync(a => a.ActorId == user.Id.ToString()));
                Assert.Equal(1, await db.WorkspaceUsers.CountAsync(u => u.IsActive && u.Role == "Admin"));
            }
        }
        finally
        {
            Assert.StartsWith("alias_user_delete_test_", name);
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", adminDb);
            await drop.ExecuteNonQueryAsync();
            Assert.Equal(Path.Combine(Path.GetTempPath(), name), Path.GetFullPath(storage));
            if (Directory.Exists(storage))
                Directory.Delete(storage, true);
        }
    }
}
