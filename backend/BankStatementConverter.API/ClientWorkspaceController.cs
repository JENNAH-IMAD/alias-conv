using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BankStatementConverter.API;

// Like the client directory, this workspace is shared by authenticated application operators.
[ApiController, Authorize, Route("api/client-workspace/{clientId:guid}/archives")]
public class ClientWorkspaceController(AppDbContext db, IFileStorageService storage) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid clientId, CancellationToken ct, bool includePending = false)
    {
        if (!await db.Clients.AnyAsync(x => x.Id == clientId, ct)) throw new AppException(404, "Client introuvable.");
        return Ok(await db.History.AsNoTracking().Where(x => x.ClientId == clientId && (includePending || x.Status == "Completed"))
            .OrderByDescending(x => x.CompletedAt).ThenByDescending(x => x.CreatedAt).Take(includePending ? 1000 : 5)
            .Select(x => new { x.Id, x.ClientId, x.BankAccountId, x.BankStatementTemplateId, x.ExportTemplateId, x.InputFileName, x.CompletedAt, x.CreatedAt, x.Status, x.ErrorMessage, BankId = x.BankAccount.BankId, x.TransactionCount, Bank = x.BankAccount.Bank.Name,
                Account = x.BankAccount.AccountNumber, AccountName = x.BankAccount.AccountName, x.OutputFileName, x.ExportLabel,
                x.ProcessedByName, x.ProcessedAt, x.ModifiedByName, x.ModifiedAt,
                HasOriginal = x.InputStorageKey != null, HasStandard = x.StandardStorageKey != null, HasExport = x.OutputStorageKey != null }).ToListAsync(ct));
    }
    [HttpGet("{id:guid}/preview/{kind}")]
    public async Task<IActionResult> Preview(Guid clientId, Guid id, string kind, CancellationToken ct)
    {
        if(kind is not ("standard" or "export")) throw new AppException(400,"Aperçu inconnu.");
        var item=await db.History.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.ClientId==clientId,ct) ?? throw new AppException(404,"Relevé introuvable.");
        var key=kind=="standard"?item.StandardStorageKey:item.OutputStorageKey;
        if(key==null)throw new AppException(404,"Fichier indisponible.");
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var stream=storage.Open(key);
        using var reader=new StreamReader(stream,System.Text.Encoding.GetEncoding(kind=="standard"?"utf-8":item.OutputEncoding??"utf-8"));
        Response.Headers.CacheControl="no-store";
        string? text = null;
        bool textTruncated = false;
        if(kind=="export"&&item.OutputFileName?.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)==true)
        {
            using var rawStream = storage.Open(key);
            using var rawReader = new StreamReader(rawStream, System.Text.Encoding.GetEncoding(item.OutputEncoding ?? "utf-8"));
            var buffer = new char[100001];
            var length = await rawReader.ReadBlockAsync(buffer, ct);
            text = new string(buffer, 0, Math.Min(length, 100000));
            textTruncated = length > 100000;
        }
        using var csv=new CsvHelper.CsvReader(reader,new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture){HasHeaderRecord=false,WhiteSpaceChars=[],Delimiter=kind=="standard"?";":item.OutputDelimiter??";"});
        var rows=new List<string[]>();
        while(rows.Count<501 && await csv.ReadAsync()) rows.Add(csv.Parser.Record!.ToArray());
        return Ok(new { rows=rows.Take(500), text, textTruncated, truncated=rows.Count>500 });
    }
    [HttpGet("{id:guid}/files/{kind}")]
    public async Task<IActionResult> Download(Guid clientId, Guid id, string kind, CancellationToken ct, bool excel = false)
    {
        var item = await db.History.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct)
            ?? throw new AppException(404, "Archive introuvable.");
        var (key, name, type) = kind switch {
            "original" => (item.InputStorageKey, item.InputFileName, "application/pdf"),
            "standard" => (item.StandardStorageKey, $"standard_{id:N}.csv", "text/csv; charset=utf-8"),
            "export" => (item.OutputStorageKey, item.OutputFileName ?? "export.csv", "application/octet-stream"),
            _ => throw new AppException(400, "Type de fichier inconnu.")
        };
        if (key is null) throw new AppException(404, "Ce fichier n’est pas disponible pour cet ancien traitement.");
        Response.Headers.CacheControl = "no-store";
        if (kind != "original")
        {
            using var source = storage.Open(key);
            var encoding = kind == "standard" ? "utf-8" : item.OutputEncoding ?? "utf-8";
            var delimiter = kind == "standard" ? ";" : item.OutputDelimiter ?? ";";
            var content = ExportDownload.WithoutHeader(source, encoding, delimiter, item.TransactionCount);
            if (!excel) return File(content, type, name);
            using var operations = new MemoryStream(content);
            return File(ExcelDownload.Create(operations, encoding, delimiter), ExcelDownload.ContentType, Path.GetFileNameWithoutExtension(name) + ".xlsx");
        }
        return File(storage.Open(key), type, name);
    }
}


