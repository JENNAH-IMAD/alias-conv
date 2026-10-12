using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BankStatementConverter.API;

public class DocumentForm
{
    public Guid ClientId { get; set; }
    public Guid BankAccountId { get; set; }
    public IFormFile? File { get; set; }
}
[ApiController, Route("api/documents")]
public class DocumentsController(DocumentService service, AppDbContext db, IFileStorageService storage) : ControllerBase
{
    [HttpGet] public Task<PageResult<DocumentDto>> List(CancellationToken ct, Guid? clientId = null, int page = 1, int pageSize = 20)
        => service.ListAsync(clientId, page, pageSize, ct);
    [HttpGet("{id:guid}")]
    public async Task<DocumentDto> Get(Guid id, CancellationToken ct) => await db.Documents.AsNoTracking().Where(x => x.Id == id)
        .Select(DocumentService.Projection).SingleOrDefaultAsync(ct) ?? throw new AppException(404, "Document introuvable.");
    [HttpPost, Consumes("multipart/form-data")]
    public async Task<IActionResult> Import([FromForm] DocumentForm input, CancellationToken ct)
    {
        await using var source = input.File?.OpenReadStream();
        var result = await service.SaveAsync(null, input.ClientId, input.BankAccountId, source, input.File?.FileName, input.File?.ContentType, input.File?.Length ?? 0, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
    [HttpPut("{id:guid}"), Consumes("multipart/form-data")]
    public async Task<DocumentDto> Edit(Guid id, [FromForm] DocumentForm input, CancellationToken ct)
    {
        await using var source = input.File?.OpenReadStream();
        return await service.SaveAsync(id, input.ClientId, input.BankAccountId, source, input.File?.FileName, input.File?.ContentType, input.File?.Length ?? 0, ct);
    }
    [HttpGet("{id:guid}/original")]
    public async Task<IActionResult> Original(Guid id, CancellationToken ct, bool download = false)
    {
        var item = await db.Documents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException(404, "Document introuvable.");
        Response.Headers.CacheControl = "no-store";
        return download ? File(storage.Open(item.StorageKey), "application/pdf", item.FileName) : File(storage.Open(item.StorageKey), "application/pdf");
    }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    { await service.DeleteAsync(id, ct); return NoContent(); }
}
