using System.Security.Claims;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BankStatementConverter.API;

[ApiController, Route("api/auth")]
public sealed class AuthController(WorkspaceUserService users, AppDbContext db) : ControllerBase
{
    [HttpGet("session"), AllowAnonymous]
    public async Task<IActionResult> Session(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var user = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? await db.WorkspaceUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct) : null;
        return Ok(new { setupRequired = await users.NeedsSetup(ct), user = user == null ? null : WorkspaceUserService.Dto(user) });
    }
    [HttpPost("setup"), AllowAnonymous, EnableRateLimiting("authentication")]
    public async Task<UserDto> Setup(SetupInput input, CancellationToken ct) => await SignIn(await users.Setup(input, ct), ct);
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("authentication")]
    public async Task<UserDto> Login(LoginInput input, CancellationToken ct) => await SignIn(await users.Login(input, ct), ct);
    private async Task<UserDto> SignIn(WorkspaceUser user, CancellationToken ct)
    {
        var expiry = DateTime.UtcNow.AddHours(8);
        var previous = User.FindFirstValue("session");
        if (Guid.TryParse(previous, out var oldId)) await db.WorkspaceSessions.Where(s => s.Id == oldId).ExecuteDeleteAsync(ct);
        await db.WorkspaceSessions.Where(s => s.ExpiresAt < DateTime.UtcNow).ExecuteDeleteAsync(ct);
        var session = new WorkspaceSession { UserId = user.Id, UserSessionVersion = user.SessionVersion, ExpiresAt = expiry };
        db.WorkspaceSessions.Add(session); await db.SaveChangesAsync(ct);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, WorkspaceCookieEvents.Principal(user, session.Id), new AuthenticationProperties { ExpiresUtc = expiry, IsPersistent = false });
        return WorkspaceUserService.Dto(user);
    }
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Guid.TryParse(User.FindFirstValue("session"), out var id)) await db.WorkspaceSessions.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        await HttpContext.SignOutAsync(); return NoContent();
    }
}
