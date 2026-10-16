using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BankStatementConverter.API;

[ApiController, Route("api/dashboard")]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var start = today.AddDays(-89);
        var end = today.AddDays(1);
        var canReadDocuments = User.HasClaim("permission", "documents.read");
        var statuses = await db.StatementImports.AsNoTracking().GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var clients = await db.Clients.CountAsync(ct);
        var banks = await db.Banks.CountAsync(ct);
        var accounts = await db.BankAccounts.CountAsync(ct);
        var extracts = await db.StatementImports.FromSqlRaw("SELECT * FROM \"StatementImports\" WHERE \"NormalizedJson\"->>'conversionType' = 'EXTRACT'").CountAsync(ct);
        var exportCounts = await db.Database.SqlQuery<DashboardExportCounts>($"""
            SELECT COUNT(*)::int AS "Total",
              (COUNT(*) FILTER (WHERE COALESCE(e."TemplateSnapshotJson"->>'conversionType',
                CASE WHEN e."TemplateSnapshotJson" ? 'extract' OR s."NormalizedJson"->>'conversionType' = 'EXTRACT'
                  THEN 'EXTRACT' ELSE 'STATEMENT' END) = 'EXTRACT'))::int AS "Extracts"
            FROM "ExportJobs" e LEFT JOIN "StatementImports" s ON s."Id" = e."StatementId"
            """).SingleAsync(ct);
        var exports = exportCounts.Total;
        var unassigned = await db.StatementImports.CountAsync(x => x.BankAccountId == null, ct);
        // Import events survive source retention.
        var importsByDay = canReadDocuments ? await db.StatementActivities.AsNoTracking()
            .Where(x => x.Action == "IMPORTED" && x.CreatedAt >= start && x.CreatedAt < end)
            .GroupBy(x => x.CreatedAt.Date).Select(g => new { Day = g.Key, Count = g.Count() }).ToListAsync(ct) : [];
        var exportsByDay = canReadDocuments ? await db.ExportJobs.AsNoTracking()
            .Where(x => x.CreatedAt >= start && x.CreatedAt < end)
            .GroupBy(x => x.CreatedAt.Date).Select(g => new { Day = g.Key, Count = g.Count() }).ToListAsync(ct) : [];
        var importCounts = importsByDay.ToDictionary(x => x.Day, x => x.Count);
        var dailyExportCounts = exportsByDay.ToDictionary(x => x.Day, x => x.Count);
        var activity = canReadDocuments ? Enumerable.Range(0, 90).Select(offset =>
        {
            var day = start.AddDays(offset);
            return new
            {
                Date = day.ToString("yyyy-MM-dd"),
                Imports = importCounts.GetValueOrDefault(day),
                Exports = dailyExportCounts.GetValueOrDefault(day)
            };
        }).ToArray() : [];
        var recent = canReadDocuments ? await (
            from statement in db.StatementImports.AsNoTracking()
            join client in db.Clients on statement.ClientId equals (Guid?)client.Id into clientGroup
            from client in clientGroup.DefaultIfEmpty()
            join account in db.BankAccounts on statement.BankAccountId equals (Guid?)account.Id into accountGroup
            from account in accountGroup.DefaultIfEmpty()
            orderby statement.CreatedAt descending, statement.Id
            select new
            {
                statement.Id,
                statement.SourceFileName,
                statement.BankName,
                statement.StatementNumber,
                statement.ClientId,
                ClientName = client == null ? null : client.Name,
                AccountName = account == null ? null : account.AccountName,
                statement.Status,
                statement.TransactionCount,
                statement.CreatedAt
            }
        ).Take(5).ToListAsync(ct) : [];
        return Ok(new
        {
            Clients = clients,
            Banks = banks,
            Accounts = accounts,
            Statements = statuses.Sum(x => x.Count),
            Exports = exports,
            Unassigned = unassigned,
            StatementDocuments = statuses.Sum(x => x.Count) - extracts,
            Extracts = extracts,
            StatementExports = exports - exportCounts.Extracts,
            ExtractExports = exportCounts.Extracts,
            PendingReview = statuses.Where(x => x.Status == "NEEDS_REVIEW").Sum(x => x.Count),
            Statuses = statuses,
            Activity = activity,
            Recent = recent,
            UpdatedAt = DateTime.UtcNow
        });
    }
}
public sealed class DashboardExportCounts
{
    public int Total
    {
        get; set;
    }
    public int Extracts
    {
        get; set;
    }
}
