using System.Security.Claims;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace BankStatementConverter.API;

[ApiController, Authorize]
public abstract class CatalogController<TEntity,TInput,TDto>(CatalogService<TEntity,TInput,TDto> service) : ControllerBase where TEntity : Entity,new()
{
    [HttpGet] public Task<PageResult<TDto>> List(CancellationToken ct, int page = 1, int pageSize = 50) => service.ListAsync(page, pageSize, ct);
    [HttpGet("{id:guid}")] public Task<TDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost] public virtual async Task<ActionResult<TDto>> Create(TInput input, CancellationToken ct) => StatusCode(201, await service.SaveAsync(null, input, ct));
    [HttpPut("{id:guid}")] public virtual Task<TDto> Update(Guid id, TInput input, CancellationToken ct) => service.SaveAsync(id, input, ct);
    [HttpDelete("{id:guid}")] public virtual async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}
public abstract class AdminCatalogController<TEntity,TInput,TDto>(CatalogService<TEntity,TInput,TDto> service) : CatalogController<TEntity,TInput,TDto>(service) where TEntity : Entity,new()
{
    public override Task<ActionResult<TDto>> Create(TInput input, CancellationToken ct) => base.Create(input, ct);
    public override Task<TDto> Update(Guid id, TInput input, CancellationToken ct) => base.Update(id, input, ct);
    public override Task<IActionResult> Delete(Guid id, CancellationToken ct) => base.Delete(id, ct);
}
[Route("api/clients")] public class ClientsController(CatalogService<Client,ClientInput,ClientDto> s) : CatalogController<Client,ClientInput,ClientDto>(s);
[ApiController, Authorize, Route("api/banks")]
public class BanksController(BankService service) : ControllerBase
{
    [HttpGet] public Task<PageResult<BankDto>> List(CancellationToken ct, int page = 1, int pageSize = 20, string? search = null, string? status = null, string sort = "name") => service.ListAsync(page, pageSize, search, status, sort, ct);
    [HttpGet("{id:guid}")] public Task<BankDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);
    [HttpPost] public async Task<ActionResult<BankDto>> Create(BankInput input, CancellationToken ct)
    { var result = await service.SaveAsync(null, input, ct); return CreatedAtAction(nameof(Get), new { id = result.Id }, result); }
    [HttpPut("{id:guid}")] public Task<BankDto> Update(Guid id, BankInput input, CancellationToken ct) => service.SaveAsync(id, input, ct);
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
    [HttpGet("{id:guid}/accounts")] public Task<PageResult<BankAccountSummary>> Accounts(Guid id, CancellationToken ct, int page = 1, int pageSize = 20) => service.AccountsAsync(id, page, pageSize, ct);
    [HttpGet("{id:guid}/profiles")] public Task<PageResult<BankProfileSummary>> Profiles(Guid id, CancellationToken ct, int page = 1, int pageSize = 20) => service.ProfilesAsync(id, page, pageSize, ct);
}
[Route("api/bank-accounts")] public class AccountsController(CatalogService<BankAccount,BankAccountInput,BankAccountDto> s) : CatalogController<BankAccount,BankAccountInput,BankAccountDto>(s);
[Route("api/bank-statement-templates")] public class StatementsController(CatalogService<BankStatementTemplate,StatementInput,StatementDto> s) : AdminCatalogController<BankStatementTemplate,StatementInput,StatementDto>(s);
[Route("api/export-templates")] public class ExportsController(CatalogService<ExportTemplate,ExportInput,ExportDto> s) : AdminCatalogController<ExportTemplate,ExportInput,ExportDto>(s);
[ApiController, Route("api/auth"), EnableRateLimiting("auth")]
public class AuthController(IAuthenticationService auth) : ControllerBase
{
    [HttpPost("login")] public Task<AuthResult> Login(LoginDto input, CancellationToken ct) => auth.LoginAsync(input, ct);
    [HttpPost("register")] public async Task<IActionResult> Register(RegisterDto input, CancellationToken ct) => StatusCode(201, await auth.RegisterAsync(input, ct));
}
public class ConversionForm
{
    public Guid ClientId { get; set; }
    public Guid BankAccountId { get; set; }
    public Guid BankStatementTemplateId { get; set; }
    public Guid ExportTemplateId { get; set; }
    public IFormFile File { get; set; } = null!;
}
[ApiController, Authorize, Route("api/conversions")]
public class ConversionsController(IConversionService service, AppDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpPost, Consumes("multipart/form-data"), EnableRateLimiting("conversion")]
    public async Task<ConversionResult> Convert([FromForm] ConversionForm input, CancellationToken ct)
    {
        if (input.File == null) throw new AppException(400, "PDF requis.");
        await using var stream = input.File.OpenReadStream();
        return await service.ConvertAsync(new(input.ClientId, input.BankAccountId, input.BankStatementTemplateId, input.ExportTemplateId, input.File.FileName, input.File.ContentType, input.File.Length, stream), UserId, ct);
    }
    [HttpGet("{id:guid}/download")] public async Task<IActionResult> Download(Guid id, CancellationToken ct, bool excel = false)
    {
        var f = await service.DownloadAsync(id, UserId, User.IsInRole("Admin"), ct);

        using var source = f.Content;
        var item = await db.History.FindAsync(new object[] { id }, ct) ?? throw new AppException(404, "Releve introuvable.");
        var content = ExportDownload.WithoutHeader(source, item.OutputEncoding ?? "utf-8", item.OutputDelimiter ?? ";", item.TransactionCount);
        if (!excel) return File(content, "application/octet-stream", f.Name);
        using var operations = new MemoryStream(content);
        return File(ExcelDownload.Create(operations, item.OutputEncoding ?? "utf-8", item.OutputDelimiter ?? ";"), ExcelDownload.ContentType, Path.GetFileNameWithoutExtension(f.Name) + ".xlsx");
    }
}
[ApiController, Authorize, Route("api/history")]
public class HistoryController(HistoryService service) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet] public Task<PageResult<HistoryDto>> List(CancellationToken ct, int page = 1, int pageSize = 20, string? search = null, string? status = null) => service.ListAsync(UserId, User.IsInRole("Admin"), page, pageSize, search, status, ct);
    [HttpGet("{id:guid}")] public Task<HistoryDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, UserId, User.IsInRole("Admin"), ct);
    [HttpGet("/api/dashboard")] public Task<object> Dashboard(CancellationToken ct) => service.DashboardAsync(UserId, User.IsInRole("Admin"), ct);
}

