using BankStatementConverter.API;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using Xunit;

namespace BankStatementConverter.Tests;

public class PermissionTests
{
    [Fact]
    public void DefaultsEmptyAndAdministratorAreDistinct()
    {
        Assert.Contains("clients.write", Permissions.Effective("User", null));
        Assert.DoesNotContain("banks.write", Permissions.Effective("User", null));
        Assert.Empty(Permissions.Effective("User", []));
        Assert.Equal(Permissions.Catalog.Length, Permissions.Effective("Admin", []).Length);
        Assert.Equal(Permissions.Defaults.Order(), Permissions.Validate(Permissions.Defaults).Order());
    }

    [Fact]
    public void UnknownAndIncompletePermissionsAreRejected()
    {
        Assert.Throws<AppException>(() => Permissions.Validate(null));
        Assert.Throws<AppException>(() => Permissions.Validate(["users.write"]));
        Assert.Throws<AppException>(() => Permissions.Validate(["banks.write"]));
        Assert.Throws<AppException>(() => Permissions.Validate(["documents.read"]));
        Assert.Equal(2, Permissions.Validate(["banks.read", "banks.write", "banks.read"]).Length);
    }

    [Fact]
    public void PermissionChangesRevokeTokensButOrderingDoesNot()
    {
        var user = new User { Username = "Test", PasswordHash = "hash", Email = "test@example.test" };
        var original = UserManagementService.SessionStamp(user);
        user.Permissions = [];
        Assert.NotEqual(original, UserManagementService.SessionStamp(user));
        user.Permissions = ["banks.read", "clients.read"];
        var stamp = UserManagementService.SessionStamp(user);
        user.Permissions = ["clients.read", "banks.read"];
        Assert.Equal(stamp, UserManagementService.SessionStamp(user));
    }

    [Theory]
    [InlineData("BankLogo", "Upload", "PUT", "banks.write")]
    [InlineData("ClientPhoto", "Upload", "PUT", "clients.write")]
    [InlineData("Documents", "Process", "POST", "documents.write")]
    [InlineData("DocumentManagement", "Delete", "DELETE", "documents.write")]
    [InlineData("ClientWorkspace", "Download", "GET", "documents.export")]
    [InlineData("Conversions", "Download", "GET", "documents.export")]
    [InlineData("Statements", "Create", "POST", "profiles.write")]
    [InlineData("Exports", "Update", "PUT", "templates.write")]
    [InlineData("Users", "SetPermissions", "PUT", "admin.only")]
    [InlineData("Unknown", "Index", "GET", "admin.only")]
    public void AlternateEndpointsCannotBypassPermissions(string controller, string action, string method, string expected)
        => Assert.Contains(expected, PermissionFilter.Required(controller, action, method));

    [Fact]
    public void CascadeDeletionRequiresPermissionsForAffectedResources()
    {
        var required = PermissionFilter.Required("CatalogDeletion", "Delete", "POST", "banks");
        Assert.Contains("banks.write", required);
        Assert.Contains("accounts.write", required);
        Assert.Contains("profiles.write", required);
        Assert.Contains("documents.write", required);
    }
}
