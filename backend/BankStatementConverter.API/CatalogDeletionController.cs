using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Mvc;
namespace BankStatementConverter.API;

public record ConfirmCatalogDeletion(string Version);
[ApiController, Route("api/catalog-deletions/{resource}/{id:guid}")]
public class CatalogDeletionController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Preview(string resource, Guid id, CancellationToken ct)
    {
        return Ok(await new CatalogDeletionService(db).PreviewAsync(resource, id, ct));
    }
    [HttpPost]
    public async Task<IActionResult> Delete(string resource, Guid id, ConfirmCatalogDeletion input, CancellationToken ct)
    {
        await new CatalogDeletionService(db).DeleteAsync(resource, id, input.Version, ct); return NoContent();
    }
}
