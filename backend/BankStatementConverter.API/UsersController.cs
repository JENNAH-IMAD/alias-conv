using System.Security.Claims;
using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankStatementConverter.API;

public record UserPermissionsInput(string[]? Permissions);

[ApiController, Authorize(Roles = "Admin"), Route("api/users")]
public class UsersController(UserManagementService service) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet] public Task<PageResult<ManagedUserDto>> List(CancellationToken ct, int page = 1, int pageSize = 20, string? search = null, string? role = null) => service.ListAsync(page, pageSize, search, role, ct);
    [HttpGet("permissions")] public object PermissionCatalog() => new { items = Permissions.Catalog, defaults = Permissions.Defaults };
    [HttpPut("{id:guid}/permissions")] public Task<ManagedUserDto> SetPermissions(Guid id, UserPermissionsInput input, CancellationToken ct) => service.SetPermissionsAsync(Actor, id, input.Permissions, ct);
    [HttpPost] public async Task<IActionResult> Create(ManagedUserInput input, CancellationToken ct) => StatusCode(201, await service.SaveAsync(Actor, null, input, ct));
    [HttpPut("{id:guid}")] public Task<ManagedUserDto> Update(Guid id, ManagedUserInput input, CancellationToken ct) => service.SaveAsync(Actor, id, input, ct);
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(Actor, id, ct); return NoContent(); }
}
