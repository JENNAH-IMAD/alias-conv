using System.Security.Cryptography;
using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using Microsoft.EntityFrameworkCore;
namespace BankStatementConverter.Infrastructure;

public record DeletionPreview(string Name, int Accounts, int Profiles, int Documents, int Files, bool Processing, string Version);
public class CatalogDeletionService(AppDbContext db)
{
    private async Task<(Entity Parent, List<BankAccount> Accounts, List<BankStatementTemplate> Profiles, List<ConversionHistory> Documents)> Load(string resource, Guid id, CancellationToken ct)
    {
        Entity parent = resource switch {
            "banks" => await db.Banks.FindAsync([id],ct) ?? throw new AppException(404,"Banque introuvable."),
            "clients" => await db.Clients.FindAsync([id],ct) ?? throw new AppException(404,"Client introuvable."),
            _ => throw new AppException(400,"Type de suppression inconnu.")
        };
        var accounts = await db.BankAccounts.Where(a=>resource=="banks"?a.BankId==id:a.ClientId==id).ToListAsync(ct);
        var profiles = await db.StatementTemplates.Where(p=>resource=="banks"&&p.BankId==id).ToListAsync(ct);
        var accountIds=accounts.Select(a=>a.Id).ToArray();var profileIds=profiles.Select(p=>p.Id).ToArray();
        var documents=await db.History.Where(h=>(resource=="clients"&&h.ClientId==id)||accountIds.Contains(h.BankAccountId)||(h.BankStatementTemplateId!=null&&profileIds.Contains(h.BankStatementTemplateId.Value))).ToListAsync(ct);
        return (parent,accounts,profiles,documents);
    }
    private static DeletionPreview Describe((Entity Parent,List<BankAccount> Accounts,List<BankStatementTemplate> Profiles,List<ConversionHistory> Documents) plan)
    {
        var entities=new List<Entity>{plan.Parent};entities.AddRange(plan.Accounts);entities.AddRange(plan.Profiles);entities.AddRange(plan.Documents);
        var stamp=string.Join("|",entities.OrderBy(e=>e.Id).Select(e=>$"{e.Id}:{e.UpdatedAt.Ticks}"));
        var version=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stamp)));
        return new(plan.Parent is Bank b?b.Name:((Client)plan.Parent).Name,plan.Accounts.Count,plan.Profiles.Count,plan.Documents.Count,
            plan.Documents.Sum(d=>new[]{d.InputStorageKey,d.StandardStorageKey,d.OutputStorageKey}.Count(k=>k!=null)),plan.Documents.Any(d=>d.Status=="Processing"),version);
    }
    public async Task<DeletionPreview> PreviewAsync(string resource,Guid id,CancellationToken ct)=>Describe(await Load(resource,id,ct));
    public async Task DeleteAsync(string resource,Guid id,string version,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        // Short write lock: prevent imports, processing claims and association changes during confirmation.
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"History\", \"BankAccounts\", \"StatementTemplates\", \"Clients\", \"Banks\" IN SHARE ROW EXCLUSIVE MODE",ct);
        var plan=await Load(resource,id,ct);var preview=Describe(plan);
        if(preview.Processing)throw new AppException(409,"Un traitement est en cours. Attendez sa fin avant de supprimer.");
        if(preview.Version!=version)throw new AppException(409,"Les éléments liés ont changé. Fermez puis rouvrez la confirmation pour actualiser le récapitulatif.");
        foreach(var document in plan.Documents)
            foreach(var key in new[]{document.InputStorageKey,document.StandardStorageKey,document.OutputStorageKey}.OfType<string>())
                db.PendingFileDeletions.Add(new(){StorageKey=key});
        db.History.RemoveRange(plan.Documents);await db.SaveChangesAsync(ct);
        db.BankAccounts.RemoveRange(plan.Accounts);db.StatementTemplates.RemoveRange(plan.Profiles);await db.SaveChangesAsync(ct);
        db.Remove(plan.Parent);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
}
