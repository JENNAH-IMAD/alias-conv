using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure.Accounting;
using Microsoft.AspNetCore.Mvc;

namespace BankStatementConverter.API;

[ApiController, Route("api/accounting/entry-output-templates")]
public sealed class EntryOutputTemplatesController(EntryOutputTemplateCatalog service) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<EntryOutputTemplateDto>> List(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return await service.List(ct);
    }
    [HttpPost]
    public async Task<EntryOutputTemplateDto> Create(EntryOutputTemplateSave input, CancellationToken ct) => await service.Create(input, ct);
    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var file = await service.Structure(id, ct); Response.Headers.CacheControl = "no-store"; return File(file.Content, file.Mime, file.Name);
    }
    [HttpPut("{id:guid}")]
    public async Task<EntryOutputTemplateDto> Update(Guid id, EntryOutputTemplateSave input, CancellationToken ct) => await service.Update(id, input, ct);
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct, int version)
    {
        await service.Delete(id, version, ct); return NoContent();
    }
}
