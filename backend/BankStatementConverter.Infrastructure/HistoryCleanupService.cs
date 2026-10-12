using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using Microsoft.EntityFrameworkCore;

namespace BankStatementConverter.Infrastructure;

public sealed class HistoryCleanupService(AppDbContext db, IFileStorageService storage)
{
    // Run while conversions are stopped: retain the schema and all primary business records.
    public async Task<HistoryCleanupResult> ClearAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var jobs = await db.ExportJobs.AsNoTracking().Select(job=>new{job.StorageKey,job.StandardStorageKey}).ToListAsync(ct);
        var keys = jobs.SelectMany(job=>new[]{job.StorageKey,job.StandardStorageKey}).Where(key=>key?.StartsWith("exports/",StringComparison.Ordinal)==true).Select(key=>key!).Distinct().ToArray();
        foreach(var key in keys) if(!await db.PendingFileDeletions.AnyAsync(x=>x.StorageKey==key,ct))db.PendingFileDeletions.Add(new(){StorageKey=key});
        var exports = await db.ExportJobs.ExecuteDeleteAsync(ct);
        var statements = await db.StatementActivities.ExecuteDeleteAsync(ct);
        var profiles = await db.ExtractionProfileActivities.ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        var deleted=0;
        foreach(var key in keys) {
            try { storage.Delete(key); var entries=await db.PendingFileDeletions.Where(x=>x.StorageKey==key).ToListAsync(ct);db.PendingFileDeletions.RemoveRange(entries);deleted++; }
            catch(IOException) { }
            catch(UnauthorizedAccessException) { }
        }
        await db.SaveChangesAsync(ct);
        return new(statements,profiles,exports,deleted,keys.Length-deleted);
    }
}
public sealed record HistoryCleanupResult(int StatementEvents,int ProfileEvents,int ExportRecords,int DeletedFiles,int PendingFiles);
