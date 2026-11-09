using System.Text.Json;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.EntityFrameworkCore;

static class TreatmentChecks
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    public static async Task Run(AppDbContext db, StatementImportService statements, StatementImport original, Guid templateId, Guid otherClient)
    {
        var exportedState = await new StatementTreatmentStore(db).Get(original.Id, null, default);
        Check(exportedState is { Kind: "STATEMENT" } && exportedState.Selection.GetProperty("templateId").GetGuid() == templateId,
            "Treatment_ExportKeepsLatestModelAndResult");
        await db.StatementTreatments.Where(x => x.StatementId == original.Id).ExecuteDeleteAsync();
        var historical = await new StatementTreatmentStore(db).Get(original.Id, null, default);
        Check(historical is { Kind: "STATEMENT", Preview: null } && historical.Selection.GetProperty("templateId").GetGuid() == templateId,
            "Treatment_OlderLegacyArchiveRestoresLastChoice");
        var preview = await statements.PreviewAsync(original.Id, templateId, new(original.Version), default);
        var saved = await new StatementTreatmentStore(db).Get(original.Id, null, default);
        Check(saved is { Kind: "STATEMENT", Current: true } && saved.Selection.GetProperty("templateId").GetGuid() == templateId
            && saved.Preview!.Value.GetProperty("rows").GetArrayLength() == preview.Rows.Count, "Treatment_LegacyPreviewSavesModelAndResult");
        var record = new StatementImport { ClientId = original.ClientId, BankAccountId = original.BankAccountId,
            BankName = original.BankName, NormalizedJson = original.NormalizedJson, Version = 1 };
        db.StatementImports.Add(record); await db.SaveChangesAsync();
        var source = await statements.GetAsync(record.Id, default);
        var store = new StatementTreatmentStore(db);
        var first = DateTime.UtcNow.AddMinutes(-2); var second = first.AddSeconds(1);
        await store.Save(source, null, "STATEMENT", new { templateId }, new { rows = new[] { new[] { "first" } } }, first, default);
        await store.Save(source, null, "ENTRY", new { modelId = "chosen", keywordOverrides = new[] { "CHEQUE" }, decisions = new Dictionary<string,string?> { ["row"] = null } }, new { rows = new[] { new[] { "last" } } }, second, default);
        await store.Save(source, null, "STATEMENT", new { templateId }, new { rows = new[] { new[] { "late-old" } } }, first, default);
        // Fresh context simulates another server request; no tracked in-memory state is reused.
        await using var fresh = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db.Database.GetConnectionString()).Options);
        var restored = await new StatementTreatmentStore(fresh).Get(record.Id, null, default);
        Check(restored is { Kind: "ENTRY", Current: true } && restored.Selection.GetProperty("modelId").GetString() == "chosen"
            && restored.Preview!.Value.GetProperty("rows")[0][0].GetString() == "last", "Treatment_DurableSnapshotRejectsLateOlderRequest");
        Check(restored!.Selection.GetProperty("keywordOverrides")[0].GetString() == "CHEQUE" && restored.Selection.GetProperty("decisions").GetProperty("row").ValueKind == JsonValueKind.Null,
            "Treatment_PreservesKeywordsAndExclusions");
        var extract = Guid.NewGuid();
        await store.Save(source, extract, "STATEMENT", new { templateId }, new { rows = new[] { new[] { "extract" } } }, second, default);
        Check((await store.Get(record.Id, extract, default))!.Preview!.Value.GetProperty("rows")[0][0].GetString() == "extract"
            && (await store.Get(record.Id, null, default))!.Kind == "ENTRY", "Treatment_IsolatesSavedExtractScope");
        Check(record.Version == 1 && record.NormalizedJson == original.NormalizedJson, "Treatment_PreviewSaveDoesNotModifySource");
        record.Version++; await db.SaveChangesAsync();
        var stale = await store.Get(record.Id, null, default);
        Check(stale is { Current: false, Preview: null } && stale.Selection.GetProperty("modelId").GetString() == "chosen", "Treatment_SourceChangeKeepsChoicesWithoutStaleResult");
        await store.Save(source, null, "STATEMENT", new { templateId }, new { rows = new[] { new[] { "obsolete" } } }, DateTime.UtcNow, default);
        Check((await store.Get(record.Id, null, default))!.Kind == "ENTRY", "Treatment_ObsoleteSourceCannotOverwriteSavedResult");
        record.ClientId = otherClient; record.BankAccountId = null; await db.SaveChangesAsync();
        Check(await store.Get(record.Id, null, default) == null, "Treatment_ReassignmentDoesNotRestoreOtherClientChoices");
        db.StatementImports.Remove(record); await db.SaveChangesAsync();
        Check(!await db.StatementTreatments.AnyAsync(x => x.StatementId == record.Id), "Treatment_SourceDeletionCleansSavedState");
    }
}
