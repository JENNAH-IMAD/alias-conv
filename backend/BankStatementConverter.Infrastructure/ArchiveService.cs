using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace BankStatementConverter.Infrastructure;

// Les suppressions sont persistées et reprises après redémarrage, uniquement après validation de la transaction.
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
