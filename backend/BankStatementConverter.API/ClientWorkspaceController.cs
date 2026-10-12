using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BankStatementConverter.API;

[ApiController, Route("api/client-workspace/{clientId:guid}/archives")]
public class ClientWorkspaceController(DocumentService service, AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<PageResult<DocumentDto>> List(Guid clientId, CancellationToken ct, int page = 1, int pageSize = 20)
    {
        if (!await db.Clients.AnyAsync(x => x.Id == clientId, ct)) throw new AppException(404, "Client introuvable.");
        return await service.ListAsync(clientId, page, pageSize, ct);
    }
    [HttpGet("/api/client-workspace/{clientId:guid}/statements")]
    [HttpGet("/api/client-workspace/statements")]
    public async Task<IActionResult> Statements(CancellationToken ct,Guid? clientId=null,int page=1,int pageSize=20,bool unassigned=false,string? status=null,string? search=null)
    {
        Validation.Require(page>=1 && pageSize is >=1 and <=100,"Pagination invalide.");
        Validation.Require(status==null || status is "VALIDATED" or "NEEDS_REVIEW" or "EXPORTED","Statut invalide.");
        Validation.Require(search==null || search.Length<=150,"La recherche est limitée à 150 caractères.");
        if(clientId.HasValue && !await db.Clients.AnyAsync(c=>c.Id==clientId.Value,ct)) throw new AppException(404,"Client introuvable.");
        var query=db.StatementImports.AsNoTracking().AsQueryable();
        if(clientId.HasValue) query=query.Where(s=>s.ClientId==clientId.Value);
        if(unassigned) query=query.Where(s=>s.ClientId==null);
        if(status!=null) query=query.Where(s=>s.Status==status);
        if(!string.IsNullOrWhiteSpace(search))
        {
            var term=search.Trim().ToLower();
            query=query.Where(s=>s.BankName.ToLower().Contains(term) || (s.SourceFileName!=null && s.SourceFileName.ToLower().Contains(term)) ||
                (s.StatementNumber!=null && s.StatementNumber.ToLower().Contains(term)) || db.Clients.Any(c=>c.Id==s.ClientId && c.Name.ToLower().Contains(term)));
        }
        var items=await query.OrderByDescending(s=>s.CreatedAt).ThenBy(s=>s.Id).Skip((page-1)*pageSize).Take(pageSize).Select(s=>new {
            s.Id,s.ClientId,s.BankAccountId,s.BankName,s.StatementNumber,s.Currency,s.SourceFileName,s.PeriodStart,s.PeriodEnd,s.Status,s.Version,s.TransactionCount,s.CreatedAt,s.UpdatedAt,s.ImportedByName,s.ModifiedByName,
            clientName=db.Clients.Where(c=>c.Id==s.ClientId).Select(c=>c.Name).FirstOrDefault(),
            accountName=db.BankAccounts.Where(a=>a.Id==s.BankAccountId).Select(a=>a.AccountName).FirstOrDefault(),
            accountNumber=db.BankAccounts.Where(a=>a.Id==s.BankAccountId).Select(a=>a.AccountNumber).FirstOrDefault(),
            exportCount=db.ExportJobs.Count(e=>e.StatementId==s.Id),
            lastExportAt=db.ExportJobs.Where(e=>e.StatementId==s.Id).Select(e=>(DateTime?)e.CreatedAt).Max()
        }).ToListAsync(ct);
        return Ok(new {items,total=await query.CountAsync(ct),page,pageSize});
    }
    [HttpGet("/api/client-workspace/history")]
    public async Task<IActionResult> History(CancellationToken ct,Guid? statementId=null,Guid? clientId=null,Guid? bankAccountId=null,int page=1,int pageSize=20) {
        Validation.Require(page>=1 && pageSize is >=1 and <=100,"Pagination invalide.");
        var query=db.StatementActivities.AsNoTracking().AsQueryable();
        if(statementId.HasValue)query=query.Where(x=>x.StatementId==statementId);
        if(clientId.HasValue)query=query.Where(x=>x.ClientId==clientId);
        if(bankAccountId.HasValue)query=query.Where(x=>x.BankAccountId==bankAccountId);
        var items=await query.OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).Skip((page-1)*pageSize).Take(pageSize).ToListAsync(ct);
        Response.Headers.CacheControl="no-store";
        return Ok(new{items,total=await query.CountAsync(ct),page,pageSize});
    }
}
