using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankStatementConverter.API;

[ApiController, Route("api/history"), Authorize(Roles = "Admin")]
public sealed class HistoryDeletionController(AppDbContext db) : ControllerBase
{
    [HttpPost("{kind}/delete-selection")]
    public async Task<IActionResult> Delete(string kind, HistoryDeletionRequest input, CancellationToken ct)
    {
        Validation.Require(kind is "statements" or "profiles", "Historique inconnu.");
        Validation.Require(input.Ids != null && input.ExcludeIds != null, "Sélection invalide.");
        var ids = input.Ids!;
        var excluded = input.ExcludeIds!;
        Validation.Require(ids.Count <= 10000 && excluded.Count <= 10000 && !ids.Contains(Guid.Empty) && !excluded.Contains(Guid.Empty), "Sélection invalide ou trop volumineuse.");
        Validation.Require(input.All ? ids.Count == 0 : ids.Count > 0 && excluded.Count == 0, "Choisissez les éléments à supprimer.");
        if (kind == "statements")
        {
            var query = db.StatementActivities.AsQueryable();
            if (input.StatementId.HasValue)
                query = query.Where(x => x.StatementId == input.StatementId);
            if (input.ClientId.HasValue)
                query = query.Where(x => x.ClientId == input.ClientId);
            if (input.BankAccountId.HasValue)
                query = query.Where(x => x.BankAccountId == input.BankAccountId);
            query = input.All ? query.Where(x => !excluded.Contains(x.Id)) : query.Where(x => ids.Contains(x.Id));
            return Ok(new
            {
                deleted = await query.ExecuteDeleteAsync(ct)
            });
        }
        Validation.Require(!input.StatementId.HasValue && !input.ClientId.HasValue && !input.BankAccountId.HasValue, "Filtres incompatibles avec l’historique des modèles.");
        var profiles = db.ExtractionProfileActivities.AsQueryable();
        profiles = input.All ? profiles.Where(x => !excluded.Contains(x.Id)) : profiles.Where(x => ids.Contains(x.Id));
        return Ok(new
        {
            deleted = await profiles.ExecuteDeleteAsync(ct)
        });
    }
}

public sealed record HistoryDeletionRequest
{
    public bool All
    {
        get; init;
    }
    public List<Guid> Ids { get; init; } = [];
    public List<Guid> ExcludeIds { get; init; } = [];
    public Guid? StatementId
    {
        get; init;
    }
    public Guid? ClientId
    {
        get; init;
    }
    public Guid? BankAccountId
    {
        get; init;
    }
}
