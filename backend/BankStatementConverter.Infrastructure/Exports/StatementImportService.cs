using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
namespace BankStatementConverter.Infrastructure.Exports;

public sealed class StatementImportService(AppDbContext db, StatementValidator validator, IExportEngine engine, IFileStorageService storage, IHttpContextAccessor? http = null)
{
    private string Actor => http?.HttpContext?.User.Identity?.Name ?? WorkspaceIdentity.Name;
    public static readonly JsonSerializerOptions Json = StatementJson.CreateOptions();
    public async Task<PageResult<StatementSummary>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        Validation.Require(page >= 1 && pageSize is >= 1 and <= 100, "Pagination invalide.");
        var query = db.StatementImports.AsNoTracking();
        return new(await query.OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new StatementSummary(s.Id, s.BankName, s.StatementNumber, s.Currency, s.PeriodStart, s.PeriodEnd, s.Status, s.Version, s.TransactionCount, s.CreatedAt)).ToListAsync(ct), await query.CountAsync(ct), page, pageSize);
    }
    private async Task<StatementImport> FindAsync(Guid id, CancellationToken ct) => await db.StatementImports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException(404, "Relevé normalisé introuvable.");
    public async Task<StatementDetail> GetAsync(Guid id, CancellationToken ct) => Detail(await FindAsync(id, ct));
    private StatementDetail Detail(StatementImport record)
    {
        var statement = StatementAmounts.Normalize(JsonSerializer.Deserialize<BankStatement>(record.NormalizedJson, Json)!) with
        {
            IsValidated = record.Status == "VALIDATED"
        };
        var diagnostics = validator.Validate(statement);
        var importWarnings = record.ImportDiagnosticsJson == null ? [] : JsonSerializer.Deserialize<List<ExportError>>(record.ImportDiagnosticsJson, Json)!;
        var raw = record.RawExtractionJson == null ? null : JsonSerializer.Deserialize<PdfTextDocument>(record.RawExtractionJson, Json);
        var extraction = raw == null ? null : new ExtractionSummary(raw.Method, raw.Pages.Count, raw.Pages.Sum(p => p.Boxes.Count(b => b.Confidence < 0.90)), statement.Transactions.Count == 0, raw.Performance);
        var coverageErrors = raw?.Method == "HYBRID" ? ExtractionAudit.Errors(statement, raw) : [];
        return new(record.Id, record.Status, record.Version, record.ValidatedAt, statement, diagnostics.Warnings.Concat(importWarnings).ToList(), diagnostics.Errors.Concat(coverageErrors).ToArray(), record.SourceFileName, extraction, record.ClientId, record.BankAccountId, record.ImportedByName, record.ModifiedByName);
    }
    private static void CheckInput(BankStatement s, bool allowEmpty = false)
    {
        Validation.Require(s.ConversionType is "STATEMENT" or "EXTRACT", "Type de conversion invalide.");
        Validation.Text(s.BankName, "Nom de la banque");
        Validation.Require(s.Transactions != null && s.Transactions.Count >= (allowEmpty ? 0 : 1) && s.Transactions.All(t => t != null), "Importez des transactions non vides.");
        foreach (var property in typeof(BankStatement).GetProperties().Where(p => p.PropertyType == typeof(string)))
            Validation.Require(((string?)property.GetValue(s))?.Length is null or <= 2000, "Métadonnée du relevé trop longue.");
        foreach (var transaction in s.Transactions!)
            foreach (var property in typeof(StatementTransaction).GetProperties().Where(p => p.PropertyType == typeof(string)))
                Validation.Require(((string?)property.GetValue(transaction))?.Length is null or <= 10000, "Métadonnée de transaction trop longue.");
    }
    public async Task<StatementDetail> CreateAsync(BankStatement s, CancellationToken ct)
    {
        CheckInput(s);
        var record = new StatementImport { ImportedByName = Actor };
        Store(record, s with
        {
            Id = record.Id,
            IsValidated = false
        });
        db.StatementImports.Add(record);
        await db.SaveChangesAsync(ct);
        return Detail(record);
    }
    public async Task<StatementDetail> CreateExtractedAsync(PdfStatementExtraction extracted, CancellationToken ct, string? sourceFileName = null, string? sourceStorageKey = null, Guid? clientId = null, Guid? bankAccountId = null)
    {
        CheckInput(extracted.Statement, extracted.RawDocument?.Pages.Any(p => p.Boxes.Count > 0) == true);
        var record = new StatementImport { ImportedByName = Actor, SourceFileName = sourceFileName, SourceStorageKey = sourceStorageKey, ImportDiagnosticsJson = JsonSerializer.Serialize(extracted.Warnings, Json), RawExtractionJson = extracted.RawDocument == null ? null : JsonSerializer.Serialize(extracted.RawDocument, Json) };
        Store(record, extracted.Statement with
        {
            Id = record.Id,
            IsValidated = false
        });
        if (clientId.HasValue)
            await AssociateRecordAsync(record, clientId.Value, bankAccountId!.Value, ct);
        db.StatementImports.Add(record);
        await db.SaveChangesAsync(ct);
        return Detail(record);
    }
    public async Task<PdfTextDocument> ExtractionAsync(Guid id, CancellationToken ct)
    {
        var record = await FindAsync(id, ct);
        return record.RawExtractionJson == null ? throw new AppException(404, "Cet ancien import ne possède pas de texte extrait enregistré. Réimportez le PDF pour le conserver.")
            : JsonSerializer.Deserialize<PdfTextDocument>(record.RawExtractionJson, Json)!;
    }
    public async Task<StatementDetail> CompleteMetadataAsync(Guid id, int version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var record = await LockAsync(id, version, ct);
        if (record.RawExtractionJson == null)
            throw new AppException(404, "Réimportez ce PDF pour compléter les informations à partir du texte extrait.");
        var old = Detail(record).Statement;
        var raw = JsonSerializer.Deserialize<PdfTextDocument>(record.RawExtractionJson, Json)!;
        var warnings = record.ImportDiagnosticsJson == null ? [] : JsonSerializer.Deserialize<List<ExportError>>(record.ImportDiagnosticsJson, Json)!;
        var completed = PdfStatementMetadata.Complete(new(old, warnings, raw), old);
        Store(record, completed.Statement);
        record.Version++;
        record.ModifiedByName = Actor;
        record.ImportDiagnosticsJson = JsonSerializer.Serialize(completed.Warnings.DistinctBy(w => (w.Code, w.TransactionSequence, w.SourceField)), Json);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Detail(record);
    }
    public async Task<StatementDetail> AdaptAsync(Guid id, AdaptExtractionRequest request, StatementPdfExtractor normalizer, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var record = await LockAsync(id, request.StatementVersion, ct);
        var old = Detail(record).Statement;
        if (record.RawExtractionJson == null)
            throw new AppException(404, "Réimportez ce PDF pour disposer de son texte extrait.");
        var raw = JsonSerializer.Deserialize<PdfTextDocument>(record.RawExtractionJson, Json)!;
        if (request.Mapping == null && raw.Method == "PADDLE_OCR_VL_1_6" && raw.NativePages == null && record.SourceStorageKey != null)
        {
            using var original = storage.Open(record.SourceStorageKey);
            using var content = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await original.ReadAsync(buffer, ct)) > 0)
            {
                await content.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            var native = normalizer.ReadDocument(content.ToArray(), ct);
            if (native.Pages.All(p => p.Boxes.Count >= 10))
                raw = raw with
                {
                    NativePages = native.Pages
                };
        }
        if (request.ProfileId.HasValue)
        {
            var profile = await db.ExtractionProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.ProfileId && p.IsActive, ct)
                ?? throw new AppException(404, "Modèle d’extraction indisponible.");
            if (profile.BankCode != old.BankCode || profile.Currency != old.Currency)
                throw new AppException(400, "Ce modèle appartient à une autre banque ou devise.");
            raw = ExtractionProfileRules.ApplyAliases(raw, JsonSerializer.Deserialize<Dictionary<string, string[]>>(profile.AliasesJson, Json)!);
            request = request with
            {
                Mapping = JsonSerializer.Deserialize<PdfColumnMapping>(profile.MappingJson, Json)
            };
        }
        var priorWarnings = record.ImportDiagnosticsJson == null ? [] : JsonSerializer.Deserialize<List<ExportError>>(record.ImportDiagnosticsJson, Json)!;
        var metadata = StatementAdaptationMetadata.ForReplay(old, priorWarnings);
        var extracted = normalizer.Normalize(raw, old.BankName, old.BankCode ?? "", old.Currency ?? "", ct, request.Mapping, metadata);
        var statement = extracted.Statement with
        {
            Id = old.Id,
            OpeningBalance = old.OpeningBalance ?? extracted.Statement.OpeningBalance,
            ClosingBalance = old.ClosingBalance ?? extracted.Statement.ClosingBalance,
            PeriodStart = old.PeriodStart ?? extracted.Statement.PeriodStart,
            PeriodEnd = old.PeriodEnd ?? extracted.Statement.PeriodEnd,
            OpeningBalanceDate = old.OpeningBalanceDate ?? extracted.Statement.OpeningBalanceDate,
            ClosingBalanceDate = old.ClosingBalanceDate ?? extracted.Statement.ClosingBalanceDate,
            OpeningBalanceDescription = old.OpeningBalanceDescription ?? extracted.Statement.OpeningBalanceDescription,
            ClosingBalanceDescription = old.ClosingBalanceDescription ?? extracted.Statement.ClosingBalanceDescription,
            OpeningBalanceValueDate = old.OpeningBalanceValueDate ?? extracted.Statement.OpeningBalanceValueDate,
            ClosingBalanceValueDate = old.ClosingBalanceValueDate ?? extracted.Statement.ClosingBalanceValueDate,
            AccountNumber = old.AccountNumber ?? extracted.Statement.AccountNumber,
            StatementNumber = old.StatementNumber ?? extracted.Statement.StatementNumber,
            BalanceConvention = old.BalanceConvention ?? extracted.Statement.BalanceConvention,
            TotalDebit = metadata.TotalDebit ?? extracted.Statement.TotalDebit,
            TotalCredit = metadata.TotalCredit ?? extracted.Statement.TotalCredit,
            Journal = old.Journal,
            Account = old.Account,
            Analytic = old.Analytic,
            RecordType = old.RecordType
        };
        CheckInput(statement);
        Store(record, statement);
        record.Version++;
        record.ModifiedByName = Actor;
        var retained = priorWarnings.Where(w => w.Code == "OCR_REVIEW_REQUIRED").ToList();
        record.ImportDiagnosticsJson = JsonSerializer.Serialize(extracted.Warnings.Concat(retained), Json);
        record.RawExtractionJson = JsonSerializer.Serialize(raw with
        {
            LastMapping = request.Mapping
        }, Json);
        if (request.ProfileId.HasValue)
            db.StatementActivities.Add(new()
            {
                StatementId = record.Id,
                ClientId = record.ClientId,
                BankAccountId = record.BankAccountId,
                BankName = record.BankName,
                FileName = record.SourceFileName ?? record.BankName,
                Action = "MODEL_APPLIED",
                ActorName = Actor,
                Version = record.Version,
                ChangesJson = JsonSerializer.Serialize(new
                {
                    profileId = request.ProfileId,
                    mapping = request.Mapping
                }, Json)
            });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Detail(record);
    }
    private static void Store(StatementImport r, BankStatement s)
    {
        // Extracts are immutable snapshots managed by their dedicated endpoint.
        var previous = JsonSerializer.Deserialize<BankStatement>(r.NormalizedJson, Json);
        s = s with
        {
            Extracts = previous?.Extracts ?? [],
            ConversionType = r.NormalizedJson == "{}" ? s.ConversionType : previous?.ConversionType ?? "STATEMENT"
        };
        s = StatementAmounts.Normalize(s);
        r.BankName = s.BankName;
        r.StatementNumber = s.StatementNumber;
        r.Currency = s.Currency;
        r.PeriodStart = s.PeriodStart;
        r.PeriodEnd = s.PeriodEnd;
        r.TransactionCount = s.Transactions.Count;
        r.NormalizedJson = JsonSerializer.Serialize(s, Json);
        r.Status = "NEEDS_REVIEW";
        r.ValidatedAt = null;
    }
    public async Task<StatementDetail> SaveExtractAsync(Guid id, int version, string filterText, CancellationToken ct, string excludeText = "", BankStatement? updatedStatement = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var record = await LockAsync(id, version, ct);
        if (updatedStatement != null)
        {
            CheckInput(updatedStatement, record.RawExtractionJson != null);
            Store(record, updatedStatement with
            {
                Id = record.Id,
                IsValidated = false
            });
        }
        var statement = Detail(record).Statement;
        Validation.Require(statement.Extracts.Count < 100, "Ce relevé possède déjà 100 extraits.");
        var extract = StatementExtracts.Create(statement, filterText, version, excludeText);
        record.NormalizedJson = JsonSerializer.Serialize(statement with
        {
            Extracts = [.. statement.Extracts, extract]
        }, Json);
        record.Version++;
        record.ModifiedByName = Actor;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Detail(record);
    }
    public async Task<StatementDetail> ResetExtractAsync(Guid id, Guid extractId, int version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var record = await LockAsync(id, version, ct);
        var statement = Detail(record).Statement;
        if (!statement.Extracts.Any(x => x.Id == extractId))
            throw new AppException(404, "Extrait introuvable.");
        record.NormalizedJson = JsonSerializer.Serialize(statement with
        {
            Extracts = statement.Extracts.Where(x => x.Id != extractId).ToList()
        }, Json);
        record.Version++;
        record.ModifiedByName = Actor;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Detail(record);
    }
    private async Task<StatementImport> LockAsync(Guid id, int version, CancellationToken ct)
    {
        var record = await db.StatementImports.FromSqlInterpolated($"SELECT * FROM \"StatementImports\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new AppException(404, "Relevé normalisé introuvable.");
        if (record.Version != version)
            throw new AppException(409, "Le relevé a changé. Rechargez les données avant de poursuivre.");
        return record;
    }
    public async Task<StatementDetail> UpdateAsync(Guid id, int version, BankStatement s, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var record = await LockAsync(id, version, ct);
        CheckInput(s, record.RawExtractionJson != null);
        Store(record, s with
        {
            Id = record.Id,
            IsValidated = false
        });
        record.Version++;
        record.ModifiedByName = Actor;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Detail(record);
    }
    public async Task<StatementDetail> CompleteDownloadAsync(Guid id, int version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var record = await LockAsync(id, version, ct);
        if (!await db.ExportJobs.AnyAsync(x => x.StatementId == id && x.StatementVersion == version, ct))
            throw new AppException(409, "Téléchargez le fichier de cette version avant de terminer.");
        record.Status = record.Status == "VALIDATED" ? "VALIDATED" : "EXPORTED";
        record.Version++;
        record.ModifiedByName = Actor;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Detail(record);
    }
    public async Task<StatementDetail> ValidateAsync(Guid id, int version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var record = await LockAsync(id, version, ct);
        var detail = Detail(record);
        if (detail.Errors.Count > 0)
            return detail;
        record.Status = "VALIDATED";
        record.ValidatedAt = DateTime.UtcNow;
        record.Version++;
        record.ModifiedByName = Actor;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Detail(record);
    }
    private async Task<ExportTemplate> TemplateAsync(Guid id, CancellationToken ct) => await db.ExportTemplates.AsNoTracking().Include(t => t.Fields).SingleOrDefaultAsync(t => t.Id == id, ct)
        ?? throw new AppException(404, "Modèle d’exportation introuvable.");
    private static string Snapshot(ExportTemplate t) => JsonSerializer.Serialize(new ExportDto(t.Id, t.Name, t.Type, t.Description, t.FileExtension, t.Delimiter, t.Encoding, t.NumberCulture, t.Type != "SAGE100" && t.IncludeHeader, t.IsActive,
        t.Fields.OrderBy(f => f.Position).Select(f => new ExportFieldDto(f.FieldName, f.SourceField, f.Position, f.Format, f.Required)).ToList()), Json);
    private static string Fingerprint(string json) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)));
    private static string TemplateFingerprint(ExportTemplate template, string snapshot) => Fingerprint((template.Type is "SAGEX3" or "SAGE100" ? SageTextCompatibility.ProfileVersion : "") + (template.Type == "SAGE100" ? "Sage100-no-header-v1:" : "") + (template.Type == "SAGEX3" ? SageX3Rows.ProfileVersion : "") + snapshot);
    private static string ScopedFingerprint(ExportTemplate template, string snapshot, Guid? extractId) => TemplateFingerprint(template, snapshot) + (extractId.HasValue ? ":" + extractId.Value.ToString("N") : "");
    public async Task<PreparedStatementExport> PrepareAsync(Guid id, PrepareStatementExportRequest request, StatementPdfExtractor normalizer, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var record = await LockAsync(id, request.StatementVersion, ct);
        var template = await TemplateAsync(request.TemplateId, ct);
        Validation.Require(template.IsActive, "Choisissez un modèle d’exportation actif.");
        var before = Detail(record);
        var diagnostics = record.ImportDiagnosticsJson == null ? [] : JsonSerializer.Deserialize<List<ExportError>>(record.ImportDiagnosticsJson, Json)!;
        var raw = record.RawExtractionJson == null ? null : JsonSerializer.Deserialize<PdfTextDocument>(record.RawExtractionJson, Json);
        var completed = StatementExportPreparation.Complete(before.Statement, template, diagnostics, raw, normalizer, ct);
        // Merely selecting a model must not invalidate an already verified statement.
        if (JsonSerializer.Serialize(before.Statement, Json) != JsonSerializer.Serialize(completed.Statement, Json))
        {
            CheckInput(completed.Statement, raw != null);
            Store(record, completed.Statement with
            {
                IsValidated = false
            });
            record.Version++;
            record.ModifiedByName = Actor;
            record.ImportDiagnosticsJson = JsonSerializer.Serialize(completed.Warnings, Json);
            await db.SaveChangesAsync(ct);
        }
        var detail = Detail(record);
        var preview = await engine.PreviewAsync(StatementExtracts.ForExport(detail.Statement, request.ExtractId), template, ct);
        preview = preview with
        {
            StatementVersion = record.Version,
            ExtractId = request.ExtractId,
            TemplateFingerprint = ScopedFingerprint(template, Snapshot(template), request.ExtractId),
            Warnings = detail.Warnings.Concat(preview.Warnings).DistinctBy(w => (w.Code, w.TransactionSequence, w.FieldName, w.SourceField)).ToList()
        };
        await tx.CommitAsync(ct);
        return new(detail, preview);
    }
    public async Task<ExportPreview> PreviewAsync(Guid id, Guid templateId, ExportRequest request, CancellationToken ct)
    {
        var record = await FindAsync(id, ct);
        if (record.Version != request.StatementVersion)
            throw new AppException(409, "Le relevé a changé. Rechargez-le.");
        var template = await TemplateAsync(templateId, ct);
        var detail = Detail(record);
        var preview = await engine.PreviewAsync(StatementExtracts.ForExport(detail.Statement, request.ExtractId), template, ct);
        return preview with
        {
            StatementVersion = record.Version,
            ExtractId = request.ExtractId,
            TemplateFingerprint = ScopedFingerprint(template, Snapshot(template), request.ExtractId)
        };
    }
    public async Task<ExportResult> ExportAsync(Guid id, Guid templateId, ExportRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var record = await LockAsync(id, request.StatementVersion, ct);
        var template = await TemplateAsync(templateId, ct);
        var snapshot = Snapshot(template);
        if (request.TemplateFingerprint != null && request.TemplateFingerprint != ScopedFingerprint(template, snapshot, request.ExtractId))
            throw new AppException(409, "Le modèle, l’extrait ou les règles d’export ont changé depuis l’aperçu. Affichez un nouvel aperçu.");
        if (request.EditedText != null && request.TemplateFingerprint == null)
            throw new AppException(409, "Affichez un aperçu avant de modifier le texte du fichier.");
        var source = Detail(record).Statement;
        var exportStatement = StatementExtracts.ForExport(source, request.ExtractId);
        var result = await engine.GenerateAsync(exportStatement, template, ct, new(request.AllowErrors, request.EditedText));
        if (request.ExtractId.HasValue)
            result = result with
            {
                FileName = "extrait_" + result.FileName
            };
        if (result.DownloadAllowed)
        {
            var classificationSnapshot = System.Text.Json.Nodes.JsonNode.Parse(snapshot)!.AsObject();
            classificationSnapshot["conversionType"] = request.ExtractId.HasValue || source.ConversionType == "EXTRACT" ? "EXTRACT" : "STATEMENT";
            snapshot = classificationSnapshot.ToJsonString(Json);
            if (request.ExtractId.HasValue)
            {
                var extract = source.Extracts.Single(x => x.Id == request.ExtractId);
                var audit = System.Text.Json.Nodes.JsonNode.Parse(snapshot)!.AsObject();
                audit["extract"] = JsonSerializer.SerializeToNode(new
                {
                    extract.Id,
                    extract.FilterText,
                    extract.ExcludeText,
                    extract.TotalDebit,
                    extract.TotalCredit,
                    extract.Balance
                }, Json);
                snapshot = audit.ToJsonString(Json);
            }
            if (request.AllowErrors || result.TextEdited)
            {
                var audit = System.Text.Json.Nodes.JsonNode.Parse(snapshot)!.AsObject();
                audit["downloadWithErrors"] = request.AllowErrors;
                audit["textEdited"] = result.TextEdited;
                audit["actualEncoding"] = result.Encoding;
                audit["diagnostics"] = JsonSerializer.SerializeToNode(new
                {
                    result.Errors,
                    result.Warnings
                }, Json);
                snapshot = audit.ToJsonString(Json);
            }
            string? saved = null, standard = null;
            try
            {
                using var content = new MemoryStream(result.Content);
                saved = await storage.SaveAsync(content, "exports", result.FileExtension, ct);
                using var csv = new MemoryStream(request.ExtractId.HasValue ? StatementExtracts.Csv(source.Extracts.Single(x => x.Id == request.ExtractId)) : StandardStatementCsv.Generate(exportStatement));
                standard = await storage.SaveAsync(csv, "exports", "csv", ct);
                db.ExportJobs.Add(new()
                {
                    StatementId = record.Id,
                    StatementVersion = record.Version,
                    TemplateId = template.Id,
                    TemplateNameSnapshot = template.Name,
                    TemplateSnapshotJson = snapshot,
                    StorageKey = saved,
                    StandardStorageKey = standard,
                    GeneratedByName = Actor,
                    RowCount = result.RowCount,
                    Extension = result.FileExtension,
                    Encoding = result.Encoding ?? template.Encoding,
                    Delimiter = template.Delimiter,
                    FileName = result.FileName,
                    ContentSha256 = Convert.ToHexString(SHA256.HashData(result.Content)),
                    Status = request.AllowErrors ? "FORCED" : result.TextEdited ? "EDITED" : "SUCCEEDED"
                });
                db.StatementActivities.Add(new()
                {
                    StatementId = record.Id,
                    ClientId = record.ClientId,
                    BankAccountId = record.BankAccountId,
                    ClientName = await db.Clients.Where(x => x.Id == record.ClientId).Select(x => x.Name).FirstOrDefaultAsync(ct) ?? "",
                    AccountName = await db.BankAccounts.Where(x => x.Id == record.BankAccountId).Select(x => x.AccountName).FirstOrDefaultAsync(ct) ?? "",
                    BankName = record.BankName,
                    FileName = record.SourceFileName ?? record.StatementNumber ?? record.BankName,
                    Action = "EXPORTED",
                    ActorName = Actor,
                    ActorId = http?.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    Version = record.Version
                });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch { if (saved != null) storage.Delete(saved); if (standard != null) storage.Delete(standard); throw; }
        }
        return result;
    }
    public async Task<PageResult<ExportJobDto>> HistoryAsync(Guid id, int page, int pageSize, CancellationToken ct)
    {
        _ = await FindAsync(id, ct);
        Validation.Require(page >= 1 && pageSize is >= 1 and <= 100, "Pagination invalide.");
        var query = db.ExportJobs.AsNoTracking().Where(j => j.StatementId == id);
        return new(await query.OrderByDescending(j => j.CreatedAt).ThenBy(j => j.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(j => new ExportJobDto(j.Id, j.TemplateId, j.TemplateNameSnapshot, j.StatementVersion, j.FileName, j.RowCount, j.Extension, j.Encoding, j.CreatedAt, j.Status, j.ContentSha256, j.StorageKey != null, j.GeneratedByName)).ToListAsync(ct), await query.CountAsync(ct), page, pageSize);
    }
    public async Task<StatementDetail> AssociateAsync(Guid id, StatementAssociation input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var record = await LockAsync(id, input.Version, ct);
        var previousAccountId = record.BankAccountId;
        await AssociateRecordAsync(record, input.ClientId, input.BankAccountId, ct);
        record.Version++;
        record.ModifiedByName = Actor;
        await db.SaveChangesAsync(ct);
        if (previousAccountId != input.BankAccountId)
            await RetainLatestAsync(input.BankAccountId, ct);
        await tx.CommitAsync(ct);
        return Detail(record);
    }
    public async Task AssociateRecordAsync(StatementImport record, Guid clientId, Guid accountId, CancellationToken ct)
    {
        // Serialize filing per account, including concurrent PDF imports and associations.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({accountId.ToString()}, 0))", ct);
        var account = await db.BankAccounts.AsNoTracking().Include(a => a.Bank).SingleOrDefaultAsync(a => a.Id == accountId && a.ClientId == clientId, ct)
            ?? throw new AppException(400, "Ce compte bancaire n’appartient pas au client sélectionné.");
        var statement = Detail(record).Statement;
        if (!string.Equals(statement.BankName, account.Bank.Name, StringComparison.OrdinalIgnoreCase) && !string.Equals(statement.BankCode, account.Bank.Code, StringComparison.OrdinalIgnoreCase))
            throw new AppException(400, "La banque du compte doit correspondre à celle du relevé.");
        if (!string.Equals(statement.Currency, account.Currency, StringComparison.OrdinalIgnoreCase))
            throw new AppException(400, "La devise du compte doit correspondre à celle du relevé.");
        if (record.BankAccountId != accountId)
            record.FiledAt = DateTime.UtcNow;
        record.ClientId = clientId;
        record.BankAccountId = accountId;
        // Accounting defaults apply to unfilled fields only. OCR data is kept for review.
        Store(record, statement with
        {
            Journal = statement.Journal ?? account.Journal,
            Account = statement.Account ?? account.AccountCode,
            AccountNumber = statement.AccountNumber ?? account.AccountNumber
        });
        var warnings = record.ImportDiagnosticsJson == null ? [] : JsonSerializer.Deserialize<List<ExportError>>(record.ImportDiagnosticsJson, Json)!;
        warnings.RemoveAll(w => w.Code == "ACCOUNT_IDENTITY_MISMATCH");
        static string Digits(string? s) => new((s ?? "").Where(char.IsAsciiDigit).ToArray());
        if (!string.IsNullOrWhiteSpace(statement.AccountNumber) && Digits(statement.AccountNumber) != Digits(account.AccountNumber))
            warnings.Add(new("ACCOUNT_IDENTITY_MISMATCH", "Le numéro lu dans le PDF diffère du compte choisi. Vérifiez l’identité du relevé.", SourceField: "AccountNumber"));
        record.ImportDiagnosticsJson = JsonSerializer.Serialize(warnings, Json);
    }
    public async Task DeleteAsync(IReadOnlyList<StatementVersionToDelete> items, CancellationToken ct)
    {
        Validation.Require(items.Count is > 0 and <= 100 && items.Select(x => x.Id).Distinct().Count() == items.Count, "Sélectionnez entre 1 et 100 relevés différents.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var records = new List<StatementImport>();
        foreach (var item in items.OrderBy(x => x.Id))
        {
            var record = await db.StatementImports.FromSqlInterpolated($"SELECT * FROM \"StatementImports\" WHERE \"Id\" = {item.Id} FOR UPDATE")
                .SingleOrDefaultAsync(ct) ?? throw new AppException(404, "Un relevé sélectionné n’existe plus. Actualisez la liste.");
            if (record.Version != item.Version)
                throw new AppException(409, "Un relevé sélectionné a été modifié. Actualisez la liste avant de le supprimer.");
            records.Add(record);
        }
        var ids = records.Select(x => x.Id).ToList();
        var jobs = await db.ExportJobs.Where(x => ids.Contains(x.StatementId)).ToListAsync(ct);
        foreach (var key in records.Select(x => x.SourceStorageKey).Concat(jobs.SelectMany(x => new[] { x.StorageKey, x.StandardStorageKey })).Where(x => x != null).Distinct())
            db.PendingFileDeletions.Add(new()
            {
                StorageKey = key!
            });
        db.ExportJobs.RemoveRange(jobs);
        db.StatementImports.RemoveRange(records);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task RetainLatestAsync(Guid accountId, CancellationToken ct)
    {
        // Called inside the filing transaction, under the per-account advisory lock.
        var obsolete = await db.StatementImports.Where(x => x.BankAccountId == accountId)
            .OrderByDescending(x => x.FiledAt ?? x.CreatedAt).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip(5).ToListAsync(ct);
        if (obsolete.Count == 0)
            return;
        var ids = obsolete.Select(x => x.Id).ToList();
        var jobs = await db.ExportJobs.Where(x => ids.Contains(x.StatementId)).ToListAsync(ct);
        foreach (var key in obsolete.Select(x => x.SourceStorageKey).Concat(jobs.SelectMany(x => new[] { x.StorageKey, x.StandardStorageKey })).Where(x => x != null).Distinct())
            db.PendingFileDeletions.Add(new()
            {
                StorageKey = key!
            });
        db.ExportJobs.RemoveRange(jobs);
        db.StatementImports.RemoveRange(obsolete);
        await db.SaveChangesAsync(ct);
    }
    public async Task<ExportJob> ArchivedJobAsync(Guid id, Guid jobId, CancellationToken ct) => await db.ExportJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == jobId && j.StatementId == id, ct) ?? throw new AppException(404, "Export archivé introuvable.");
    public async Task<(byte[] Content, string FileName, string MimeType)> ArchivedDownloadAsync(Guid id, Guid jobId, bool standard, CancellationToken ct)
    {
        var job = await ArchivedJobAsync(id, jobId, ct);
        var key = standard ? job.StandardStorageKey : job.StorageKey;
        if (key == null)
            throw new AppException(404, "Cet ancien export n’a pas de fichier conservé. Générez un nouvel export après vérification.");
        using var source = storage.Open(key);
        using var content = new MemoryStream();
        await source.CopyToAsync(content, ct);
        var bytes = content.ToArray();
        if (!standard && Convert.ToHexString(SHA256.HashData(bytes)) != job.ContentSha256)
            throw new AppException(409, "Le fichier archivé ne correspond plus à son empreinte.");
        return (bytes, standard ? Path.GetFileNameWithoutExtension(job.FileName) + "_standard.csv" : job.FileName, standard ? "text/csv; charset=utf-8" : $"{(job.Extension == "csv" ? "text/csv" : "text/plain")}; charset={job.Encoding}");
    }
}

public sealed record StatementVersionToDelete(Guid Id, int Version);
