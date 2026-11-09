using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
namespace BankStatementConverter.API;

public sealed class PermissionFilter : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            return Task.CompletedTask;
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                title = "Connectez-vous pour accéder à cet espace."
            });
            return Task.CompletedTask;
        }
        var action = (ControllerActionDescriptor)context.ActionDescriptor;
        if (action.ControllerName == "Auth")
            return Task.CompletedTask;
        if (action.ControllerName is "Users" or "OcrSettings" or "HistoryDeletion")
        {
            if (!user.IsInRole("Admin"))
                Deny(context);
            return Task.CompletedTask;
        }
        var area = action.ControllerName switch
        {
            "Dashboard" => "dashboard",
            "Clients" or "ClientPhoto" => "clients",
            "Banks" or "BankLogo" => "banks",
            "Accounts" => "accounts",
            "AccountingEntryModels" => "documents",
            "Exports" or "EntryOutputTemplates" => "templates",
            "ClientWorkspace" or "StatementImports" or "StatementExports" or "StatementTreatments" or "ExtractionProfiles" => "documents",
            "CatalogDeletion" => context.RouteData.Values["resource"]?.ToString(),
            _ => null
        };
        var method = context.HttpContext.Request.Method;
        if (action.ControllerName == "Banks" && action.ActionName == "Accounts")
            area = "accounts";
        var operation = HttpMethods.IsGet(method) ? "read" : "write";
        if (HttpMethods.IsGet(method) && action.ActionName == "History"
            && action.ControllerName is "ClientWorkspace" or "ExtractionProfiles")
        {
            if (user.IsInRole("Admin")) return Task.CompletedTask;
            area = "history";
        }
        if (action.ControllerName == "EntryOutputTemplates" && action.ActionName == "List" && HttpMethods.IsGet(method)
            && (user.HasClaim("permission", "templates.read") || user.HasClaim("permission", "documents.read")))
            return Task.CompletedTask;
        if (action.ControllerName == "EntryOutputTemplates" && action.ActionName == "Download") { area = "documents"; operation = "export"; }
        if (action.ControllerName == "CatalogDeletion")
            operation = "write";
        if (action.ControllerName == "CatalogDeletion" && !HttpMethods.IsGet(method) &&
           (!user.HasClaim("permission", "accounts.write") || !user.HasClaim("permission", "documents.write")))
        {
            Deny(context);
            return Task.CompletedTask;
        }
        if (action.ControllerName == "AccountingEntryModels" && action.ActionName == "Preview") operation = "read";
        if (action.ControllerName == "AccountingEntryModels" && action.ActionName == "Export") operation = "export";
        if (action.ControllerName == "AccountingEntryModels" && action.ActionName is "ModelFile" or "ArchiveFile") operation = "export";
        if (action.ControllerName == "StatementExports")
            operation = action.ActionName == "Download" || action.ActionName == "ArchivedDownload"
                ? "export"
                : action.ActionName == "DeleteArchived" ? "write" : "read";
        if (action.ControllerName == "StatementImports" && action.ActionName is "StandardDownload" or "CompleteDownload" or "NormalizedPdf" or "DownloadExtract" or "SourceText")
            operation = "export";
        if (action.ControllerName == "StatementImports" && action.ActionName is "NormalizedPdf" or "SourceText" && bool.TryParse(context.HttpContext.Request.Query["preview"], out var preview) && preview)
            operation = "read";
        if (action.ActionName == "Original" && bool.TryParse(context.HttpContext.Request.Query["download"], out var download) && download)
            operation = "export";
        if (area == null || !user.HasClaim("permission", $"{area}.{operation}"))
            Deny(context);
        return Task.CompletedTask;
    }
    private static void Deny(AuthorizationFilterContext context) => context.Result = new ObjectResult(new { title = "Vous n’avez pas la permission nécessaire pour cette action." }) { StatusCode = 403 };
}
