using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace BankStatementConverter.Infrastructure;

public class ArchiveService(AppDbContext db)
{
    // The lock serializes successful completions for a client, including concurrent imports.
    public async Task CompleteAsync(ConversionHistory history, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({history.ClientId.ToString()}, 0))", ct);
        history.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        var expired = await db.History.Where(x => x.ClientId == history.ClientId && x.Status == "Completed")
            .OrderByDescending(x => x.CompletedAt).ThenByDescending(x => x.Id).Skip(5).ToListAsync(ct);
        foreach (var item in expired)
        {
            foreach (var key in new[] { item.InputStorageKey, item.StandardStorageKey, item.OutputStorageKey }.OfType<string>())
                db.PendingFileDeletions.Add(new() { StorageKey = key });
            db.History.Remove(item);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
// Deletions are durable and retried after restarts; files are never deleted before the DB commit.
public class ArchiveCleanupWorker(IServiceScopeFactory scopes, ILogger<ArchiveCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
                foreach (var item in await db.PendingFileDeletions.OrderBy(x => x.CreatedAt).Take(100).ToListAsync(stoppingToken))
                {
                    try { storage.Delete(item.StorageKey); db.PendingFileDeletions.Remove(item); }
                    catch (IOException ex) { logger.LogWarning(ex, "Suppression de fichier à réessayer {Id}", item.Id); }
                    catch (UnauthorizedAccessException ex) { logger.LogWarning(ex, "Accès au fichier à réessayer {Id}", item.Id); }
                }
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(ex, "Nettoyage des archives à réessayer"); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
