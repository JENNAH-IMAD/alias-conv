using System.Security.Claims;
﻿using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BankStatementConverter.API;
public class EditDocumentForm { public Guid ClientId { get; set; } public Guid BankAccountId { get; set; } public IFormFile? File { get; set; } }
[ApiController, Authorize, Route("api/documents")]
public class DocumentManagementController(AppDbContext db, IFileStorageService storage, IConfiguration config) : ControllerBase
{
    [HttpPut("{id:guid}"), Consumes("multipart/form-data")]
    public async Task<IActionResult> Edit(Guid id, [FromForm] EditDocumentForm input, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var item=await db.History.FromSqlInterpolated($"SELECT * FROM \"History\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new AppException(404,"Relevé introuvable.");
        if(item.Status=="Processing")throw new AppException(409,"Attendez la fin du traitement avant de modifier ce relevé.");
        if(!await db.BankAccounts.AnyAsync(x=>x.Id==input.BankAccountId&&x.ClientId==input.ClientId,ct))throw new AppException(400,"Compte bancaire non associé au client.");
        string? replacement=null;
        try {
            if(input.File is not null){
                var limit=config.GetValue<long>("Storage:MaxBytes",20*1024*1024);
                if(input.File.Length<=0||input.File.Length>limit||input.File.ContentType!="application/pdf"||!Path.GetExtension(input.File.FileName).Equals(".pdf",StringComparison.OrdinalIgnoreCase))throw new AppException(400,"Choisissez un PDF de 20 Mo maximum.");
                using var buffer=new MemoryStream();await using var source=input.File.OpenReadStream();var chunk=new byte[81920];int count;
                while((count=await source.ReadAsync(chunk,ct))>0){if(buffer.Length+count>limit)throw new AppException(400,"PDF trop volumineux.");await buffer.WriteAsync(chunk.AsMemory(0,count),ct);}
                if(buffer.Length<5||System.Text.Encoding.ASCII.GetString(buffer.GetBuffer(),0,5)!="%PDF-")throw new AppException(400,"Signature PDF invalide.");
                buffer.Position=0;replacement=await storage.SaveAsync(buffer,"input","pdf",ct);
                if(item.InputStorageKey!=null)db.PendingFileDeletions.Add(new(){StorageKey=item.InputStorageKey});
                item.InputStorageKey=replacement;item.InputFileName=Path.GetFileName(input.File.FileName.Replace('\\','/'));
            }
            foreach(var key in new[]{item.StandardStorageKey,item.OutputStorageKey}.OfType<string>())db.PendingFileDeletions.Add(new(){StorageKey=key});
            item.ClientId=input.ClientId;item.BankAccountId=input.BankAccountId;item.Status="Pending";item.ErrorMessage=null;item.CompletedAt=null;item.BankStatementTemplateId=null;item.ExportTemplateId=null;
            item.StandardStorageKey=null;item.OutputStorageKey=null;item.OutputFileName=null;item.ExportLabel=null;item.OutputDelimiter=null;item.OutputEncoding=null;item.TransactionCount=0;
            var actorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var actor = await db.Users.AsNoTracking().SingleAsync(x => x.Id == actorId, ct);
            item.ModifiedByUserId = actor.Id; item.ModifiedByName = actor.Username; item.ModifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return NoContent();
        } catch {if(replacement!=null)storage.Delete(replacement);throw;}
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var item=await db.History.FromSqlInterpolated($"SELECT * FROM \"History\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new AppException(404,"Relevé introuvable.");
        if(item.Status=="Processing")throw new AppException(409,"Attendez la fin du traitement avant de supprimer ce relevé.");
        foreach(var key in new[]{item.InputStorageKey,item.StandardStorageKey,item.OutputStorageKey}.OfType<string>())db.PendingFileDeletions.Add(new(){StorageKey=key});
        db.History.Remove(item);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return NoContent();
    }
}
