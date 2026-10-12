using System.Linq.Expressions;
using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace BankStatementConverter.Infrastructure;

public class DocumentService(AppDbContext db, IFileStorageService storage, IConfiguration config)
{
    public static Expression<Func<Document, DocumentDto>> Projection => x => new(x.Id, x.ClientId, x.BankAccountId,
        x.Client.Name, x.BankAccount.Bank.Name, x.BankAccount.AccountName, x.BankAccount.AccountNumber, x.FileName, x.CreatedAt);

    public async Task<PageResult<DocumentDto>> ListAsync(Guid? clientId, int page, int pageSize, CancellationToken ct)
    {
        Validation.Require(page >= 1 && pageSize is >= 1 and <= 100, "Pagination invalide.");
        var query = db.Documents.AsNoTracking().Where(x => clientId == null || x.ClientId == clientId);
        return new(await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize)
            .Take(pageSize).Select(Projection).ToListAsync(ct), await query.CountAsync(ct), page, pageSize);
    }

    public async Task<DocumentDto> SaveAsync(Guid? id, Guid clientId, Guid accountId, Stream? source,
        string? name, string? contentType, long length, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var item = id.HasValue ? await db.Documents.FromSqlInterpolated($"SELECT * FROM \"Documents\" WHERE \"Id\" = {id.Value} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new AppException(404, "Document introuvable.") : new Document();
        Validation.Require(await db.BankAccounts.AnyAsync(x => x.Id == accountId && x.ClientId == clientId, ct), "Compte bancaire non associé au client.");
        Validation.Require(id.HasValue || source != null, "PDF requis.");
        string? saved = null;
        try
        {
            if (source != null)
            {
                var limit = config.GetValue<long>("Storage:MaxBytes", 20 * 1024 * 1024);
                Validation.Require(length > 0 && length <= limit && contentType == "application/pdf" &&
                    string.Equals(Path.GetExtension(name), ".pdf", StringComparison.OrdinalIgnoreCase), "Choisissez un PDF de 20 Mo maximum.");
                using var buffer = new MemoryStream();
                var chunk = new byte[81920]; int count;
                while ((count = await source.ReadAsync(chunk, ct)) > 0)
                {
                    Validation.Require(buffer.Length + count <= limit, "PDF trop volumineux.");
                    await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
                }
                Validation.Require(buffer.Length >= 5 && Encoding.ASCII.GetString(buffer.GetBuffer(), 0, 5) == "%PDF-", "Signature PDF invalide.");
                buffer.Position = 0;
                saved = await storage.SaveAsync(buffer, "input", "pdf", ct);
                if (id.HasValue) db.PendingFileDeletions.Add(new() { StorageKey = item.StorageKey });
                item.StorageKey = saved;
                item.FileName = Path.GetFileName(name!.Replace('\\', '/'));
                Validation.Text(item.FileName, "Nom du fichier", 2000);
            }
            item.ClientId = clientId; item.BankAccountId = accountId;
            if (!id.HasValue) db.Documents.Add(item);
            await db.SaveChangesAsync(ct);
            var result = await db.Documents.Where(x => x.Id == item.Id).Select(Projection).SingleAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch { if (saved != null) storage.Delete(saved); throw; }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var item = await db.Documents.FromSqlInterpolated($"SELECT * FROM \"Documents\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new AppException(404, "Document introuvable.");
        db.PendingFileDeletions.Add(new() { StorageKey = item.StorageKey });
        db.Documents.Remove(item);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
