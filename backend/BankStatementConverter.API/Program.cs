using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using BankStatementConverter.Infrastructure.Exports;
using BankStatementConverter.Application.Exports;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using BankStatementConverter.API;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
// Published server/desktop builds can read a configuration kept outside the application files.
var externalConfig=Environment.GetEnvironmentVariable("ALIAS_CONFIG_FILE");
if(!string.IsNullOrWhiteSpace(externalConfig))
    builder.Configuration.AddJsonFile(Path.GetFullPath(externalConfig),optional:false,reloadOnChange:false).AddEnvironmentVariables().AddCommandLine(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddControllers(o => o.Filters.Add<PermissionFilter>()).AddJsonOptions(o=>o.JsonSerializerOptions.NumberHandling=JsonNumberHandling.AllowReadingFromString);
builder.Services.AddScoped<WorkspaceUserService>();
builder.Services.AddScoped<WorkspaceCookieEvents>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "Alias.Session"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = false;
    o.EventsType = typeof(WorkspaceCookieEvents);
});
builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
var keysDirectory = Path.Combine(Path.GetFullPath(builder.Configuration["Storage:Root"] ?? "uploads",builder.Environment.ContentRootPath), ".keys");
Directory.CreateDirectory(keysDirectory);
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysDirectory)).SetApplicationName("Alias.Workspace");
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("authentication", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new() { Title = "Alias Convert PDF", Version = "v1" }));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3002"]).AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("Content-Disposition")));
var maxBytes = builder.Configuration.GetValue<long>("Storage:MaxBytes", 20 * 1024 * 1024);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxBytes + 1024 * 1024);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxBytes + 1024 * 1024);
if (builder.Configuration.GetValue("ArchiveCleanup:Enabled", true)) builder.Services.AddHostedService<ArchiveCleanupWorker>();
builder.Services.AddScoped<BankService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddSingleton<IFileStorageService, LocalFileStorage>();
builder.Services.AddScoped(typeof(CatalogService<,,>));
builder.Services.AddScoped<ICatalogDefinition<Client, ClientInput, ClientDto>, ClientDefinition>();
builder.Services.AddScoped<ICatalogDefinition<BankAccount, BankAccountInput, BankAccountDto>, AccountDefinition>();
builder.Services.AddScoped<ICatalogDefinition<ExportTemplate, ExportInput, ExportDto>, ExportDefinition>();
builder.Services.AddSingleton<IExportSourceResolver,ExportSourceResolver>();
builder.Services.AddSingleton<ExportDateFormatter>();
builder.Services.AddSingleton<ExportNumberFormatter>();
builder.Services.AddSingleton<CsvExportWriter>();
builder.Services.AddSingleton<DelimitedTextExportWriter>();
builder.Services.AddSingleton<ExportFileNameBuilder>();
builder.Services.AddSingleton<StatementValidator>();
builder.Services.AddSingleton<IExportEngine,ExportEngine>();
builder.Services.AddScoped<StatementImportService>();
builder.Services.AddSingleton<ProcessingProgressStore>();
builder.Services.AddScoped<RequestProcessingProgress>();
builder.Services.AddScoped<IProcessingProgress>(s=>s.GetRequiredService<RequestProcessingProgress>());
builder.Services.AddSingleton<StatementPdfExtractor>();
builder.Services.AddSingleton<IStatementPdfExtractor>(s=>s.GetRequiredService<StatementPdfExtractor>());
builder.Services.AddSingleton<OcrSettingsStore>();
builder.Services.AddSingleton<OcrWorkQueue>();
builder.Services.AddHttpClient("paddle-cloud",http=>http.Timeout=Timeout.InfiniteTimeSpan).RemoveAllLoggers();
builder.Services.AddHttpClient("paddle-local",http=>http.Timeout=Timeout.InfiniteTimeSpan).RemoveAllLoggers();
builder.Services.AddScoped<IPdfOcrClient>(s=> {
    var settings=s.GetRequiredService<OcrSettingsStore>().Snapshot;
    var clients=s.GetRequiredService<IHttpClientFactory>();
    IPdfOcrClient client=settings.Provider switch {
        "AI_STUDIO" => new PaddleAiStudioClient(clients.CreateClient("paddle-cloud"),builder.Configuration,s.GetRequiredService<StatementPdfExtractor>(),s.GetRequiredService<IProcessingProgress>(),settings),
        "LOCAL" => new PaddleOcrClient(clients.CreateClient("paddle-local"),builder.Configuration,settings),
        _ => throw new InvalidOperationException("Ocr:Provider doit être LOCAL ou AI_STUDIO.")
    };
    return new QueuedPdfOcrClient(client,s.GetRequiredService<OcrWorkQueue>(),s.GetRequiredService<IProcessingProgress>());
});
builder.Services.AddScoped<IAutomaticPdfExtraction,AutomaticPdfExtraction>();
builder.Services.AddScoped<PdfStatementImportService>();
var app = builder.Build();
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        var status = ex switch { AppException a => a.Status, DbUpdateException { InnerException: PostgresException { SqlState: "23505" or "23503" or "23001" } } => 409, BadHttpRequestException b => b.StatusCode, OperationCanceledException => 499, _ => 500 };
        var message = ex is AppException ? ex.Message : status == 409 ? "Doublon ou élément encore utilisé : modification impossible." : status == 413 ? "Fichier trop volumineux." : "Erreur serveur. Consultez les journaux.";
        app.Logger.Log(status >= 500 ? LogLevel.Error : LogLevel.Warning, ex, "Requête échouée : {Status}", status);
        if (!ctx.Response.HasStarted) { ctx.Response.StatusCode = status; await ctx.Response.WriteAsJsonAsync(new { title = message, status, traceId = ctx.TraceIdentifier }); }
    }
});
app.UseCors();
// A custom header requires a CORS preflight, preventing cross-site HTML forms from making cookie-authenticated writes.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method) && !HttpMethods.IsOptions(ctx.Request.Method)
        && ctx.Request.Headers["X-Alias-Request"] != "1")
    { ctx.Response.StatusCode = 400; await ctx.Response.WriteAsJsonAsync(new { title = "En-tête de sécurité manquant." }); return; }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled")) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapControllers();
app.MapGet("/health", async (AppDbContext db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503)).AllowAnonymous();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (await db.Database.CanConnectAsync())
    {
        if (builder.Configuration.GetValue<bool>("Database:AutoMigrate")) await db.Database.MigrateAsync();
        await Seed.RunAsync(db, builder.Configuration);
    }
    else if (app.Environment.IsDevelopment())
    {
        app.Logger.LogWarning("PostgreSQL indisponible : demarrage de l'API sans initialisation de base. /health renverra 503 jusqu'a ce que la base soit accessible.");
    }
    else
    {
        throw new InvalidOperationException("PostgreSQL indisponible. Configurez ConnectionStrings__Default et verifiez que la base est accessible.");
    }
}
app.Run();
public partial class Program;

