using System.Linq.Expressions;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
namespace BankStatementConverter.Infrastructure;

public class LocalFileStorage(IConfiguration config) : IFileStorageService
{
    private readonly string root = Path.GetFullPath(config["Storage:Root"] ?? "uploads");
    private string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new AppException(400, "Chemin de fichier invalide.");
        return path;
    }
    public async Task<string> SaveAsync(Stream stream, string category, string extension, CancellationToken ct)
    {
        Validation.Require(category is "input" or "output" && extension is "pdf" or "csv" or "txt", "Type de stockage invalide.");
        var key = $"{category}/{Guid.NewGuid():N}.{extension}";
        var path = Resolve(key); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try { await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true); await stream.CopyToAsync(file, ct); }
        catch { File.Delete(path); throw; }
        return key;
    }
    public Stream Open(string key) => File.OpenRead(Resolve(key));
    public void Delete(string key) => File.Delete(Resolve(key));
}
public class HistoryService(AppDbContext db)
{
    public static Expression<Func<ConversionHistory, HistoryDto>> Projection => x => new(x.Id, x.ClientId, x.BankAccountId, x.Client.Name, x.BankAccount.Bank.Name, x.InputFileName, x.OutputFileName, x.Status, x.ErrorMessage, x.TransactionCount, x.CreatedAt, x.CompletedAt, x.BankAccount.AccountName, x.BankAccount.AccountNumber, x.ProcessedByName, x.ProcessedAt, x.ModifiedByName, x.ModifiedAt);
    public IQueryable<ConversionHistory> Accessible(Guid userId, bool admin) => db.History.AsNoTracking().Where(x => admin || x.UserId == userId);
    public async Task<HistoryDto> GetAsync(Guid id, Guid userId, bool admin, CancellationToken ct) => await Accessible(userId, admin).Where(x => x.Id == id).Select(Projection).SingleOrDefaultAsync(ct) ?? throw new AppException(404, "Conversion introuvable.");
    public async Task<PageResult<HistoryDto>> ListAsync(Guid userId, bool admin, int page, int pageSize, string? search, string? status, CancellationToken ct)
    {
        Validation.Require(page >= 1 && pageSize is >= 1 and <= 100, "Pagination invalide.");
        var q = Accessible(userId, admin);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(x => x.Client.Name.Contains(search) || x.BankAccount.Bank.Name.Contains(search) || x.InputFileName.Contains(search));
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        return new(await q.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(Projection).ToListAsync(ct), await q.CountAsync(ct), page, pageSize);
    }
    public async Task<object> DashboardAsync(Guid userId, bool admin, CancellationToken ct)
    {
        var q = Accessible(userId, admin);
        return new { clients = await db.Clients.CountAsync(ct), banks = await db.Banks.CountAsync(ct), conversions = await q.CountAsync(ct), completed = await q.CountAsync(x => x.Status == "Completed", ct), failed = await q.CountAsync(x => x.Status == "Failed", ct), recent = await q.OrderByDescending(x => x.CreatedAt).Take(5).Select(Projection).ToListAsync(ct) };
    }
}
public class ConversionService(AppDbContext db, IPdfExtractionService extraction, IEnumerable<IBankStatementParser> parsers, IEnumerable<ITransactionExporter> exporters, IFileStorageService storage, IConfiguration config, ILogger<ConversionService> logger) : IConversionService
{
    public async Task<ConversionResult> ConvertAsync(ConversionInput input, Guid userId, CancellationToken ct) {
        var imported = await ImportAsync(input, userId, ct);
        return await ProcessAsync(imported.Id, input.BankStatementTemplateId, input.ExportTemplateId, userId, ct);
    }
    public async Task<HistoryDto> ImportAsync(ConversionInput input, Guid userId, CancellationToken ct)
    {
        var limit = config.GetValue<long>("Storage:MaxBytes", 20 * 1024 * 1024);
        Validation.Require(input.Length > 0 && input.Length <= limit, $"PDF vide ou supérieur à {limit / 1024 / 1024} Mo.");
        Validation.Require(Path.GetExtension(input.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) && input.ContentType == "application/pdf", "Un fichier PDF est requis.");
        var account = await db.BankAccounts.Include(x => x.Client).Include(x => x.Bank).SingleOrDefaultAsync(x => x.Id == input.BankAccountId && x.ClientId == input.ClientId, ct) ?? throw new AppException(400, "Compte bancaire non associé au client.");
        // Bounded copy also checks the actual length, independent of the declared length.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920]; int count;
        while ((count = await input.File.ReadAsync(chunk, ct)) > 0) { Validation.Require(buffer.Length + count <= limit, "PDF trop volumineux."); await buffer.WriteAsync(chunk.AsMemory(0, count), ct); }
        Validation.Require(buffer.Length >= 5 && System.Text.Encoding.ASCII.GetString(buffer.GetBuffer(), 0, 5) == "%PDF-", "Signature PDF invalide."); buffer.Position = 0;
        var h = new ConversionHistory { UserId = userId, ClientId = input.ClientId, BankAccountId = account.Id, InputFileName = Path.GetFileName(input.FileName.Replace('\\', '/')), Status = "Pending" };
        h.InputStorageKey = await storage.SaveAsync(buffer, "input", "pdf", ct);
        try { db.History.Add(h); await db.SaveChangesAsync(ct); }
        catch { storage.Delete(h.InputStorageKey); throw; }
        return await new HistoryService(db).GetAsync(h.Id, userId, false, ct);
    }
    public async Task<ConversionResult> ProcessAsync(Guid id, Guid profileId, Guid exportId, Guid actorId, CancellationToken ct)
    {
        var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, ct) ?? throw new AppException(401, "Session invalide.");
        var h = await db.History.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException(404, "Relevé introuvable.");
        if (h.InputStorageKey == null) throw new AppException(409, "PDF original indisponible.");
        var account = await db.BankAccounts.Include(x=>x.Client).Include(x=>x.Bank).SingleAsync(x=>x.Id==h.BankAccountId,ct);
        var template = await db.StatementTemplates.SingleOrDefaultAsync(x=>x.Id==profileId && (x.BankId==account.BankId || (x.BankId==null && x.ParserKey=="multibank-auto")) && x.IsActive,ct) ?? throw new AppException(400,"Profil absent, inactif ou incompatible avec la banque.");
        var export = await db.ExportTemplates.Include(x=>x.Fields).SingleOrDefaultAsync(x=>x.Id==exportId && x.IsActive,ct) ?? throw new AppException(400,"Modèle d’export absent ou inactif.");
        var parser = template.ParserKey == "multibank-auto" ? null : parsers.SingleOrDefault(x=>x.Key==template.ParserKey) ?? throw new AppException(400,"Parser indisponible.");
        var exporter = exporters.Single(x=>x.Type==export.Type);
        var claimed = await db.History.Where(x=>x.Id==id && x.UpdatedAt==h.UpdatedAt && (x.Status=="Pending" || x.Status=="Failed")).ExecuteUpdateAsync(u=>u.SetProperty(x=>x.Status,"Processing").SetProperty(x=>x.ErrorMessage,(string?)null),ct);
        if(claimed!=1) throw new AppException(409,"Ce relevé est déjà traité ou en cours de traitement.");
        h.Status="Processing"; h.ErrorMessage=null; h.BankStatementTemplateId=profileId; h.ExportTemplateId=exportId;
        try
        {
            await db.SaveChangesAsync(ct);
            using var buffer = storage.Open(h.InputStorageKey);
            logger.LogInformation("Extraction PDF {ConversionId}", h.Id);
            IReadOnlyList<BankTransaction> transactions;
            if (template.ParserKey == "multibank-auto") transactions = new AutomaticStatementParser().Parse(buffer, ct);
            else
            {
                var text = await extraction.ExtractAsync(buffer, ct);
                transactions = parser!.Parse(text, template);
            }
            foreach (var t in transactions) { t.Account = account.AccountCode; t.Journal = account.Journal; }
            var standardTemplate = new ExportTemplate { Type = "CUSTOMCSV", Encoding = "utf-8", NumberCulture = "en-US", Delimiter = ";", IncludeHeader = true };
            var columns = new[] { "Date", "ValueDate", "Reference", "Description", "Debit", "Credit", "Balance", "Amount", "Direction", "Account", "Journal", "Analytic" };
            standardTemplate.Fields = columns.Select((name, index) => new ExportTemplateField { FieldName = name, SourceField = name, Position = index, Format = name is "Date" or "ValueDate" ? "yyyy-MM-dd" : null }).ToList();
            using var standard = new MemoryStream(new CustomCsvExporter().Export(transactions, standardTemplate));
            h.StandardStorageKey = await storage.SaveAsync(standard, "output", "csv", ct);
            h.OutputDelimiter = export.Delimiter; h.OutputEncoding = export.Encoding;
            h.ExportLabel = export.Name + " · " + export.Type;
            using var output = new MemoryStream(exporter.Export(transactions, export));
            h.OutputStorageKey = await storage.SaveAsync(output, "output", export.FileExtension, ct);
            h.OutputFileName = ExportFileName.Create(account.Bank.Name, account.AccountName, DateTime.UtcNow, h.Id, export.FileExtension);
            h.ProcessedByUserId = actor.Id; h.ProcessedByName = actor.Username; h.ProcessedAt = DateTime.UtcNow; h.TransactionCount = transactions.Count; h.Status = "Completed"; h.CompletedAt = DateTime.UtcNow;
            await new ArchiveService(db).CompleteAsync(h, ct); logger.LogInformation("Export terminé {ConversionId}, {Count} opérations", h.Id, transactions.Count);
            return new(new HistoryDto(h.Id, h.ClientId, h.BankAccountId, account.Client.Name, account.Bank.Name, h.InputFileName, h.OutputFileName, h.Status, h.ErrorMessage, h.TransactionCount, h.CreatedAt, h.CompletedAt, account.AccountName, account.AccountNumber, h.ProcessedByName, h.ProcessedAt, h.ModifiedByName, h.ModifiedAt), transactions);
        }
        catch (Exception ex)
        {
            db.ChangeTracker.Clear();
            db.Attach(h);
            if (h.StandardStorageKey != null) { storage.Delete(h.StandardStorageKey); h.StandardStorageKey = null; }

            if (h.OutputStorageKey != null) { storage.Delete(h.OutputStorageKey); h.OutputStorageKey = null; h.OutputFileName = null; }
            h.Status = "Failed"; h.ErrorMessage = ex is AppException ? ex.Message[..Math.Min(ex.Message.Length, 1900)] : ex is OperationCanceledException ? "Conversion annulée." : "Erreur de traitement. Consultez les journaux serveur."; h.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None); logger.LogWarning(ex, "Conversion échouée {ConversionId}", h.Id); throw;
        }
    }
    public async Task<(Stream Content, string Name)> DownloadAsync(Guid id, Guid userId, bool admin, CancellationToken ct)
    {
        var h = await db.History.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && (admin || x.UserId == userId), ct) ?? throw new AppException(404, "Conversion introuvable.");
        if (h.Status != "Completed" || h.OutputStorageKey == null) throw new AppException(409, "Fichier non disponible.");
        return (storage.Open(h.OutputStorageKey), h.OutputFileName!);
    }
}
