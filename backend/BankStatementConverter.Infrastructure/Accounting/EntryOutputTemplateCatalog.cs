using System.Text.Json;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.EntityFrameworkCore;

namespace BankStatementConverter.Infrastructure.Accounting;

public sealed class EntryOutputTemplateCatalog(AppDbContext db)
{
    public static readonly Guid DefaultId = Guid.Parse("9d2e4a78-fb36-4820-9c04-87b370743915");
    private static readonly JsonSerializerOptions Json = StatementJson.CreateOptions();
    public static EntryModelOutput Output(EntryOutputTemplate item) => JsonSerializer.Deserialize<EntryModelOutput>(item.OutputJson, Json) ?? new();
    private static EntryOutputTemplateDto Dto(EntryOutputTemplate item) => new(item.Id, item.Name, item.Description, item.Active, item.Version, Output(item));
    public async Task<IReadOnlyList<EntryOutputTemplateDto>> List(CancellationToken ct) => (await db.EntryOutputTemplates.AsNoTracking().OrderBy(t => t.Name).ToArrayAsync(ct)).Select(Dto).ToArray();
    public async Task<(byte[] Content, string Mime, string Name)> Structure(Guid id, CancellationToken ct)
    {
        var item = await db.EntryOutputTemplates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw new AppException(404, "Modèle d’exportation introuvable.");
        var output = Output(item);
        return (EntryModelTextExport.Generate(output, [], true), $"{(output.FileExtension == "csv" ? "text/csv" : "text/plain")}; charset={output.Encoding}", $"modele_ecritures.{output.FileExtension}");
    }
    private static void Validate(EntryOutputTemplateSave input)
    {
        Validation.Text(input.Name, "Nom du modèle d’exportation", 100);
        Validation.Require(input.Description != null && input.Description.Length <= 1000, "Description limitée à 1 000 caractères.");
        Validation.Require(input.Output != null, "Configuration du fichier obligatoire.");
        EntryModelTextExport.Validate(input.Output);
    }
    public async Task<EntryOutputTemplateDto> Create(EntryOutputTemplateSave input, CancellationToken ct)
    {
        Validate(input);
        var item = new EntryOutputTemplate { Name = input.Name.Trim(), Description = input.Description.Trim(), Active = input.Active, OutputJson = JsonSerializer.Serialize(input.Output, Json) };
        db.EntryOutputTemplates.Add(item);
        await db.SaveChangesAsync(ct);
        return Dto(item);
    }
    public async Task<EntryOutputTemplateDto> Update(Guid id, EntryOutputTemplateSave input, CancellationToken ct)
    {
        Validate(input);
        var item = await db.EntryOutputTemplates.SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw new AppException(404, "Modèle d’exportation introuvable.");
        if (item.Version != input.Version) throw new AppException(409, "Le modèle a changé. Rechargez-le avant d’enregistrer.");
        item.Name = input.Name.Trim(); item.Description = input.Description.Trim(); item.Active = input.Active;
        item.OutputJson = JsonSerializer.Serialize(input.Output, Json); item.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new AppException(409, "Le modèle a changé. Rechargez-le avant d’enregistrer."); }
        return Dto(item);
    }
    public async Task Delete(Guid id, int version, CancellationToken ct)
    {
        var item = await db.EntryOutputTemplates.SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw new AppException(404, "Modèle d’exportation introuvable.");
        if (item.Version != version) throw new AppException(409, "Le modèle a changé. Rechargez la liste.");
        var reference = JsonSerializer.Serialize(new[] { new { outputTemplateId = id } }, Json);
        if (await db.ClientEntryModelSets.AnyAsync(d => EF.Functions.JsonContains(d.ModelsJson, reference), ct))
            throw new AppException(409, "Ce format est utilisé par une écriture. Choisissez un autre format dans cette écriture avant de le supprimer.");
        db.EntryOutputTemplates.Remove(item);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new AppException(409, "Le modèle a changé. Rechargez la liste."); }
    }
}
