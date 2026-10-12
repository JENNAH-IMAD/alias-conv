using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BankStatementConverter.Infrastructure.Exports;
public sealed class PdfStatementImportService(AppDbContext db, IFileStorageService storage, IAutomaticPdfExtraction extractor, StatementImportService imports, IConfiguration config, IProcessingProgress? progress=null)
{
    public async Task<StatementDetail> ImportAsync(Stream source, string fileName, long length, Guid bankId, string currency, CancellationToken ct, Guid? clientId=null, Guid? bankAccountId=null)
    {
        var limit = config.GetValue<long>("Storage:MaxBytes", 20 * 1024 * 1024);
        Validation.Require(length > 0 && length <= limit && string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase), "Choisissez un PDF de 20 Mo maximum.");
        Validation.Text(Path.GetFileName(fileName.Replace('\\','/')), "Nom du fichier", 2000);
        Validation.Require(currency.Length == 3 && currency.All(char.IsAsciiLetterUpper), "La devise doit comporter trois lettres majuscules.");
        var bank = await db.Banks.AsNoTracking().SingleOrDefaultAsync(b => b.Id == bankId, ct) ?? throw new AppException(400, "Choisissez la banque du relevé.");
        Validation.Require(clientId.HasValue==bankAccountId.HasValue,"Choisissez ensemble le client et son compte bancaire.");
        if(bankAccountId.HasValue)
            Validation.Require(await db.BankAccounts.AnyAsync(a=>a.Id==bankAccountId && a.ClientId==clientId && a.BankId==bankId && a.Currency==currency,ct),"Le compte doit correspondre au client, à la banque et à la devise sélectionnés.");
        using var buffer = new MemoryStream(); var chunk = new byte[81920]; int count;
        while ((count = await source.ReadAsync(chunk, ct)) > 0)
        {
            Validation.Require(buffer.Length + count <= limit, "PDF trop volumineux.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
        }
        Validation.Require(buffer.Length >= 5 && Encoding.ASCII.GetString(buffer.GetBuffer(), 0, 5) == "%PDF-", "Signature PDF invalide.");
        var extracted = await extractor.ExtractAsync(buffer.ToArray(), bank.Name, bank.Code, currency, ct);
        progress?.Report("saving",90);
        buffer.Position = 0; string? saved = null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            saved = await storage.SaveAsync(buffer, "input", "pdf", ct);
            var detail = await imports.CreateExtractedAsync(extracted, ct,
                Path.GetFileName(fileName.Replace('\\', '/')), saved, clientId, bankAccountId);
            if(bankAccountId.HasValue) await imports.RetainLatestAsync(bankAccountId.Value,ct);
            await tx.CommitAsync(ct);
            return detail;
        }
        catch { if (saved != null) storage.Delete(saved); throw; }
    }
    public async Task<(Stream Content, string FileName)> OriginalAsync(Guid id, CancellationToken ct)
    {
        var item = await db.StatementImports.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new AppException(404, "Relevé introuvable.");
        if (item.SourceStorageKey == null) throw new AppException(404, "Ce relevé n’a pas de PDF original associé.");
        return (storage.Open(item.SourceStorageKey), item.SourceFileName ?? "releve.pdf");
    }
}
