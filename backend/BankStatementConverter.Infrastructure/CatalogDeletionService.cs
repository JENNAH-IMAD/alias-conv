using System.Security.Cryptography;
using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using Microsoft.EntityFrameworkCore;
namespace BankStatementConverter.Infrastructure;

public record DeletionPreview(string Name, int Accounts, int Documents, int Files, string Version);
public class CatalogDeletionService(AppDbContext db)
{
    private async Task<(Entity Parent, List<BankAccount> Accounts, List<Document> Documents)> Load(string resource, Guid id, CancellationToken ct)
    {
        Entity parent = resource switch
        {
            "banks" => await db.Banks.FindAsync([id], ct) ?? throw new AppException(404, "Banque introuvable."),
            "clients" => await db.Clients.FindAsync([id], ct) ?? throw new AppException(404, "Client introuvable."),
            _ => throw new AppException(400, "Type de suppression inconnu.")
        };
        var accounts = await db.BankAccounts.Where(a => resource == "banks" ? a.BankId == id : a.ClientId == id).ToListAsync(ct);
        var ids = accounts.Select(x => x.Id).ToArray();
        if (await db.StatementImports.AnyAsync(s => (resource == "clients" && s.ClientId == id) || (s.BankAccountId.HasValue && ids.Contains(s.BankAccountId.Value)), ct))
            throw new AppException(409, "Ce client ou cette banque possède des conversions archivées. Conservez ce référentiel pour préserver leur historique.");
        var documents = await db.Documents.Where(d => (resource == "clients" && d.ClientId == id) || ids.Contains(d.BankAccountId)).ToListAsync(ct);
        return (parent, accounts, documents);
    }
    private static DeletionPreview Describe((Entity Parent, List<BankAccount> Accounts, List<Document> Documents) plan)
    {
        var entities = new List<Entity> { plan.Parent };
        entities.AddRange(plan.Accounts);
        entities.AddRange(plan.Documents);
        var stamp = string.Join("|", entities.OrderBy(e => e.Id).Select(e => $"{e.Id}:{e.UpdatedAt.Ticks}"));
        return new(plan.Parent is Bank b ? b.Name : ((Client)plan.Parent).Name, plan.Accounts.Count, plan.Documents.Count,
            plan.Documents.Select(d => d.StorageKey).Distinct().Count(), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stamp))));
    }
    public async Task<DeletionPreview> PreviewAsync(string resource, Guid id, CancellationToken ct) => Describe(await Load(resource, id, ct));
    public async Task DeleteAsync(string resource, Guid id, string version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Documents\", \"BankAccounts\", \"Clients\", \"Banks\" IN SHARE ROW EXCLUSIVE MODE", ct);
        var plan = await Load(resource, id, ct);
        if (Describe(plan).Version != version)
            throw new AppException(409, "Les éléments liés ont changé. Rouvrez la confirmation.");
        foreach (var key in plan.Documents.Select(d => d.StorageKey).Distinct())
            db.PendingFileDeletions.Add(new()
            {
                StorageKey = key
            });
        db.Documents.RemoveRange(plan.Documents);
        await db.SaveChangesAsync(ct);
        db.BankAccounts.RemoveRange(plan.Accounts);
        await db.SaveChangesAsync(ct);
        db.Remove(plan.Parent);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
