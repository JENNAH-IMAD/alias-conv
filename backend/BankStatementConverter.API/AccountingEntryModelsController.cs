using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure.Accounting;
using Microsoft.AspNetCore.Mvc;
namespace BankStatementConverter.API;

[ApiController, Route("api/accounting/entry-models")]
public sealed class AccountingEntryModelsController(IndependentEntryModelService service) : ControllerBase
{
    [HttpGet("{clientId:guid}")]
    public Task<EntryModelDossier> Get(Guid clientId, CancellationToken ct) => service.Get(clientId, ct);
    [HttpPut("{clientId:guid}")]
    public Task<EntryModelDossier> Save(Guid clientId, EntryModelSave input, CancellationToken ct) => service.Save(clientId, input, ct);
    [HttpPost("preview")]
    public Task<EntryModelPreview> Preview(EntryModelRun input, CancellationToken ct) => service.Preview(input, ct);
    [HttpGet("{clientId:guid}/{modelId:guid}/file")]
    public async Task<IActionResult> ModelFile(Guid clientId, Guid modelId, CancellationToken ct) {
        var file = await service.ModelFile(clientId, modelId, ct); return File(file.Content, file.Mime, file.Name);
    }
    [HttpGet("archives/{id:guid}")]
    public Task<EntryArchiveDetail> Archive(Guid id, CancellationToken ct) => service.Archive(id, ct);
    [HttpGet("archives/{id:guid}/file")]
    public async Task<IActionResult> ArchiveFile(Guid id, CancellationToken ct) {
        var file = await service.ArchiveFile(id, ct); return File(file.Content, file.Mime, file.Name);
    }
    [HttpDelete("archives/{id:guid}")]
    public async Task<IActionResult> DeleteArchive(Guid id, CancellationToken ct)
    {
        await service.DeleteArchive(id, ct);
        return NoContent();
    }
    [HttpPost("export")]
    public async Task<IActionResult> Export(EntryModelRun input, CancellationToken ct)
    {
        var file = await service.Export(input, ct);
        Response.Headers.CacheControl = "no-store";
        return File(file.Content, file.Mime, file.Name);
    }
}
