using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Mvc;
namespace BankStatementConverter.API;

[ApiController]
public abstract class CatalogController<TEntity, TInput, TDto>(CatalogService<TEntity, TInput, TDto> service) : ControllerBase where TEntity : Entity, new()
{
    [HttpGet] public Task<PageResult<TDto>> List(CancellationToken ct, int page = 1, int pageSize = 50) => service.ListAsync(page, pageSize, ct);
    [HttpGet("{id:guid}")] public Task<TDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost] public virtual async Task<ActionResult<TDto>> Create(TInput input, CancellationToken ct) => StatusCode(201, await service.SaveAsync(null, input, ct));
    [HttpPut("{id:guid}")] public virtual Task<TDto> Update(Guid id, TInput input, CancellationToken ct) => service.SaveAsync(id, input, ct);
    [HttpDelete("{id:guid}")]
    public virtual async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
