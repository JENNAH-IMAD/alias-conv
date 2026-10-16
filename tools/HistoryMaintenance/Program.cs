using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

if (args.Length != 3 || args[2] is not ("--apply" or "--verify")) throw new ArgumentException("Usage : HistoryMaintenance <dossier-projet> <sauvegarde.dump> --apply|--verify. Arrêter les conversions avant --apply.");
var root = Path.GetFullPath(args[0]);
var backup = Path.GetFullPath(args[1]);
if (!File.Exists(backup) || new FileInfo(backup).Length == 0) throw new InvalidOperationException("Sauvegarde PostgreSQL manquante.");
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = Path.Combine(root, "backend", "BankStatementConverter.API"), EnvironmentName = "Development" });
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton<IFileStorageService, LocalFileStorage>();
builder.Services.AddScoped<HistoryCleanupService>();
using var host = builder.Build();
using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var before = new { Statements = await db.StatementImports.CountAsync(), Clients = await db.Clients.CountAsync(), Accounts = await db.BankAccounts.CountAsync(), Templates = await db.ExportTemplates.CountAsync(), Profiles = await db.ExtractionProfiles.CountAsync(), Documents = await db.Documents.CountAsync() };
if (args[2] == "--verify")
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        Histories = new
        {
            StatementEvents = await db.StatementActivities.CountAsync(),
            ProfileEvents = await db.ExtractionProfileActivities.CountAsync(),
            ExportRecords = await db.ExportJobs.CountAsync()
        },
        Business = before
    }));
    return;
}
var result = await scope.ServiceProvider.GetRequiredService<HistoryCleanupService>().ClearAsync(default);
var after = new { Statements = await db.StatementImports.CountAsync(), Clients = await db.Clients.CountAsync(), Accounts = await db.BankAccounts.CountAsync(), Templates = await db.ExportTemplates.CountAsync(), Profiles = await db.ExtractionProfiles.CountAsync(), Documents = await db.Documents.CountAsync() };
if (!before.Equals(after)) throw new InvalidOperationException("Les comptes des données principales ont changé : vérifier la maintenance.");
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { result, Preserved = after }));
