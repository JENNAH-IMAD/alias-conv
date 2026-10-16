using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace BankStatementConverter.API;

[ApiController, Route("api/statement-imports")]
public sealed class StatementImportsController(StatementImportService service, PdfStatementImportService pdf, RequestProcessingProgress progress, ProcessingProgressStore progressStore) : ControllerBase
{
    [HttpPost("delete-selection")]
    public async Task<IActionResult> DeleteSelection(List<StatementVersionToDelete> items, CancellationToken ct)
    {
        await service.DeleteAsync(items, ct);
        return NoContent();
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] int version, CancellationToken ct)
    {
        await service.DeleteAsync([new(id, version)], ct);
        return NoContent();
    }
    [HttpGet] public Task<PageResult<StatementSummary>> List(CancellationToken ct, int page = 1, int pageSize = 20) => service.ListAsync(page, pageSize, ct);
    [HttpGet("{id:guid}")] public Task<StatementDetail> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost]
    public async Task<IActionResult> Create(BankStatement statement, CancellationToken ct)
    {
        var result = await service.CreateAsync(statement, ct);
        return CreatedAtAction(nameof(Get), new
        {
            id = result.Id
        }, result);
    }
    [HttpPost("pdf"), Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportPdf([FromForm] PdfStatementForm input, CancellationToken ct)
    {
        if (input.File == null)
            throw new AppException(400, "Choisissez un fichier PDF.");
        if (input.ProcessingId == Guid.Empty)
            throw new AppException(400, "Identifiant de traitement invalide.");
        if (input.ProcessingId.HasValue)
            progress.Start(input.ProcessingId.Value, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {
            await using var source = input.File.OpenReadStream();
            var result = await pdf.ImportAsync(source, input.File.FileName, input.File.Length, input.BankId, input.Currency, ct, input.ClientId, input.BankAccountId, input.ConversionType);
            progress.Report("done", 100);
            return CreatedAtAction(nameof(Get), new
            {
                id = result.Id
            }, result);
        }
        catch (OperationCanceledException) { progress.Report("cancelled", 0); throw; }
        catch { progress.Report("failed", 0); throw; }
    }
    [HttpGet("processing/{id:guid}")]
    public ProcessingStatus Processing(Guid id)
    {
        Response.Headers.CacheControl = "no-store";
        return progressStore.Get(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
    [HttpGet("{id:guid}/original")]
    public async Task<IActionResult> Original(Guid id, CancellationToken ct, bool download = false)
    {
        var original = await pdf.OriginalAsync(id, ct);
        Response.Headers.CacheControl = "no-store";
        return download ? File(original.Content, "application/pdf", original.FileName) : File(original.Content, "application/pdf");
    }
    [HttpPut("{id:guid}")] public Task<StatementDetail> Update(Guid id, [FromQuery] int version, BankStatement statement, CancellationToken ct) => service.UpdateAsync(id, version, statement, ct);
    [HttpPost("{id:guid}/extracts")]
    public Task<StatementDetail> SaveExtract(Guid id, SaveStatementExtractRequest input, CancellationToken ct)
        => service.SaveExtractAsync(id, input.StatementVersion, input.FilterText ?? "", ct, input.ExcludeText ?? "", input.Statement);
    [HttpDelete("{id:guid}/extracts/{extractId:guid}")]
    public Task<StatementDetail> ResetExtract(Guid id, Guid extractId, [FromQuery] int version, CancellationToken ct)
        => service.ResetExtractAsync(id, extractId, version, ct);
    [HttpGet("{id:guid}/extracts/{extractId:guid}")]
    public async Task<StatementExtract> GetExtract(Guid id, Guid extractId, CancellationToken ct)
    {
        var detail = await service.GetAsync(id, ct);
        Response.Headers.CacheControl = "no-store";
        return detail.Statement.Extracts.SingleOrDefault(x => x.Id == extractId) ?? throw new AppException(404, "Extrait introuvable.");
    }
    [HttpGet("{id:guid}/extracts/{extractId:guid}/file")]
    public async Task<IActionResult> DownloadExtract(Guid id, Guid extractId, CancellationToken ct)
    {
        var detail = await service.GetAsync(id, ct);
        var extract = detail.Statement.Extracts.SingleOrDefault(x => x.Id == extractId) ?? throw new AppException(404, "Extrait introuvable.");
        Response.Headers.CacheControl = "no-store";
        return File(StatementExtracts.Csv(extract), "text/csv; charset=utf-8", $"extrait_{extractId:N}.csv");
    }
    [HttpPut("{id:guid}/association")] public Task<StatementDetail> Associate(Guid id, StatementAssociation input, CancellationToken ct) => service.AssociateAsync(id, input, ct);
    [HttpGet("{id:guid}/standard")]
    public async Task<IActionResult> StandardDownload(Guid id, CancellationToken ct)
    {
        var detail = await service.GetAsync(id, ct);
        if (detail.Status != "VALIDATED")
            throw new AppException(422, "Validez le relevé avant de télécharger le CSV standard.");
        Response.Headers.CacheControl = "no-store";
        return File(StandardStatementCsv.Generate(detail.Statement), "text/csv; charset=utf-8", $"releve_{id:N}_standard.csv");
    }
    [HttpGet("{id:guid}/normalized-pdf")]
    public async Task<IActionResult> NormalizedPdf(Guid id, [FromQuery] int version, CancellationToken ct, [FromQuery] bool preview = false, [FromQuery] Guid? extractId = null)
    {
        var detail = await service.GetAsync(id, ct);
        Response.Headers.CacheControl = "no-store";
        if (detail.Version != version)
            throw new AppException(409, "Le relevé a été modifié. Actualisez l’aperçu avant de télécharger le PDF.");
        var content = NormalizedStatementPdf.Generate(StatementExtracts.ForExport(detail.Statement, extractId), detail.Status == "VALIDATED");
        return preview ? File(content, "application/pdf") : File(content, "application/pdf", $"{(extractId.HasValue ? "extrait" : "releve")}_{id:N}_structure.pdf");
    }
    [HttpGet("{id:guid}/conversion-report")]
    public async Task<IActionResult> ConversionReport(Guid id, [FromQuery] int version, CancellationToken ct, [FromQuery] Guid? extractId = null)
    {
        var detail = await service.GetAsync(id, ct);
        if (detail.Version != version)
            throw new AppException(409, "Le relevé a été modifié. Actualisez l’aperçu avant de télécharger le rapport.");
        var raw = await service.ExtractionAsync(id, ct);
        var s = StatementExtracts.ForExport(detail.Statement, extractId);
        var debit = s.Transactions.Sum(t => t.Debit ?? 0);
        var credit = s.Transactions.Sum(t => t.Credit ?? 0);
        var validation = new StatementValidator().Validate(s);
        var report = new
        {
            extractId,
            status = detail.Status,
            method = raw.Method,
            pages = raw.Pages.Select(p => p.Number),
            operations = s.Transactions.Count,
            totalDebit = debit,
            totalCredit = credit,
            s.OpeningBalance,
            s.ClosingBalance,
            balanceDifference = s.OpeningBalance.HasValue && s.ClosingBalance.HasValue ? s.ClosingBalance - (s.OpeningBalance + credit - debit) : null,
            validation,
            detail.Warnings,
            detail.Errors,
            movements = s.Transactions.Select(t => new { t.Sequence, t.SourcePages }),
            note = "Les totaux calculés et l'équilibre ne prouvent pas l'exhaustivité. Comparez les opérations et les données incertaines au PDF original."
        };
        Response.Headers.CacheControl = "no-store";
        return File(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(report, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true }), "application/json", $"releve_{id:N}_controles.json");
    }
    [HttpGet("{id:guid}/extraction")]
    public async Task<PdfTextDocument> Extraction(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return await service.ExtractionAsync(id, ct);
    }
    [HttpPost("{id:guid}/adapt")]
    public Task<StatementDetail> Adapt(Guid id, AdaptExtractionRequest request, [FromServices] StatementPdfExtractor normalizer, CancellationToken ct)
        => service.AdaptAsync(id, request, normalizer, ct);
    [HttpPost("{id:guid}/prepare")]
    public Task<PreparedStatementExport> Prepare(Guid id, PrepareStatementExportRequest request, [FromServices] StatementPdfExtractor normalizer, CancellationToken ct)
        => service.PrepareAsync(id, request, normalizer, ct);
    [HttpPost("{id:guid}/complete-metadata")]
    public Task<StatementDetail> CompleteMetadata(Guid id, StatementVersionRequest request, CancellationToken ct)
        => service.CompleteMetadataAsync(id, request.Version, ct);
    [HttpPost("{id:guid}/download-complete")]
    public Task<StatementDetail> CompleteDownload(Guid id, StatementVersionRequest request, CancellationToken ct)
        => service.CompleteDownloadAsync(id, request.Version, ct);
    [HttpPost("{id:guid}/validate")]
    public async Task<IActionResult> Validate(Guid id, StatementVersionRequest request, CancellationToken ct)
    {
        var result = await service.ValidateAsync(id, request.Version, ct);
        return result.Errors.Count > 0 ? UnprocessableEntity(result) : Ok(result);
    }
}
public sealed class PdfStatementForm
{
    public string ConversionType { get; set; } = "STATEMENT";
    public Guid? ProcessingId
    {
        get; set;
    }
    public IFormFile? File
    {
        get; set;
    }
    public Guid BankId
    {
        get; set;
    }
    public Guid? ClientId
    {
        get; set;
    }
    public Guid? BankAccountId
    {
        get; set;
    }
    public string Currency { get; set; } = "MAD";
}
