using System.Text;
using System.Text.Json;
using BankStatementConverter.API;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using BankStatementConverter.Infrastructure.Accounting;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

static class IndependentChecks
{
    // Keep this configurable five-column fixture distinct from the seven-column S100 default.
    private static EntryModelOutput FiveColumnOutput() => new()
    {
        Columns = [new("ACCOUNT", "N° compte"), new("TITLE", "Intitulé"), new("DESCRIPTION", "Libellé"), new("BANK", "Banque"), new("NET", "Montant HT")]
    };
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static async Task Reject(Func<Task> action, int status, string name) { try { await action(); } catch (AppException e) when (e.Status == status) { Console.WriteLine("PASS " + name); return; } throw new Exception(name); }
    public static async Task Run(AppDbContext db, EntryModelStore accounting, StatementImportService statements, IFileStorageService storage, IHttpContextAccessor http, Client client, BankAccount bank)
    {
        var row = new EntryResultRow(Guid.NewGuid(), 1, new(2026, 11, 1), "0512000", "Café", "ACHAT; \"café\"\nSuite", "Banque", "ACH", "MAD", "R1", 120, 100, 20, 20);
        var output = new EntryModelOutput { Columns = [new("DESCRIPTION", "Libellé", false), new("ACCOUNT", "Compte", true, false), new("NET", "HT")] };
        var text = Encoding.UTF8.GetString(EntryModelTextExport.Generate(output, [row]));
        Check(text == "Libellé;HT\r\n\"ACHAT; \"\"café\"\" Suite\";100,00\r\n", "IndependentTXT_ColumnOrderVisibilityAndEscaping");
        var originalDescription = row.Description;
        var wrapped = row with { Description = "PAIEMENT CHEQUE\r\n  Paiement Direction\rGénérale\n des\u0085impôts\u2028et\u2029taxes\tlocales" };
        var physical = Encoding.UTF8.GetString(EntryModelTextExport.Generate(FiveColumnOutput(), [wrapped, row with { TransactionId = Guid.NewGuid() }]));
        Check(physical.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length == 3 && !physical.Replace("\r\n", "").Any(c => c is '\r' or '\n' or '\u0085' or '\u2028' or '\u2029')
            && physical.Contains("PAIEMENT CHEQUE Paiement Direction Générale des impôts et taxes locales") && physical.Contains("0512000;Café;") && physical.Contains(";100,00\r\n") && row.Description == originalDescription,
            "IndependentTXT_OnePhysicalLinePerMovementPreservesSourceAndAmounts");
        var plain = output with { IncludeHeader = false, NumberCulture = "en-US", Delimiter = "|", Encoding = "windows-1252" };
        Check(EntryModelTextExport.EncodingFor(plain).GetString(EntryModelTextExport.Generate(plain, [row])).EndsWith("|100.00\r\n") && !EntryModelTextExport.EncodingFor(plain).GetString(EntryModelTextExport.Generate(plain, [row])).StartsWith("Libellé"), "IndependentTXT_EncodingCultureAndHeader");
        await Reject(() => Task.FromResult(EntryModelTextExport.Generate(plain, [row with { Description = "漢字" }])), 400, "IndependentTXT_RejectEncodingDataLoss");
        await Reject(() => Task.FromResult(EntryModelTextExport.Generate(output with { Columns = [new("NET", "HT"), new("NET", "Dup")] }, [row])), 400, "IndependentTXT_RejectDuplicateColumns");
        await Reject(() => Task.FromResult(EntryModelTextExport.Generate(output with { Columns = [new("NET", "HT", false)] }, [row])), 400, "IndependentTXT_RequireVisibleColumn");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var dossier = await accounting.Get(client.Id, default);
        var movement = new StatementTransaction { Sequence = 1, Date = new(2026, 11, 1), Description = "ACHAT CAFE", Debit = 120, Currency = bank.Currency };
        var source = new BankStatement { BankName = "Test bank", BankCode = "TEST", Currency = bank.Currency, AccountNumber = bank.AccountNumber, Transactions = [movement] };
        var record = new StatementImport { ClientId = client.Id, BankAccountId = bank.Id, BankName = "Test bank", Currency = bank.Currency, Status = "VALIDATED", Version = 1, TransactionCount = 1, SourceFileName = "independent.pdf", NormalizedJson = JsonSerializer.Serialize(source, json) };
        db.StatementImports.Add(record); await db.SaveChangesAsync();
        using var localFiles = new LocalEntryStorageFixture();
        var service = new IndependentEntryModelService(db, accounting, statements, localFiles.Storage, http);
        var model = new AccountingEntryModel(Guid.NewGuid(), "Bank title", "ACH", ["ACHAT"], bank.Id, bank.AccountCode, Guid.Empty, 20, "", Output: FiveColumnOutput());
        var saved = await service.Save(client.Id, new(dossier.Version, [model]), default);
        Check(saved.Models.Single().ExportTemplateId == Guid.Empty && saved.Models.Single().Output!.Columns.Count == 5, "IndependentModel_SaveWithoutLegacyTemplateOrLedgerPlan");
        var modelFile = await service.ModelFile(client.Id, model.Id, default);
        Check(Encoding.UTF8.GetString(modelFile.Content) == "N° compte;Intitulé;Libellé;Banque;Montant HT\r\n", "IndependentModel_DownloadConfiguredStructure");
        var run = new EntryModelRun(client.Id, record.Id, Guid.Empty, "ACH", ModelId: model.Id);
        await Reject(() => service.Preview(run with { ModelId = Guid.NewGuid() }, default), 400, "IndependentPreview_RejectForeignModel");
        foreach (var rate in new[] { 0, 7, 10, 14, 20 }) {
            var rateModel = model with { TaxRate = rate };
            var computed = EntryModelEngine.Evaluate(source, bank, [rateModel]).Single();
            Check(computed.Net == decimal.Round(120m / (1 + rate / 100m), 2, MidpointRounding.AwayFromZero) && computed.Net + computed.Tax == 120m, "IndependentVAT_Rate" + rate);
        }
        foreach (var rate in new[] { 7, 14 }) {
            saved = await service.Save(client.Id, new(saved.Version, [model with { TaxRate = rate }]), default);
            var ratePreview = await service.Preview(run, default);
            Check(saved.Models.Single().TaxRate == rate && ratePreview.ResultRows!.Single().Rate == rate
                && ratePreview.ResultRows!.Single().Net + ratePreview.ResultRows!.Single().Tax == 120m,
                "IndependentVAT_SaveAndPreviewRate" + rate);
        }
        saved = await service.Save(client.Id, new(saved.Version, [model]), default);
        var preview = await service.Preview(run, default);
        var remembered = await new StatementTreatmentStore(db).Get(record.Id, null, default);
        Check(remembered is { Kind: "ENTRY", Current: true } && remembered.Selection.GetProperty("modelId").GetGuid() == model.Id
            && remembered.Preview!.Value.GetProperty("resultRows")[0].GetProperty("net").GetString() == "100",
            "Treatment_EntryPreviewSavesExactModelAndCalculatedRows");
        Check(preview.CanExport && preview.ResultRows!.Single().Net == 100 && preview.ResultRows!.Single().Tax == 20 && preview.Lines.Count == 0, "IndependentPreview_CalculatesVATWithoutJournalMappings");
        await Reject(() => service.Export(run with { Confirmed = true, PreviewFingerprint = "stale" }, default), 409, "IndependentExport_RejectStalePreview");
        var originalSource = await db.StatementImports.AsNoTracking().Where(s => s.Id == record.Id).Select(s => s.NormalizedJson).SingleAsync();
        var file = await service.Export(run with { Confirmed = true, PreviewFingerprint = preview.Fingerprint }, default);
        await db.StatementTreatments.Where(x => x.StatementId == record.Id).ExecuteDeleteAsync();
        var priorResult = await new StatementTreatmentStore(db).Get(record.Id, null, default);
        Check(priorResult is { Kind: "ENTRY" } && priorResult.Selection.GetProperty("modelId").GetGuid() == model.Id,
            "Treatment_OlderEntryArchiveRestoresLastChoice");
        Check(Encoding.UTF8.GetString(file.Content).Contains("ACHAT CAFE;Test bank;100,00"), "IndependentExport_TXTUsesNewModelFields");
        var again = await service.Export(run with { Confirmed = true, PreviewFingerprint = preview.Fingerprint }, default);
        Check(again.Content.SequenceEqual(file.Content) && await db.AccountingEntryArchives.CountAsync() == 1, "IndependentExport_RetryReturnsSameArchive");
        await Reject(() => service.Export(run with { Confirmed = true, KeywordOverrides = ["CAFE"], PreviewFingerprint = preview.Fingerprint }, default), 409, "IndependentExport_RejectChangedWords");
        Check((await db.StatementImports.AsNoTracking().SingleAsync(s => s.Id == record.Id)).NormalizedJson == originalSource && (await accounting.Get(client.Id, default)).Version == saved.Version, "IndependentExport_PreservesSourceAndDossier");
        var archiveId = await db.AccountingEntryArchives.Select(a => a.Id).SingleAsync();
        var stored = await service.Archive(archiveId, default);
        Check(stored.StatementId == record.Id && stored.ClientId == client.Id && stored.Rows.Count == 1, "IndependentArchive_ConsultationRetainsClientAndResult");
        await service.Save(client.Id, new(saved.Version, []), default);
        db.StatementImports.Remove(record); await db.SaveChangesAsync();
        var archived = await service.Archive(archiveId, default);
        Check(archived.Model.Id == model.Id && archived.Rows.Single().Net == 100 && archived.Text == Encoding.UTF8.GetString(file.Content), "IndependentArchive_SurvivesModelAndSourceDeletion");
        Check(await db.AccountingEntryArchives.AnyAsync(a => a.Id == archiveId), "ClientSpace_RetainsResultAfterSourceDeletion");
        await Catalog(db, accounting, service, client, bank, source);
    }
    private static async Task Catalog(AppDbContext db, EntryModelStore accounting, IndependentEntryModelService service, Client client, BankAccount bank, BankStatement source)
    {
        var catalog = new EntryOutputTemplateCatalog(db);
        var legacyCount = await db.ExportTemplates.CountAsync();
        var format = await catalog.Create(new("Écritures comptables — 5 colonnes", "Format dédié", true, 0, FiveColumnOutput()), default);
        Check(format.Output.Columns.Select(c => c.Field).SequenceEqual(["ACCOUNT", "TITLE", "DESCRIPTION", "BANK", "NET"]) && await db.ExportTemplates.CountAsync() == legacyCount, "EntryCatalog_CreatesIndependentFiveColumnFormat");
        await Reject(() => catalog.Update(format.Id, new(format.Name, format.Description, true, 0, format.Output), default), 409, "EntryCatalog_RejectStaleVersion");
        format = await catalog.Update(format.Id, new(format.Name, format.Description, true, format.Version, format.Output with { FileExtension = "csv", Delimiter = "|", Encoding = "windows-1252", NumberCulture = "en-US", IncludeHeader = false }), default);
        var structure = await catalog.Structure(format.Id, default);
        Check(structure.Name.EndsWith(".csv") && structure.Mime.Contains("windows-1252") && EntryModelTextExport.EncodingFor(format.Output).GetString(structure.Content) == "N° compte|Intitulé|Libellé|Banque|Montant HT\r\n", "EntryCatalog_StructureUsesConfiguredFileSettings");
        var record = new StatementImport { ClientId = client.Id, BankAccountId = bank.Id, BankName = "Test bank", Currency = bank.Currency, Status = "VALIDATED", Version = 1, TransactionCount = 1, SourceFileName = "catalog.pdf", NormalizedJson = JsonSerializer.Serialize(source, new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        db.StatementImports.Add(record); await db.SaveChangesAsync();
        var model = new AccountingEntryModel(Guid.NewGuid(), "Bank title", "ACH", ["ACHAT"], bank.Id, bank.AccountCode, Guid.Empty, 20, Output: FiveColumnOutput(), OutputTemplateId: format.Id);
        var dossier = await accounting.Get(client.Id, default);
        var saved = await service.Save(client.Id, new(dossier.Version, [model]), default);
        Check(saved.Models.Single().Output!.FileExtension == "csv" && saved.Models.Single().Output!.Delimiter == "|", "EntryCatalog_EntryUsesSelectedExportFormat");
        await Reject(() => catalog.Delete(format.Id, format.Version, default), 409, "EntryCatalog_ProtectReferencedFormat");
        var run = new EntryModelRun(client.Id, record.Id, Guid.Empty, "ACH", ModelId: model.Id);
        var preview = await service.Preview(run, default);
        var file = await service.Export(run with { Confirmed = true, PreviewFingerprint = preview.Fingerprint }, default);
        Check(file.Name.EndsWith(".csv") && file.Mime == "text/csv; charset=windows-1252" && EntryModelTextExport.EncodingFor(format.Output).GetString(file.Content) == bank.AccountCode + "|Bank title|ACHAT CAFE|Test bank|100.00\r\n", "EntryCatalog_ResultUsesSeparatorEncodingTypeAndColumns");
        var s100 = await catalog.Create(new("Écriture comptable S100", "Consultation dédiée", true, 0, FiveColumnOutput()), default);
        var originalRule = JsonSerializer.Serialize((await service.Get(client.Id, default)).Models.Single());
        var displayRun = run with { OutputTemplateId = s100.Id };
        var displayPreview = await service.Preview(displayRun, default);
        Check(displayPreview.Output!.FileExtension == "txt" && displayPreview.Output.Delimiter == ";" && displayPreview.Output.Columns.Select(c => c.Field).SequenceEqual(["ACCOUNT", "TITLE", "DESCRIPTION", "BANK", "NET"]), "Consultation_UsesSelectedEntryDisplayFormat");
        await Reject(() => service.Preview(displayRun with { OutputTemplateId = Guid.NewGuid() }, default), 400, "Consultation_RejectUnknownDisplayFormat");
        await Reject(() => service.Preview(displayRun with { ModelId = null }, default), 400, "Consultation_DisplayOverrideRequiresExactEntry");
        await Reject(() => service.Export(displayRun with { Confirmed = true, PreviewFingerprint = preview.Fingerprint }, default), 409, "Consultation_RejectOtherDisplayFormatFingerprint");
        var displayFile = await service.Export(displayRun with { Confirmed = true, PreviewFingerprint = displayPreview.Fingerprint }, default);
        Check(displayFile.Name.EndsWith(".txt") && Encoding.UTF8.GetString(displayFile.Content) == "N° compte;Intitulé;Libellé;Banque;Montant HT\r\n" + bank.AccountCode + ";Bank title;ACHAT CAFE;Test bank;100,00\r\n", "Consultation_DownloadUsesDisplayModelParameters");
        Check(JsonSerializer.Serialize((await service.Get(client.Id, default)).Models.Single()) == originalRule, "Consultation_DoesNotModifySavedEntryRule");
        var displayArchiveId = await db.AccountingEntryArchives.Where(a => a.StatementId == record.Id && a.PreviewFingerprint == displayPreview.Fingerprint).Select(a => a.Id).SingleAsync();
        var displayArchive = await service.Archive(displayArchiveId, default);
        Check(displayArchive.Model.OutputTemplateId == s100.Id && displayArchive.Rows.Single().Net == 100m, "Consultation_ArchiveKeepsChosenDisplayFormat");
        var listing = new ClientWorkspaceController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var filters = (OkObjectResult)await listing.ModelFilters(default);
        var choices = (IEnumerable<WorkspaceModelFilterOption>)filters.Value!;
        Check(choices.Any(option => option.Kind == "entry-output" && option.Id == format.Id)
            && choices.All(option => option.Kind is "entry-output" or "export"), "ClientSpace_FilterListsExportFormatsWithoutEntryRules");
        var filtered = (OkObjectResult)await listing.Statements(default, client.Id, entryOutputTemplateId: format.Id);
        var filteredJson = JsonSerializer.SerializeToElement(filtered.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Check(filteredJson.GetProperty("total").GetInt32() == 1, "ClientSpace_FilterUsesArchivedEntryOutputFormat");
        var archiveId = await db.AccountingEntryArchives.Where(a => a.StatementId == record.Id && a.PreviewFingerprint == preview.Fingerprint).Select(a => a.Id).SingleAsync();
        format = await catalog.Update(format.Id, new(format.Name, format.Description, true, format.Version, format.Output with { Delimiter = ";", FileExtension = "txt" }), default);
        await Reject(() => service.Export(run with { Confirmed = true, PreviewFingerprint = preview.Fingerprint }, default), 409, "EntryCatalog_FormatChangeInvalidatesOldPreview");
        var current = await service.Preview(run, default);
        Check(current.Output!.Delimiter == ";" && current.Output.FileExtension == "txt", "EntryCatalog_ProcessingResolvesCurrentFormat");
        await service.Save(client.Id, new(saved.Version, []), default);
        await catalog.Delete(format.Id, format.Version, default);
        var archive = await service.Archive(archiveId, default);
        Check(archive.Output.Delimiter == "|" && archive.Output.FileExtension == "csv" && archive.Text == EntryModelTextExport.EncodingFor(archive.Output).GetString(file.Content), "EntryCatalog_ArchiveKeepsOriginalFormatAfterDeletion");
    }
}
