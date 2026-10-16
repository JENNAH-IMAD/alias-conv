using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BankStatementConverter.API;

internal static class ApplicationSetup
{
    // Installer ces protections avant l'authentification pour traiter aussi ses erreurs.
    public static void UseAliasRequestHandling(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception exception)
            {
                var status = exception switch
                {
                    AppException applicationError => applicationError.Status,
                    DbUpdateException { InnerException: PostgresException { SqlState: "23505" or "23503" or "23001" } } => 409,
                    BadHttpRequestException requestError => requestError.StatusCode,
                    OperationCanceledException => 499,
                    _ => 500
                };
                var message = exception is AppException ? exception.Message : status switch
                {
                    409 => "Doublon ou élément encore utilisé : modification impossible.",
                    413 => "Fichier trop volumineux.",
                    _ => "Erreur serveur. Consultez les journaux."
                };

                app.Logger.Log(status >= 500 ? LogLevel.Error : LogLevel.Warning, exception, "Requête échouée : {Status}", status);
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = status;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        title = message,
                        status,
                        traceId = context.TraceIdentifier
                    });
                }
            }
        });
        app.UseCors();

        // Cet en-tête impose un contrôle CORS avant toute écriture depuis une autre origine.
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            var isRead = HttpMethods.IsGet(request.Method)
                || HttpMethods.IsHead(request.Method)
                || HttpMethods.IsOptions(request.Method);
            if (request.Path.StartsWithSegments("/api") && !isRead && request.Headers["X-Alias-Request"] != "1")
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "En-tête de sécurité manquant."
                });
                return;
            }
            await next();
        });
    }

    public static async Task InitializeAliasDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Database.CanConnectAsync())
        {
            if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
            {
                await db.Database.MigrateAsync();
            }
            await Seed.RunAsync(db, app.Configuration);
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
}
