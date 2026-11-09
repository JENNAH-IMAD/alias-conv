using System.Text.Json;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.EntityFrameworkCore;

namespace BankStatementConverter.Infrastructure.Accounting;

public sealed class EntryModelStore(AppDbContext db)
{
    private static readonly JsonSerializerOptions Json = StatementJson.CreateOptions();
    public async Task<EntryModelDossier> Get(Guid clientId, CancellationToken ct)
    {
        Validation.Require(await db.Clients.AnyAsync(x => x.Id == clientId, ct), "Client introuvable.");
        var row = await db.ClientEntryModelSets.AsNoTracking().SingleOrDefaultAsync(x => x.ClientId == clientId, ct);
        return new(clientId, row?.Version ?? 0, row == null ? [] : JsonSerializer.Deserialize<AccountingEntryModel[]>(row.ModelsJson, Json)!);
    }
    public async Task<EntryModelDossier> Save(Guid clientId, EntryModelSave input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"accounting:" + clientId}, 0))", ct);
        Validation.Require(await db.Clients.AnyAsync(x => x.Id == clientId, ct), "Client introuvable.");
        var row = await db.ClientEntryModelSets.SingleOrDefaultAsync(x => x.ClientId == clientId, ct);
        if ((row?.Version ?? 0) != input.Version) throw new AppException(409, "Les modèles ont changé. Rechargez-les avant d'enregistrer.");
        if (row == null) { row = new() { ClientId = clientId, Version = 0 }; db.ClientEntryModelSets.Add(row); }
        row.ModelsJson = JsonSerializer.Serialize(input.Models, Json);
        row.Version++;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(clientId, row.Version, input.Models);
    }
}
