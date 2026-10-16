using System.Linq.Expressions;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using Microsoft.EntityFrameworkCore;
namespace BankStatementConverter.Infrastructure;

public class ExportDefinition : CatalogDefinition<ExportTemplate, ExportInput, ExportDto>
{
    public override IQueryable<ExportTemplate> Query(AppDbContext db) => db.ExportTemplates.Include(x => x.Fields);
    public override Expression<Func<ExportTemplate, ExportDto>> Projection => x => new(x.Id, x.Name, x.Type, x.Description, x.FileExtension,
        x.Delimiter, x.Encoding, x.NumberCulture, x.Type != "SAGE100" && x.IncludeHeader, x.IsActive,
        x.Fields.OrderBy(f => f.Position).Select(f => new ExportFieldDto(f.FieldName, f.SourceField, f.Position, f.Format, f.Required)).ToList());
    public override Task ValidateAsync(ExportInput input, AppDbContext db, CancellationToken ct)
    {
        Validation.Export(input);
        return Task.CompletedTask;
    }
    public override void Apply(ExportInput input, ExportTemplate entity, AppDbContext db)
    {
        base.Apply(input, entity, db);
        if (entity.Type == "SAGE100")
            entity.IncludeHeader = false;
        var incoming = input.Fields.ToDictionary(x => x.Position);
        foreach (var old in entity.Fields.ToList())
            if (!incoming.ContainsKey(old.Position))
            {
                db.Remove(old);
                entity.Fields.Remove(old);
            }
        foreach (var f in input.Fields)
        {
            var field = entity.Fields.FirstOrDefault(x => x.Position == f.Position);
            if (field == null)
            {
                field = new()
                {
                    ExportTemplateId = entity.Id,
                    Position = f.Position
                };
                entity.Fields.Add(field);
                db.Add(field);
            }
            field.FieldName = f.FieldName;
            field.SourceField = f.SourceField;
            field.Format = f.Format;
            field.Required = f.Required;
        }
    }
}
