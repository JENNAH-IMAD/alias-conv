using BankStatementConverter.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace BankStatementConverter.Infrastructure;

public static class Seed
{
    public static ExportTemplate DefaultExport(string type)
    {
        (string Label, string Source)[] fields = type switch
        {
            "SAGEX3" => [("Date", "Date"), ("Journal", "Journal"), ("Compte", "Account"), ("Libellé", "Description"), ("Débit", "Debit"), ("Crédit", "Credit"), ("Référence", "Reference")],
            "SAGE100" => [("Date", "Date"), ("Compte", "Account"), ("Libellé", "Description"), ("Montant", "Amount"), ("Sens", "Direction"), ("Analytique", "Analytic")],
            _ => [("Date", "Date"), ("Libellé", "Description"), ("Débit", "Debit"), ("Crédit", "Credit"), ("Solde", "Balance")]
        };
        return new() { Name = type == "SAGE100" ? "Sage 100 Standard" : type == "SAGEX3" ? "Sage X3 – Import banque" : "CSV personnalisé", Type = type, FileExtension = type == "SAGE100" ? "txt" : "csv", Delimiter = type == "SAGE100" ? " " : ";", Encoding = type == "SAGE100" ? "windows-1252" : "utf-8", Fields = fields.Select((f, i) => new ExportTemplateField { FieldName = f.Label, SourceField = f.Source, Position = i, Required = f.Source is "Date" or "Description", Format = f.Source == "Date" ? "dd/MM/yyyy" : null }).ToList() };
    }
    public static async Task RunAsync(AppDbContext db, IConfiguration config)
    {
        // Preserve profile IDs referenced by conversion history while retiring the BMCE engines.
        foreach (var profile in await db.StatementTemplates.Where(x => x.ParserKey == "bmce-text" || x.ParserKey == "bmce-auto" || x.ParserKey == "bmce-scan").ToListAsync())
        {
            profile.ParserKey = "multibank-auto";
            profile.Description = "Extraction automatique multibanque - PDF texte uniquement, sans OCR.";
        }
        if (!await db.StatementTemplates.AnyAsync(x => x.BankId == null && x.ParserKey == "multibank-auto"))
            db.StatementTemplates.Add(new() { Name = "Extraction automatique multibanque", ParserKey = "multibank-auto", BankId = null, Description = "PDF texte : Attijariwafa, Banque Populaire, BMCE, Credit Agricole et Societe Generale. Controle des soldes et des reports." });
        foreach (var type in new[] { "SAGE100", "SAGEX3", "CUSTOMCSV" })
            if (!await db.ExportTemplates.AnyAsync(x => x.Type == type)) db.ExportTemplates.Add(DefaultExport(type));
        var email = config["Seed:AdminEmail"]?.Trim().ToLowerInvariant(); var password = config["Seed:AdminPassword"];
        if (!string.IsNullOrEmpty(email) && !await db.Users.AnyAsync(x => x.Email == email))
        {
            Validation.Email(email); Validation.Password(password);
            var admin = new User { Username = email, Email = email, Role = "Admin" };
            admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, password!); db.Users.Add(admin);
        }
        if (config.GetValue<bool>("Seed:DemoData") && !await db.Banks.AnyAsync(x => x.Code == "FICTIVE"))
        {
            var client = new Client { Name = "Client fictif", LegalName = "Démonstration uniquement", ICE = "000000000000001", IF = "DEMO", RC = "DEMO", Email = "demo@example.test", Phone = "+212600000000" };
            var bank = new Bank { Name = "Banque fictive", Code = "FICTIVE", Description = "Fixture technique, aucune règle de banque réelle." };
            db.Clients.Add(client); db.Banks.Add(bank);
            db.BankAccounts.Add(new() { Client = client, Bank = bank, AccountNumber = "DEMO001", AccountName = "Compte fictif" });
            db.StatementTemplates.Add(new() { Bank = bank, Name = "Fixture délimitée fictive", Description = "Date;Libellé;Débit;Crédit;Solde;Référence. Une ligne par opération, sans en-tête." });
        }
        await db.SaveChangesAsync();
    }
}
