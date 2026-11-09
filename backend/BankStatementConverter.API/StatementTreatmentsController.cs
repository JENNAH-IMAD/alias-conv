using BankStatementConverter.Infrastructure.Exports;
using Microsoft.AspNetCore.Mvc;

namespace BankStatementConverter.API;

[ApiController, Route("api/statement-imports/{statementId:guid}/treatment")]
public sealed class StatementTreatmentsController(StatementTreatmentStore store) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid statementId, CancellationToken ct, Guid? extractId = null)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await store.Get(statementId, extractId, ct));
    }
}
