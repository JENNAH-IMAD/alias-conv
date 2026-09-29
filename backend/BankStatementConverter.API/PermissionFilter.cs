using System.Security.Claims;
using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace BankStatementConverter.API;

// Runs for every MVC endpoint, including uploads, previews and direct file downloads.
public sealed class PermissionFilter(AppDbContext db) : IAsyncAuthorizationFilter
{
    public static string[] Required(string controller, string action, string method, string? resource = null)
    {
        var write = method is not ("GET" or "HEAD");
        string[] Module(string key) => write ? [key + ".read", key + ".write"] : [key + ".read"];
        return controller switch
        {
            "Auth" => [],
            "Users" => ["admin.only"],
            "Clients" or "ClientPhoto" => Module("clients"),
            "Accounts" => Module("accounts"),
            "Banks" when action == "Accounts" => ["banks.read", "accounts.read"],
            "Banks" when action == "Profiles" => ["banks.read", "profiles.read"],
            "Banks" or "BankLogo" => Module("banks"),
            "Statements" => Module("profiles"),
            "Exports" => Module("templates"),
            "History" when action == "Dashboard" => ["dashboard.read"],
            "History" => ["documents.read"],
            "Documents" or "DocumentManagement" => ["documents.read", "documents.write"],
            "Conversions" when action == "Download" => ["documents.read", "documents.export"],
            "Conversions" => ["documents.read", "documents.write", "documents.export"],
            "ClientWorkspace" when action == "Download" => ["documents.read", "documents.export"],
            "ClientWorkspace" => ["documents.read"],
            "CatalogDeletion" => resource switch
            {
                "clients" => ["clients.write", "accounts.write", "documents.write"],
                "banks" => ["banks.write", "accounts.write", "profiles.write", "documents.write"],
                "bank-accounts" => ["accounts.write", "documents.write"],
                _ => ["admin.only"]
            },
            _ => ["admin.only"]
        };
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor action || action.ControllerName == "Auth") return;
        var principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true) { context.Result = new UnauthorizedResult(); return; }
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) { context.Result = new UnauthorizedResult(); return; }
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, context.HttpContext.RequestAborted);
        if (user == null) { context.Result = new UnauthorizedResult(); return; }
        if (user.Role == "Admin") return;
        var required = Required(action.ControllerName, action.ActionName, context.HttpContext.Request.Method, context.RouteData.Values["resource"]?.ToString());
        var allowed = Permissions.Effective(user.Role, user.Permissions);
        if (!required.All(allowed.Contains)) context.Result = new ObjectResult(new { title = "Vous ne disposez pas des permissions nécessaires pour cette action.", status = 403 }) { StatusCode = 403 };
    }
}
