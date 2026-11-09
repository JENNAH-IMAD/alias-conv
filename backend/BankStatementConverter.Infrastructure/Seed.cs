using BankStatementConverter.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace BankStatementConverter.Infrastructure;

public static class Seed
{
    public static async Task RunAsync(AppDbContext db, IConfiguration config)
    {
        var existingBanks = await db.Banks.ToListAsync();
        var missingBanks = MoroccanBankCatalog.Missing(existingBanks);
        db.Banks.AddRange(missingBanks);
        MoroccanBankCatalog.ApplyMissingLogos(existingBanks.Concat(missingBanks));
        var sage100Templates = await db.ExportTemplates.Include(t => t.Fields).Where(t => t.Type == "SAGE100ENTRIES").ToListAsync();
        foreach (var template in sage100Templates.Where(Exports.AccountingExportTemplates.IsLegacySage100Default))
        {
            var updated = Exports.AccountingExportTemplates.Create("SAGE100ENTRIES");
            db.RemoveRange(template.Fields.ToArray());
            template.Fields.Clear();
            foreach (var field in updated.Fields)
            {
                field.ExportTemplateId = template.Id;
                template.Fields.Add(field);
                db.Add(field);
            }
            template.Description = updated.Description;
        }
        foreach (var type in new[] { "SAGE100ENTRIES", "SAGEX3ENTRIES" })
            if (!await db.ExportTemplates.AnyAsync(template => template.Type == type))
                db.ExportTemplates.Add(Exports.AccountingExportTemplates.Create(type));
        if (config.GetValue<bool>("Seed:DemoData") && !await db.Banks.AnyAsync(x => x.Code == "FICTIVE"))
        {
            var client = new Client { Name = "Client fictif", LegalName = "Démonstration uniquement", ICE = "000000000000001", IF = "DEMO", RC = "DEMO", Email = "demo@example.test", Phone = "+212600000000" };
            var bank = new Bank { Name = "Banque fictive", Code = "FICTIVE", Description = "Fixture technique, aucune règle de banque réelle." };
            db.Clients.Add(client);
            db.Banks.Add(bank);
            db.BankAccounts.Add(new()
            {
                Client = client,
                Bank = bank,
                AccountNumber = "DEMO001",
                AccountName = "Compte fictif"
            });
        }
        await db.SaveChangesAsync();
    }
}
