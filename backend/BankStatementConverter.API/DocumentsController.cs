using System.Security.Claims;
using BankStatementConverter.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace BankStatementConverter.API;
public class ImportPdfForm { public Guid ClientId { get; set; } public Guid BankAccountId { get; set; } public IFormFile File { get; set; } = null!; }
public record ProcessPdfInput(Guid ProfileId, Guid ExportId);
[ApiController, Authorize, Route("api/documents")]
public class DocumentsController(IConversionService service) : ControllerBase
{
    [HttpPost, Consumes("multipart/form-data"), EnableRateLimiting("conversion")]
    public async Task<IActionResult> Import([FromForm] ImportPdfForm input, CancellationToken ct) {
        if(input.File==null) throw new AppException(400,"PDF requis.");
        await using var stream=input.File.OpenReadStream();
        var result=await service.ImportAsync(new(input.ClientId,input.BankAccountId,Guid.Empty,Guid.Empty,input.File.FileName,input.File.ContentType,input.File.Length,stream),Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),ct);
        return StatusCode(201,result);
    }
    [HttpPost("{id:guid}/process"), EnableRateLimiting("conversion")]
    public Task<ConversionResult> Process(Guid id, ProcessPdfInput input, CancellationToken ct) => service.ProcessAsync(id,input.ProfileId,input.ExportId,Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),ct);
}
