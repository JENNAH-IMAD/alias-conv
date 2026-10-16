using BankStatementConverter.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace BankStatementConverter.Infrastructure;

public static class Seed
{
    public static async Task RunAsync(AppDbContext db, IConfiguration config)
    {
        var existingBanks = await db.Banks.ToListAsync();
        // Separate the historical Saham catalog code from the Société Générale entry.
        foreach (var bank in existingBanks.Where(bank => bank.Code == "SGMB" &&
            MoroccanBankCatalog.IdentityCodes(bank.Name).SequenceEqual(new[] { "SAHAM" })))
            if (!existingBanks.Any(other => other.Id != bank.Id && other.Code == "SAHAM"))
                bank.Code = "SAHAM";
        var missingBanks = MoroccanBankCatalog.Missing(existingBanks);
        db.Banks.AddRange(missingBanks);
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
