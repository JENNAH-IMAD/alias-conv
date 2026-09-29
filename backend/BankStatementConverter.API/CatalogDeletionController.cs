using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BankStatementConverter.API;
public record ConfirmCatalogDeletion(string Version);
[ApiController,Authorize,Route("api/catalog-deletions/{resource}/{id:guid}")]
public class CatalogDeletionController(AppDbContext db):ControllerBase
{
    [HttpGet] public async Task<IActionResult> Preview(string resource,Guid id,CancellationToken ct)
    {
        if(resource=="banks"&&!User.IsInRole("Admin"))return Forbid();
        return Ok(await new CatalogDeletionService(db).PreviewAsync(resource,id,ct));
    }
    [HttpPost] public async Task<IActionResult> Delete(string resource,Guid id,ConfirmCatalogDeletion input,CancellationToken ct)
    {
        if(resource=="banks"&&!User.IsInRole("Admin"))return Forbid();
        await new CatalogDeletionService(db).DeleteAsync(resource,id,input.Version,ct);return NoContent();
    }
}
