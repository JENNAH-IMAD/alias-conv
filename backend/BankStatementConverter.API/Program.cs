using System.Text;
using System.Threading.RateLimiting;
using BankStatementConverter.API;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
var key = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32) throw new InvalidOperationException("Configurez Jwt__Key ou Jwt:Key (32 octets minimum).");
var connection = builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Configurez ConnectionStrings__Default.");
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connection));
builder.Services.AddControllers(options => options.Filters.Add<PermissionFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => {
    o.SwaggerDoc("v1", new() { Title = "Bank Statement Converter", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new() { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    o.AddSecurityRequirement(new() { [new() { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>() });
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new() {
    ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"],
    ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(15)
});
builder.Services.AddScoped<UserManagementService>();
builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options => {
    options.Events = new JwtBearerEvents {
        OnTokenValidated = async context => {
            var principal = context.Principal;
            if (!Guid.TryParse(principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id)) { context.Fail("Session invalide."); return; }
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, context.HttpContext.RequestAborted);
            if (user == null || principal?.FindFirst("session_stamp")?.Value != UserManagementService.SessionStamp(user)) context.Fail("Session expirée. Reconnectez-vous.");
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3002"]).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition")));
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new() { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("conversion", ctx => RateLimitPartition.GetConcurrencyLimiter(ctx.User.Identity?.Name ?? "unknown", _ => new() { PermitLimit = 2, QueueLimit = 0 }));
});
var maxBytes = builder.Configuration.GetValue<long>("Storage:MaxBytes", 20 * 1024 * 1024);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = maxBytes + 1024 * 1024);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxBytes + 1024 * 1024);
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IConversionService, ConversionService>();
if (builder.Configuration.GetValue("ArchiveCleanup:Enabled", true)) builder.Services.AddHostedService<ArchiveCleanupWorker>();
builder.Services.AddScoped<HistoryService>();
builder.Services.AddScoped<BankService>();
builder.Services.AddSingleton<IFileStorageService, LocalFileStorage>();
builder.Services.AddSingleton<IPdfExtractionService, PdfExtractionService>();
builder.Services.AddSingleton<IBankStatementParser, DelimitedStatementParser>();
builder.Services.AddSingleton<ITransactionExporter, Sage100Exporter>();
builder.Services.AddSingleton<ITransactionExporter, SageX3Exporter>();
builder.Services.AddSingleton<ITransactionExporter, CustomCsvExporter>();
builder.Services.AddScoped(typeof(CatalogService<,,>));
builder.Services.AddScoped<ICatalogDefinition<Client,ClientInput,ClientDto>,ClientDefinition>();
builder.Services.AddScoped<ICatalogDefinition<BankAccount,BankAccountInput,BankAccountDto>,AccountDefinition>();
builder.Services.AddScoped<ICatalogDefinition<BankStatementTemplate,StatementInput,StatementDto>,StatementDefinition>();
builder.Services.AddScoped<ICatalogDefinition<ExportTemplate,ExportInput,ExportDto>,ExportDefinition>();
var app = builder.Build();
app.Use(async (ctx, next) => {
    try { await next(); }
    catch (Exception ex) {
        var status = ex switch { AppException a => a.Status, DbUpdateException { InnerException: PostgresException { SqlState: "23505" or "23503" or "23001" } } => 409, BadHttpRequestException b => b.StatusCode, OperationCanceledException => 499, _ => 500 };
        var message = ex is AppException ? ex.Message : status == 409 ? "Doublon ou élément encore utilisé : modification impossible." : status == 413 ? "Fichier trop volumineux." : "Erreur serveur. Consultez les journaux.";
        app.Logger.Log(status >= 500 ? LogLevel.Error : LogLevel.Warning, ex, "Requête échouée : {Status}", status);
        if (!ctx.Response.HasStarted) { ctx.Response.StatusCode = status; await ctx.Response.WriteAsJsonAsync(new { title = message, status, traceId = ctx.TraceIdentifier }); }
    }
});
app.UseCors(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled")) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapControllers();
app.MapGet("/health", async (AppDbContext db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503));
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

