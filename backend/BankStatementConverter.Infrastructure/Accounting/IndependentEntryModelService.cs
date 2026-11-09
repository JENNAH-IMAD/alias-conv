using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
namespace BankStatementConverter.Infrastructure.Accounting;

public sealed class IndependentEntryModelService(AppDbContext db, EntryModelStore accounting, StatementImportService statements,
    IFileStorageService storage, IHttpContextAccessor http)
{
    private static readonly JsonSerializerOptions Json = StatementJson.CreateOptions();
    private static AccountingEntryModel Normalize(AccountingEntryModel m, IReadOnlyList<BankAccount> banks, IReadOnlyList<EntryOutputTemplate> formats)
    {
        var templateId = m.OutputTemplateId ?? (m.Output == null && formats.Any(t => t.Id == EntryOutputTemplateCatalog.DefaultId) ? EntryOutputTemplateCatalog.DefaultId : (Guid?)null);
        var format = formats.FirstOrDefault(t => t.Id == templateId);
        return m with {
            ExportTemplateId = Guid.Empty, TaxAccountCode = "", BankAccountBased = true,
            AccountCode = banks.FirstOrDefault(b => b.Id == m.BankAccountId)?.AccountCode ?? m.AccountCode,
            OutputTemplateId = templateId, Output = format != null ? EntryOutputTemplateCatalog.Output(format) : m.Output ?? new EntryModelOutput()
        };
    }
    private static void ValidateFormat(AccountingEntryModel model, IReadOnlyList<EntryOutputTemplate> formats) =>
        Validation.Require(!model.OutputTemplateId.HasValue || formats.Any(t => t.Id == model.OutputTemplateId && t.Active), "Le modèle d’exportation choisi est introuvable ou inactif. Choisissez un format actif.");
    private static void Validate(AccountingEntryModel m, IReadOnlyList<BankAccount> banks)
    {
        Validation.Require(m.Id != Guid.Empty, "Identifiant du modèle obligatoire.");
        Validation.Text(m.Name, "Intitulé", 100);
        Validation.Require(m.Abbreviation is { Length: >= 1 and <= 16 } && m.Abbreviation.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c is '-' or '_'), "Abréviation invalide.");
        Validation.Require(m.Keywords is { Count: >= 1 and <= 100 } && m.Keywords.All(k => !string.IsNullOrWhiteSpace(k) && k.Length <= 150 && EntryModelEngine.Normalize(k).Length > 0), "Ajoutez 1 à 100 mots ou expressions de 150 caractères maximum.");
        Validation.Require(banks.Any(b => b.Id == m.BankAccountId), "Le compte bancaire doit appartenir à cette entreprise.");
        Validation.Require(!string.IsNullOrWhiteSpace(m.AccountCode) && m.AccountCode == banks.Single(b => b.Id == m.BankAccountId).AccountCode, "Le numéro de compte doit correspondre à la banque choisie.");
        Validation.Require(m.TaxRate is 0 or 7 or 10 or 14 or 20, "Le taux doit être 0 %, 7 %, 10 %, 14 % ou 20 %.");
        EntryModelTextExport.Validate(m.Output ?? new());
    }
    public async Task<EntryModelDossier> Get(Guid clientId, CancellationToken ct)
    {
        var dossier = await accounting.Get(clientId, ct);
        var banks = await db.BankAccounts.AsNoTracking().Where(b => b.ClientId == clientId).ToArrayAsync(ct);
        var formats = await db.EntryOutputTemplates.AsNoTracking().ToArrayAsync(ct);
        return new(clientId, dossier.Version, dossier.Models.Select(m => Normalize(m, banks, formats)).ToArray());
    }
    public async Task<EntryModelDossier> Save(Guid clientId, EntryModelSave input, CancellationToken ct)
    {
        var dossier = await accounting.Get(clientId, ct);
        if (dossier.Version != input.Version) throw new AppException(409, "Le dossier a changé. Rechargez les modèles avant d'enregistrer.");
        Validation.Require(input.Models is { Count: <= 500 } && input.Models.All(m => m != null && m.Id != Guid.Empty)
            && input.Models.Select(m => m.Id).Distinct().Count() == input.Models.Count, "Liste ou identifiants de modèles invalides.");
        var banks = await db.BankAccounts.AsNoTracking().Where(b => b.ClientId == clientId).ToArrayAsync(ct);
        var formats = await db.EntryOutputTemplates.AsNoTracking().ToArrayAsync(ct);
        var models = input.Models.Select(m => Normalize(m, banks, formats)).ToArray();
        foreach (var model in models) { ValidateFormat(model, formats); Validate(model, banks); }
        var saved = await accounting.Save(clientId, new(input.Version, models), ct);
        return new(clientId, saved.Version, saved.Models);
    }
    private sealed record Context(StatementDetail Detail, BankStatement Source, BankAccount Bank, EntryModelDossier Dossier, AccountingEntryModel Model);
    private async Task<Context> Load(EntryModelRun input, CancellationToken ct)
    {
        var detail = await statements.GetAsync(input.StatementId, ct);
        Validation.Require(detail.ClientId == input.ClientId && detail.BankAccountId.HasValue, "Classez le document dans l'entreprise et son compte bancaire.");
        var bank = await db.BankAccounts.AsNoTracking().SingleOrDefaultAsync(b => b.Id == detail.BankAccountId && b.ClientId == input.ClientId, ct)
            ?? throw new AppException(404, "Compte bancaire introuvable.");
        if (db.Database.CurrentTransaction != null) bank = await db.BankAccounts.FromSqlInterpolated($"SELECT * FROM \"BankAccounts\" WHERE \"Id\"={bank.Id} FOR SHARE").AsNoTracking().SingleAsync(ct);
        var dossier = await accounting.Get(input.ClientId, ct);
        var models = dossier.Models.Where(m => m.Active && m.BankAccountId == bank.Id && m.Abbreviation == input.Abbreviation && (!input.ModelId.HasValue || m.Id == input.ModelId)).ToArray();
        Validation.Require(models.Length == 1, "Choisissez une écriture précise, active et liée à ce compte de l'entreprise.");
        var selectedModel = models[0];
        if (input.OutputTemplateId.HasValue) {
            Validation.Require(input.ModelId.HasValue, "Choisissez une écriture précise pour appliquer un modèle d’affichage et d’exportation.");
            selectedModel = selectedModel with { OutputTemplateId = input.OutputTemplateId };
        }
        var formats = selectedModel.OutputTemplateId is Guid templateId && db.Database.CurrentTransaction != null
            ? await db.EntryOutputTemplates.FromSqlInterpolated($"SELECT * FROM \"EntryOutputTemplates\" WHERE \"Id\"={templateId} FOR SHARE").AsNoTracking().ToArrayAsync(ct)
            : await db.EntryOutputTemplates.AsNoTracking().ToArrayAsync(ct);
        var model = Normalize(selectedModel, [bank], formats);
        ValidateFormat(model, formats);
        if (input.KeywordOverrides != null) {
            Validation.Require(input.ModelId.HasValue, "Choisissez une écriture précise pour ajuster les mots.");
            model = model with { Keywords = input.KeywordOverrides };
        }
        Validate(model, [bank]);
        return new(detail, StatementExtracts.ForExport(detail.Statement, input.ExtractId), bank, dossier, model);
    }
    private EntryModelPreview Evaluate(Context c, EntryModelRun input)
    {
        var classified = EntryModelEngine.Evaluate(c.Source, c.Bank, [c.Model], input.Decisions);
        var rows = classified.Where(r => r.Status == "MATCHED").Select(r => {
            var t = c.Source.Transactions.Single(t => t.Id == r.TransactionId);
            var bankAmounts = AccountingEngine.BankAmounts(c.Source, t);
            var rawAmounts = StatementAmounts.Normalize(t);
            // The bank statement's explicit debit/credit columns remain authoritative when
            // its balance convention is unavailable; the contra-account side is reversed.
            var bankCredit = bankAmounts != null
                ? bankAmounts.Value.Credit > 0
                : rawAmounts.Credit.HasValue != rawAmounts.Debit.HasValue
                    ? rawAmounts.Credit.HasValue
                    : rawAmounts.Direction?.ToUpperInvariant() is "CREDIT" or "IN" or "DEPOSIT";
            var debitHt = bankCredit ? r.Net!.Value : 0m;
            var creditHt = bankCredit ? 0m : r.Net!.Value;
            return new EntryResultRow(r.TransactionId, r.Sequence, r.Date, c.Model.AccountCode, c.Model.Name, r.Description, (c.Source.BankName ?? c.Bank.AccountName),
                c.Model.Abbreviation, c.Source.Currency ?? c.Bank.Currency, t.Reference ?? "", r.Total!.Value, r.Net!.Value, r.Tax!.Value, c.Model.TaxRate, debitHt, creditHt);
        }).ToArray();
        var errors = new List<AccountingIssue>();
        if (!rows.Any()) errors.Add(new("EMPTY", "Aucun libellé ne correspond aux mots choisis pour cette écriture."));
        if (classified.Any(r => r.Status == "INVALID")) errors.Add(new("INVALID", "Corrigez les dates ou montants des lignes correspondantes dans la source."));
        if (c.Detail.Status != "VALIDATED") errors.Add(new("SOURCE_NOT_VALIDATED", "Vérifiez et validez le document source avant le téléchargement."));
        errors.AddRange(c.Detail.Errors.Select(e => new AccountingIssue(e.Code, e.Message)));
        if (c.Source.Currency != c.Bank.Currency) errors.Add(new("CURRENCY", "La devise de la source doit correspondre au compte bancaire."));
        try { _ = EntryModelTextExport.Generate(c.Model.Output!, rows); } catch (AppException e) { errors.Add(new("TXT_FORMAT", e.Message)); }
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            formatVersion = EntryModelTextExport.FormatVersion,
            input.ClientId, input.StatementId, input.ExtractId, modelId = c.Model.Id, input.KeywordOverrides,
            decisions = input.Decisions?.OrderBy(d => d.Key).ToArray(), c.Detail.Version, dossierVersion = c.Dossier.Version,
            model = c.Model, bank = new { c.Bank.AccountCode, c.Bank.AccountName, c.Bank.Currency }, rows, errors
        }, Json))));
        return new(input.ClientId, input.StatementId, input.ExtractId, c.Model.Abbreviation, c.Detail.Version, c.Dossier.Version,
            fingerprint, classified, [], errors, errors.Count == 0, c.Model.Output, rows);
    }
    public async Task<EntryModelPreview> Preview(EntryModelRun input, CancellationToken ct)
    {
        var requestedAt = DateTime.UtcNow;
        var context = await Load(input, ct);
        var preview = Evaluate(context, input);
        await new StatementTreatmentStore(db).Save(context.Detail, input.ExtractId, "ENTRY", input, preview, requestedAt, ct);
        return preview;
    }
    public async Task<(byte[] Content, string Mime, string Name)> Export(EntryModelRun input, CancellationToken ct)
    {
        var requestedAt = DateTime.UtcNow;
        Validation.Require(input.Confirmed, "Confirmez la vérification des résultats avant le téléchargement.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"accounting:" + input.ClientId}, 0))", ct);
        _ = await db.StatementImports.FromSqlInterpolated($"SELECT * FROM \"StatementImports\" WHERE \"Id\"={input.StatementId} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new AppException(404, "Document source introuvable.");
        var c = await Load(input, ct); var preview = Evaluate(c, input);
        if (!preview.CanExport) throw new AppException(422, string.Join(" ", preview.Errors.Select(e => e.Message)));
        if (preview.Fingerprint != input.PreviewFingerprint) throw new AppException(409, "Les données ou le modèle ont changé. Recalculez le résultat.");
        await new StatementTreatmentStore(db).Save(c.Detail, input.ExtractId, "ENTRY", input, preview, requestedAt, ct);
        var existing = await db.AccountingEntryArchives.AsNoTracking().SingleOrDefaultAsync(a => a.ClientId == input.ClientId && a.StatementId == input.StatementId && a.PreviewFingerprint == preview.Fingerprint, ct);
        if (existing != null) { await tx.CommitAsync(ct); return await ArchiveFile(existing.Id, ct); }
        var bytes = EntryModelTextExport.Generate(c.Model.Output!, preview.ResultRows!);
        var fileName = $"ecritures_{c.Model.Abbreviation}_{input.StatementId.ToString("N")[..8]}.{c.Model.Output!.FileExtension}";
        var key = await storage.SaveAsync(new MemoryStream(bytes), "entry-exports", c.Model.Output.FileExtension, ct);
        try {
            var clientName = await db.Clients.Where(x => x.Id == input.ClientId).Select(x => x.Name).SingleAsync(ct);
            db.AccountingEntryArchives.Add(new() { ClientId = input.ClientId, StatementId = input.StatementId, ModelId = c.Model.Id,
                ClientName = clientName, BankName = (c.Source.BankName ?? c.Bank.AccountName), SourceFileName = c.Detail.SourceFileName ?? "", ModelName = c.Model.Name + " · " + c.Model.Abbreviation,
                SourceVersion = c.Detail.Version, DossierVersion = c.Dossier.Version, RowCount = preview.ResultRows!.Count,
                FileName = fileName, Encoding = c.Model.Output!.Encoding, Delimiter = c.Model.Output.Delimiter, StorageKey = key,
                ContentSha256 = Convert.ToHexString(SHA256.HashData(bytes)), PreviewFingerprint = preview.Fingerprint,
                GeneratedByName = http.HttpContext?.User.Identity?.Name ?? "Utilisateur",
                SnapshotJson = JsonSerializer.Serialize(new Snapshot(c.Model, c.Model.Output, preview.ResultRows!, c.Source, input.Decisions, input.KeywordOverrides), Json) });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return (bytes, Mime(c.Model.Output), fileName);
        } catch { storage.Delete(key); throw; }
    }
    private sealed record Snapshot(AccountingEntryModel Model, EntryModelOutput Output, IReadOnlyList<EntryResultRow> Rows, BankStatement Source,
        IReadOnlyDictionary<Guid, Guid?>? Decisions, IReadOnlyList<string>? KeywordOverrides);
    public async Task<(byte[] Content, string Mime, string Name)> ModelFile(Guid clientId, Guid modelId, CancellationToken ct)
    {
        var model = (await Get(clientId, ct)).Models.SingleOrDefault(m => m.Id == modelId) ?? throw new AppException(404, "Modèle introuvable pour cette entreprise.");
        return (EntryModelTextExport.Generate(model.Output!, [], true), Mime(model.Output!), $"modele_{model.Abbreviation}.{model.Output!.FileExtension}");
    }
    public async Task<(byte[] Content, string Mime, string Name)> ArchiveFile(Guid id, CancellationToken ct)
    {
        var a = await db.AccountingEntryArchives.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new AppException(404, "Résultat archivé introuvable.");
        using var source = storage.Open(a.StorageKey); using var stream = new MemoryStream(); await source.CopyToAsync(stream, ct); var bytes = stream.ToArray();
        if (Convert.ToHexString(SHA256.HashData(bytes)) != a.ContentSha256) throw new AppException(409, "L'empreinte du fichier archivé ne correspond plus.");
        return (bytes, $"{(a.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? "text/csv" : "text/plain")}; charset={a.Encoding}", a.FileName);
    }
    public async Task<EntryArchiveDetail> Archive(Guid id, CancellationToken ct)
    {
        var a = await db.AccountingEntryArchives.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new AppException(404, "Résultat archivé introuvable.");
        var s = JsonSerializer.Deserialize<Snapshot>(a.SnapshotJson, Json)!; var file = await ArchiveFile(id, ct);
        var text = EntryModelTextExport.EncodingFor(s.Output).GetString(file.Content);
        return new(a.Id, a.ClientId, a.StatementId, a.ClientName, a.BankName, a.SourceFileName, a.ModelName, a.FileName, a.SourceVersion, a.DossierVersion,
            a.CreatedAt, a.GeneratedByName, a.ContentSha256, s.Model, s.Output, s.Rows, text.Length > 200000 ? text[..200000] : text, text.Length > 200000);
    }
    public async Task DeleteArchive(Guid id, CancellationToken ct)
    {
        var archive = await db.AccountingEntryArchives.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new AppException(404, "Résultat archivé introuvable.");
        if (!string.IsNullOrWhiteSpace(archive.StorageKey))
            db.PendingFileDeletions.Add(new() { StorageKey = archive.StorageKey });
        db.AccountingEntryArchives.Remove(archive);
        await db.SaveChangesAsync(ct);
    }
    private static string Mime(EntryModelOutput output) => $"{(output.FileExtension == "csv" ? "text/csv" : "text/plain")}; charset={output.Encoding}";
}

