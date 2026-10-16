using System.Security.Claims;
using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BankStatementConverter.API;

[ApiController, Route("api/users"), Authorize(Roles = "Admin")]
public sealed class UsersController(WorkspaceUserService service) : ControllerBase
{
    [HttpGet] public Task<PageResult<UserDto>> List(CancellationToken ct, int page = 1, int pageSize = 20) => service.List(page, pageSize, ct);
    [HttpGet("permissions")] public IActionResult Catalog() => Ok(new { permissions = Permissions.Catalog, presets = new { reader = Permissions.Reader, accountant = Permissions.Accountant } });
    [HttpPost] public async Task<IActionResult> Create(UserInput input, CancellationToken ct) => StatusCode(201, await service.Save(null, input, Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct));
    [HttpPut("{id:guid}")] public Task<UserDto> Update(Guid id, UserInput input, CancellationToken ct) => service.Save(id, input, Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] int version, CancellationToken ct)
    {
        await service.Delete(id, version, Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);
        return NoContent();
    }
}
