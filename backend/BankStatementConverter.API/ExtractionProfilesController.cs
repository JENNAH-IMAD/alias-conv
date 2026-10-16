using System.Text.Json;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BankStatementConverter.API;

[ApiController, Route("api/extraction-profiles")]
public class ExtractionProfilesController(AppDbContext db, StatementImportService imports, StatementPdfExtractor normalizer) : ControllerBase
{
    private string Actor => User.Identity?.Name ?? WorkspaceIdentity.Name;
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.ExtractionProfiles.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));
    [HttpGet("history")]
    public async Task<IActionResult> History(CancellationToken ct, int page = 1, int pageSize = 20)
    {
        Validation.Require(page >= 1 && pageSize is >= 1 and <= 100, "Pagination invalide.");
        var query = db.ExtractionProfileActivities.AsNoTracking();
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { x.Id, x.ProfileId, x.ProfileName, x.Action, x.ActorName, x.Version, x.CreatedAt, x.SnapshotJson }).ToListAsync(ct);
        return Ok(new
        {
            items,
            total = await query.CountAsync(ct),
            page,
            pageSize
        });
    }
    [HttpPost] public Task<ExtractionProfile> Create(ProfileInput input, CancellationToken ct) => Save(null, input, ct);
    [HttpPut("{id:guid}")] public Task<ExtractionProfile> Update(Guid id, ProfileInput input, CancellationToken ct) => Save(id, input, ct);
    private async Task<ExtractionProfile> Save(Guid? id, ProfileInput input, CancellationToken ct)
    {
        Validation.Text(input.Name, "Nom du modèle", 150);
        Validation.Require(input.Mapping != null, "Réglez les colonnes avant d’enregistrer le modèle.");
        ExtractionProfileRules.ValidateAliases(input.Aliases);
        var mapping = input.Mapping ?? throw new AppException(400, "Mapping required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var detail = await imports.GetAsync(input.StatementId, ct);
        if (detail.Version != input.StatementVersion)
            throw new AppException(409, "Le relevé a changé. Actualisez avant d’enregistrer le modèle.");
        var raw = await imports.ExtractionAsync(input.StatementId, ct);
        normalizer.Normalize(ExtractionProfileRules.ApplyAliases(raw, input.Aliases), detail.Statement.BankName, detail.Statement.BankCode ?? "", detail.Statement.Currency ?? "", ct, mapping, detail.Statement);
        var profile = id.HasValue ? await db.ExtractionProfiles.FromSqlInterpolated($"SELECT * FROM \"ExtractionProfiles\" WHERE \"Id\" = {id.Value} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new AppException(404, "Modèle introuvable.") : new ExtractionProfile { CreatedByName = Actor };
        if (id.HasValue && profile.Version != input.Version)
            throw new AppException(409, "Le modèle a changé. Actualisez la liste.");
        profile.Name = input.Name.Trim();
        profile.BankCode = detail.Statement.BankCode ?? "";
        profile.Currency = detail.Statement.Currency ?? "";
        profile.MappingJson = JsonSerializer.Serialize(mapping, StatementImportService.Json);
        // Retain header geometry only; no customer transaction data in a reusable model.
        var first = raw.Pages.First();
        var tables = mapping.TableColumns is { } columns && first.Tables?.FirstOrDefault() is { } table
            ? new List<PdfStructuredTable> { table with { Rows = table.Rows.Take(columns.StartRow).Select((row, index) => (IReadOnlyList<string>)(index == columns.StartRow - 1 ? row.ToList() : row.Select(_ => "").ToList())).ToList() } } : null;
        var nearby = first.Boxes.Where(box => Math.Abs(box.Bottom - first.Height * mapping.HeaderTop / 100) <= first.Height * .05).ToList();
        var debitCaption = nearby.FirstOrDefault(box => StatementFieldNames.Column(box.Text) == "Debit" || (input.Aliases.GetValueOrDefault("Debit") ?? []).Any(alias => ExtractionProfileRules.Fold(alias) == ExtractionProfileRules.Fold(box.Text)));
        var boxes = debitCaption == null ? new List<PdfTextBox>() : nearby.Where(box => Math.Abs(box.Top - debitCaption.Top) <= first.Height * .008 && !box.Text.Any(char.IsDigit)).ToList();
        if (tables != null && tables[0].Rows.LastOrDefault() is { } header && (header.Any(cell => cell.Any(char.IsDigit)) || header.Count(cell => !string.IsNullOrWhiteSpace(cell)) < 3))
            tables = null;
        profile.SampleJson = JsonSerializer.Serialize(new PdfTextDocument(raw.Method, [first with { Boxes = boxes, Tables = tables, RawResult = null, Markdown = null }], mapping), StatementImportService.Json);
        profile.AliasesJson = JsonSerializer.Serialize(input.Aliases, StatementImportService.Json);
        profile.IsActive = input.IsActive;
        profile.ModifiedByName = Actor;
        if (id.HasValue)
            profile.Version++;
        else
            db.ExtractionProfiles.Add(profile);
        db.ExtractionProfileActivities.Add(new()
        {
            ProfileId = profile.Id,
            ProfileName = profile.Name,
            Action = id.HasValue ? "UPDATED" : "CREATED",
            ActorName = Actor,
            Version = profile.Version,
            SnapshotJson = JsonSerializer.Serialize(new
            {
                profile.Name,
                profile.BankCode,
                profile.Currency,
                mapping,
                input.Aliases,
                profile.IsActive
            }, StatementImportService.Json)
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return profile;
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] int version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var profile = await db.ExtractionProfiles.FromSqlInterpolated($"SELECT * FROM \"ExtractionProfiles\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new AppException(404, "Modèle introuvable.");
        if (profile.Version != version)
            throw new AppException(409, "Le modèle a changé.");
        db.ExtractionProfileActivities.Add(new()
        {
            ProfileId = id,
            ProfileName = profile.Name,
            Action = "DELETED",
            ActorName = Actor,
            Version = profile.Version,
            SnapshotJson = JsonSerializer.Serialize(profile, StatementImportService.Json)
        });
        db.ExtractionProfiles.Remove(profile);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }
}
public record ProfileInput(string Name, Guid StatementId, int StatementVersion, PdfColumnMapping Mapping, Dictionary<string, string[]> Aliases, bool IsActive = true, int Version = 1);
