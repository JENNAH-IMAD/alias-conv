using System.Security.Claims;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace BankStatementConverter.API;

public sealed class WorkspaceCookieEvents(AppDbContext db) : CookieAuthenticationEvents
{
    public static ClaimsPrincipal Principal(WorkspaceUser user, Guid sessionId) => new(new ClaimsIdentity(new[] {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Name),
        new Claim(ClaimTypes.Role, user.Role), new Claim("session", sessionId.ToString())
    }.Concat(WorkspaceUserService.Dto(user).Permissions.Select(p => new Claim("permission", p))), CookieAuthenticationDefaults.AuthenticationScheme));
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var ct = context.HttpContext.RequestAborted;
        if (!Guid.TryParse(context.Principal?.FindFirstValue("session"), out var id)) { context.RejectPrincipal(); return; }
        var session = await db.WorkspaceSessions.AsNoTracking().Include(s => s.User).SingleOrDefaultAsync(s => s.Id == id, ct);
        if (session == null || session.ExpiresAt <= DateTime.UtcNow || !session.User.IsActive || session.UserSessionVersion != session.User.SessionVersion)
        { context.RejectPrincipal(); return; }
        // Permissions and name are always read from the database, never trusted from a stale cookie.
        context.ReplacePrincipal(Principal(session.User, session.Id));
    }
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) => Reject(context, 401, "Connectez-vous pour accéder à cet espace.");
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) => Reject(context, 403, "Vous n’avez pas la permission nécessaire.");
    private static Task Reject(RedirectContext<CookieAuthenticationOptions> context, int status, string title)
    { context.Response.StatusCode = status; return context.Response.WriteAsJsonAsync(new { status, title }); }
}
