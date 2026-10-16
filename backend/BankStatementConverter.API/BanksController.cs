using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Mvc;
namespace BankStatementConverter.API;

[ApiController, Route("api/banks")]
public class BanksController(BankService service) : ControllerBase
{
    [HttpGet] public Task<PageResult<BankDto>> List(CancellationToken ct, int page = 1, int pageSize = 20, string? search = null, string sort = "name") => service.ListAsync(page, pageSize, search, sort, ct);
    [HttpGet("{id:guid}")] public Task<BankDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost]
    public async Task<ActionResult<BankDto>> Create(BankInput input, CancellationToken ct)
    {
        var result = await service.SaveAsync(null, input, ct);
        return CreatedAtAction(nameof(Get), new
        {
            id = result.Id
        }, result);
    }
    [HttpPut("{id:guid}")] public Task<BankDto> Update(Guid id, BankInput input, CancellationToken ct) => service.SaveAsync(id, input, ct);
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
    [HttpGet("{id:guid}/accounts")] public Task<PageResult<BankAccountSummary>> Accounts(Guid id, CancellationToken ct, int page = 1, int pageSize = 20) => service.AccountsAsync(id, page, pageSize, ct);
}
