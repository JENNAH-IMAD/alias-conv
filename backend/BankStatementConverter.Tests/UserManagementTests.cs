using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BankStatementConverter.Tests;
public class UserManagementTests
{
    [Fact]
    public void ValidationAndSessionRevocation()
    {
        Assert.Throws<AppException>(() => UserManagementService.ValidateInput(new("Test", "x@test.fr", "Root", "PasswordTest12"), true));
        Assert.Throws<AppException>(() => UserManagementService.ValidateInput(new("Test", "x@test.fr", "User", "short"), true));
        Assert.Throws<AppException>(() => UserManagementService.ValidateInput(new("Test", "x@test.fr", "User", null), true));
        UserManagementService.ValidateInput(new("Test", "x@test.fr", "User", null), false);
        var id = Guid.NewGuid();
        Assert.Throws<AppException>(() => UserManagementService.ValidateSelfChange(id, id, "User"));
        Assert.Throws<AppException>(() => UserManagementService.ValidateSelfChange(id, id, "Admin", true));
        var user = new User { PasswordHash = "hash", Username = "Test", Email = "x@test.fr" };
        var stamp = UserManagementService.SessionStamp(user);
        user.Role = "Admin"; Assert.NotEqual(stamp, UserManagementService.SessionStamp(user));
        stamp = UserManagementService.SessionStamp(user); user.PasswordHash = "new-hash";
        Assert.NotEqual(stamp, UserManagementService.SessionStamp(user));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task AdminCrudAuthorizationAndRevokedSessions()
    {
        var connection = Environment.GetEnvironmentVariable("TEST_DATABASE_URL");
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var suffix = Guid.NewGuid().ToString("N"); var password = "TestPassword123!";
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", connection);
        Environment.SetEnvironmentVariable("Jwt__Key", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        Environment.SetEnvironmentVariable("Seed__AdminEmail", $"admin-{suffix}@example.test");
        Environment.SetEnvironmentVariable("Seed__AdminPassword", password);
        Environment.SetEnvironmentVariable("Database__AutoMigrate", "true");
        Environment.SetEnvironmentVariable("Seed__DemoData", "false");
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider()));
        using var admin = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/users")).StatusCode);
        var session = (await (await admin.PostAsJsonAsync("/api/auth/login", new LoginDto($"admin-{suffix}@example.test", password))).Content.ReadFromJsonAsync<AuthResult>())!;
        admin.DefaultRequestHeaders.Authorization = new("Bearer", session.Token);
        var input = new ManagedUserInput("User " + suffix, $"user-{suffix}@example.test", "User", password);
        var created = await admin.PostAsJsonAsync("/api/users", input); created.EnsureSuccessStatusCode();
        var managed = (await created.Content.ReadFromJsonAsync<ManagedUserDto>())!;
        Assert.DoesNotContain("password", (await created.Content.ReadAsStringAsync()).ToLowerInvariant());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/users", input)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/users/{session.User.Id}", new ManagedUserInput(session.User.Name, session.User.Email, "User", null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/users/{session.User.Id}")).StatusCode);
        using var regular = factory.CreateClient();
        async Task LoginRegular(string secret) { var login = await regular.PostAsJsonAsync("/api/auth/login", new LoginDto(input.Email, secret)); login.EnsureSuccessStatusCode(); var auth = (await login.Content.ReadFromJsonAsync<AuthResult>())!; regular.DefaultRequestHeaders.Authorization = new("Bearer", auth.Token); }
        await LoginRegular(password);
        Assert.Equal(HttpStatusCode.Forbidden, (await regular.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await regular.PostAsJsonAsync("/api/users", input)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await regular.PutAsJsonAsync($"/api/users/{managed.Id}", input)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await regular.DeleteAsync($"/api/users/{managed.Id}")).StatusCode);
        var permissionsUrl = $"/api/users/{managed.Id}/permissions";
        Assert.Equal(HttpStatusCode.Forbidden, (await regular.PutAsJsonAsync(permissionsUrl, new { permissions = Permissions.Defaults })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await regular.GetAsync("/api/users/permissions")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(permissionsUrl, new { permissions = new[] { "banks.write" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/users/{session.User.Id}/permissions", new { permissions = Array.Empty<string>() })).StatusCode);
        (await admin.PutAsJsonAsync(permissionsUrl, new { permissions = Array.Empty<string>() })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await regular.GetAsync("/api/clients")).StatusCode);
        await LoginRegular(password);
        Assert.Equal(HttpStatusCode.Forbidden, (await regular.GetAsync("/api/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await regular.GetAsync("/api/banks")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await regular.GetAsync($"/api/conversions/{Guid.NewGuid()}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await regular.GetAsync($"/api/client-workspace/{Guid.NewGuid()}/archives/{Guid.NewGuid()}/files/export")).StatusCode);
        (await admin.PutAsJsonAsync(permissionsUrl, new { permissions = new[] { "banks.read", "banks.write" } })).EnsureSuccessStatusCode();
        await LoginRegular(password);
        (await regular.GetAsync("/api/banks")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await regular.GetAsync("/api/clients")).StatusCode);
        var bankResponse = await regular.PostAsJsonAsync("/api/banks", new { name = "Permission " + suffix, code = "P" + suffix[..8], description = "Permission test" });
        bankResponse.EnsureSuccessStatusCode();
        var bank = (await bankResponse.Content.ReadFromJsonAsync<BankDto>())!;
        (await admin.DeleteAsync($"/api/banks/{bank.Id}")).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync(permissionsUrl, new { permissions = Permissions.Defaults })).EnsureSuccessStatusCode();
        await LoginRegular(password);
        (await admin.PutAsJsonAsync($"/api/users/{managed.Id}", input with { Role = "Admin", Password = null })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await regular.GetAsync("/api/users")).StatusCode);
        await LoginRegular(password);
        (await regular.GetAsync("/api/users")).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/users/{managed.Id}", input with { Password = "ChangedPassword123!" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await regular.GetAsync("/api/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await regular.PostAsJsonAsync("/api/auth/login", new LoginDto(input.Email, password))).StatusCode);
        await LoginRegular("ChangedPassword123!");
        Assert.Single((await admin.GetFromJsonAsync<PageResult<ManagedUserDto>>($"/api/users?search={suffix}&role=User"))!.Items);
        (await admin.DeleteAsync($"/api/users/{managed.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await regular.GetAsync("/api/clients")).StatusCode);
    }
}
