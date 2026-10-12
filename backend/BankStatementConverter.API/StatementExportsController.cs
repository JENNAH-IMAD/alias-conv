using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.AspNetCore.Mvc;
namespace BankStatementConverter.API;
[ApiController,Route("api/statement-imports/{statementId:guid}/exports")]
public sealed class StatementExportsController(StatementImportService service) : ControllerBase
{
    [HttpPost("{templateId:guid}/preview")]
    public Task<ExportPreview> Preview(Guid statementId,Guid templateId,ExportRequest request,CancellationToken ct)=>service.PreviewAsync(statementId,templateId,request,ct);
    [HttpPost("{templateId:guid}")]
    public async Task<IActionResult> Download(Guid statementId,Guid templateId,ExportRequest request,CancellationToken ct) {
        var result=await service.ExportAsync(statementId,templateId,request,ct);Response.Headers.CacheControl="no-store";
        if(!result.DownloadAllowed)return UnprocessableEntity(new{title="Export bloqué. Corrigez les erreurs de l’aperçu ou activez le téléchargement avec erreurs.",result.Errors,result.Warnings});
        return File(result.Content,result.MimeType,result.FileName);
    }
    [HttpGet] public Task<PageResult<ExportJobDto>> History(Guid statementId,CancellationToken ct,int page=1,int pageSize=20)=>service.HistoryAsync(statementId,page,pageSize,ct);
    [HttpGet("archives/{jobId:guid}/file")]
    public async Task<IActionResult> ArchivedDownload(Guid statementId,Guid jobId,CancellationToken ct,bool standard=false) {
        var result=await service.ArchivedDownloadAsync(statementId,jobId,standard,ct);Response.Headers.CacheControl="no-store";
        return File(result.Content,result.MimeType,result.FileName);
    }
    [HttpGet("archives/{jobId:guid}/preview")]
    public async Task<IActionResult> ArchivedPreview(Guid statementId,Guid jobId,CancellationToken ct) {
        var job=await service.ArchivedJobAsync(statementId,jobId,ct);var result=await service.ArchivedDownloadAsync(statementId,jobId,false,ct);
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var text=System.Text.Encoding.GetEncoding(job.Encoding).GetString(result.Content);var snapshot=System.Text.Json.Nodes.JsonNode.Parse(job.TemplateSnapshotJson);
        Response.Headers.CacheControl="no-store";
        return Ok(new{job.FileName,job.StatementVersion,job.RowCount,job.Encoding,job.Delimiter,job.GeneratedByName,job.TemplateNameSnapshot,job.TemplateSnapshotJson,job.Status,
            diagnostics=snapshot?["diagnostics"],textEdited=snapshot?["textEdited"]?.GetValue<bool>() ?? false,text=text.Length>200000 ? text[..200000] : text,truncated=text.Length>200000});
    }
}
