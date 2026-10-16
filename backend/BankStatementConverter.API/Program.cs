using BankStatementConverter.API;
using BankStatementConverter.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
// Les installations lisent leur configuration locale hors du dossier des binaires.
var externalConfig = Environment.GetEnvironmentVariable("ALIAS_CONFIG_FILE");
if (!string.IsNullOrWhiteSpace(externalConfig))
{
    builder.Configuration
        .AddJsonFile(Path.GetFullPath(externalConfig), optional: false, reloadOnChange: false)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
}

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.AddAliasServices();

var app = builder.Build();
app.UseAliasRequestHandling();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/health", async (AppDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok(new
        {
            status = "ok"
        })
        : Results.StatusCode(503)).AllowAnonymous();

await app.InitializeAliasDatabaseAsync();
app.Run();

// Point d'entree accessible aux tests d'integration WebApplicationFactory.
public partial class Program;
