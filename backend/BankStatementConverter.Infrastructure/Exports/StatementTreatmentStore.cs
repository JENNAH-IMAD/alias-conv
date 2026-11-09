using System.Text.Json;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using Microsoft.EntityFrameworkCore;

namespace BankStatementConverter.Infrastructure.Exports;

public sealed record SavedStatementTreatment(string Kind, Guid StatementId, Guid? ExtractId, int SourceVersion,
    Guid? ClientId, Guid? BankAccountId, DateTime SavedAt, JsonElement Selection, JsonElement? Preview, bool Current = true);

public sealed class StatementTreatmentStore(AppDbContext db)
{
    private static readonly JsonSerializerOptions Json = StatementJson.CreateOptions();
    public async Task Save(StatementDetail source, Guid? extractId, string kind, object selection, object preview,
        DateTime requestedAt, CancellationToken ct)
    {
        var scope = extractId ?? Guid.Empty;
        var now = DateTime.UtcNow;
        var snapshot = JsonSerializer.Serialize(new SavedStatementTreatment(kind, source.Id, extractId, source.Version,
            source.ClientId, source.BankAccountId, now, JsonSerializer.SerializeToElement(selection, Json),
            JsonSerializer.SerializeToElement(preview, Json)), Json);
        // Ignore a late response from an older request or an obsolete source version.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "StatementTreatments" ("Id", "StatementId", "ScopeId", "SnapshotJson", "RequestedAt", "CreatedAt", "UpdatedAt")
            SELECT {Guid.NewGuid()}, {source.Id}, {scope}, CAST({snapshot} AS jsonb), {requestedAt}, {now}, {now}
            WHERE EXISTS (SELECT 1 FROM "StatementImports" WHERE "Id"={source.Id} AND "Version"={source.Version}
                AND "ClientId" IS NOT DISTINCT FROM {source.ClientId} AND "BankAccountId" IS NOT DISTINCT FROM {source.BankAccountId})
            ON CONFLICT ("StatementId", "ScopeId") DO UPDATE SET "SnapshotJson"=EXCLUDED."SnapshotJson",
                "RequestedAt"=EXCLUDED."RequestedAt", "UpdatedAt"=EXCLUDED."UpdatedAt"
            WHERE "StatementTreatments"."RequestedAt" <= EXCLUDED."RequestedAt"
            """, ct);
    }

    public async Task<SavedStatementTreatment?> Get(Guid id, Guid? extractId, CancellationToken ct)
    {
        var source = await db.StatementImports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "Document source introuvable.");
        var scope = extractId ?? Guid.Empty;
        var saved = await db.StatementTreatments.AsNoTracking().SingleOrDefaultAsync(x => x.StatementId == id && x.ScopeId == scope, ct);
        if (saved != null)
        {
            var snapshot = JsonSerializer.Deserialize<SavedStatementTreatment>(saved.SnapshotJson, Json)!;
            // A reassigned document must never restore another client's processing choices.
            if (snapshot.ClientId != source.ClientId || snapshot.BankAccountId != source.BankAccountId) return null;
            return snapshot with { Current = snapshot.SourceVersion == source.Version,
                Preview = snapshot.SourceVersion == source.Version ? snapshot.Preview : null };
        }
        // Documents processed before automatic preview saving can still reuse their last export.
        var entry = await db.AccountingEntryArchives.AsNoTracking().Where(x => x.StatementId == id && x.ClientId == source.ClientId)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        var exports = await db.ExportJobs.AsNoTracking().Where(x => x.StatementId == id).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var export = exports.FirstOrDefault(x => {
            using var doc = JsonDocument.Parse(x.TemplateSnapshotJson);
            var root = doc.RootElement;
            var archivedExtract = root.TryGetProperty("extract", out var extract) && extract.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.String ? value.GetGuid() : (Guid?)null;
            return archivedExtract == extractId;
        });
        if (entry != null && (export == null || entry.CreatedAt > export.CreatedAt))
        {
            using var document = JsonDocument.Parse(entry.SnapshotJson);
            var root = document.RootElement;
            var model = root.GetProperty("model");
            // Older archives did not record ExtractId. Only restore full-document archives
            // whose transaction identities match the source; never guess an extract scope.
            var original = JsonSerializer.Deserialize<BankStatementConverter.Domain.BankStatement>(source.NormalizedJson, Json)!;
            var archivedSource = JsonSerializer.Deserialize<BankStatementConverter.Domain.BankStatement>(root.GetProperty("source"), Json)!;
            if (extractId.HasValue || !archivedSource.Transactions.Select(x => x.Id).SequenceEqual(original.Transactions.Select(x => x.Id)))
                return export == null ? null : Legacy();
            var selection = new {
                clientId = entry.ClientId, statementId = id, exportTemplateId = Guid.Empty,
                modelId = entry.ModelId, abbreviation = model.GetProperty("abbreviation").GetString(),
                outputTemplateId = model.TryGetProperty("outputTemplateId", out var format) ? format : (JsonElement?)null,
                decisions = root.TryGetProperty("decisions", out var decisions) ? decisions : (JsonElement?)null,
                keywordOverrides = root.TryGetProperty("keywordOverrides", out var keywords) ? keywords : (JsonElement?)null
            };
            return new("ENTRY", id, extractId, entry.SourceVersion, source.ClientId, source.BankAccountId, entry.CreatedAt,
                JsonSerializer.SerializeToElement(selection, Json), null, false);
        }
        return export == null ? null : Legacy();
        SavedStatementTreatment Legacy() => new("STATEMENT", id, extractId, export!.StatementVersion,
            source.ClientId, source.BankAccountId, export.CreatedAt,
            JsonSerializer.SerializeToElement(new { templateId = export.TemplateId, extractId }, Json), null, false);
    }
}
