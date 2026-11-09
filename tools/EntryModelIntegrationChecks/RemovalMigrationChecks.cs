using System.Text.Json;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

static class RemovalMigrationChecks
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    public static async Task Run(string connection)
    {
        var schema = "alias_removal_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin).ExecuteNonQueryAsync();
        try
        {
            var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false };
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scoped.ConnectionString,
                options => options.MigrationsHistoryTable("__EFMigrationsHistory", schema)).Options);
            await db.GetService<IMigrator>().MigrateAsync("20261104103324_EnsureStatementTreatmentCascade");
            var client = new Client { Name = "Preserved company", ICE = "987654321012345" };
            var bank = new Bank { Name = "Preserved bank", Code = "KEEP" };
            var account = new BankAccount { ClientId = client.Id, BankId = bank.Id, AccountName = "Preserved account", AccountNumber = "123456789", AccountCode = "512000", Currency = "MAD" };
            var models = new[] { new AccountingEntryModel(Guid.NewGuid(), "Preserved rule", "CHE", ["CHEQUE"], account.Id, account.AccountCode, Guid.Empty, 14, Output: new()) };
            var source = new StatementImport { ClientId = client.Id, BankAccountId = account.Id, BankName = bank.Name, NormalizedJson = "{}" };
            var archive = new AccountingEntryArchive { ClientId = client.Id, StatementId = source.Id, ModelId = models[0].Id,
                ModelName = models[0].Name, PreviewFingerprint = "immutable", SnapshotJson = "{\"protected\":true}", ContentSha256 = "kept", FileName = "kept.txt" };
            db.AddRange(client, bank, account, source, archive); await db.SaveChangesAsync();
            var json = JsonSerializer.Serialize(new { accounts = new[] { new { code = "DELETE-PLAN", name = "Plan" } }, journals = new[] { new { code = "DELETE-JOURNAL" } }, entryModels = models }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var now = DateTime.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AccountingDossiers" ("Id","ClientId","SettingsJson","Version","CreatedAt","UpdatedAt")
                VALUES ({Guid.NewGuid()},{client.Id},CAST({json} AS jsonb),9,{now},{now})
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AccountingPreparations" ("Id","StatementId","ClientId","Scope","SourceVersion","Version","DraftJson","Status","ValidationFingerprint","ValidatedBy","CreatedAt","UpdatedAt")
                VALUES ({Guid.NewGuid()},{source.Id},{client.Id},'FULL',1,1,'[]'::jsonb,'DRAFT','','',{now},{now})
                """);
            var formatBefore = await db.EntryOutputTemplates.AsNoTracking().Select(x => x.OutputJson).ToArrayAsync();
            var archiveBefore = await db.AccountingEntryArchives.AsNoTracking().Select(x => x.SnapshotJson).SingleAsync();
            await db.GetService<IMigrator>().MigrateAsync("20261104120217_RemoveJournalsAndPlansPreserveEntryModels");
            var saved = await db.ClientEntryModelSets.AsNoTracking().SingleAsync(x => x.ClientId == client.Id);
            var copied = JsonSerializer.Deserialize<AccountingEntryModel[]>(saved.ModelsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Check(saved.Version == 9 && JsonSerializer.Serialize(copied) == JsonSerializer.Serialize(models), "RemovalMigration_PreservesEveryEntryModelAndVersion");
            Check(!saved.ModelsJson.Contains("DELETE-PLAN") && !saved.ModelsJson.Contains("DELETE-JOURNAL"), "RemovalMigration_DoesNotKeepRetiredCatalogData");
            await using var check = new NpgsqlConnection(scoped.ConnectionString); await check.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT to_regclass('\"AccountingDossiers\"') IS NULL AND to_regclass('\"AccountingPreparations\"') IS NULL", check);
            Check((bool)(await command.ExecuteScalarAsync())!, "RemovalMigration_DropsJournalAndPlanTables");
            Check((await db.AccountingEntryArchives.AsNoTracking().SingleAsync()).SnapshotJson == archiveBefore
                && (await db.BankAccounts.AsNoTracking().SingleAsync()).AccountCode == account.AccountCode
                && (await db.EntryOutputTemplates.AsNoTracking().Select(x => x.OutputJson).ToArrayAsync()).SequenceEqual(formatBefore),
                "RemovalMigration_PreservesArchivesFormatsAndBankAccounts");
        }
        finally
        {
            if (!schema.StartsWith("alias_removal_test_") || !schema[19..].All(char.IsAsciiHexDigit)) throw new Exception("Unsafe migration test cleanup");
            await new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin).ExecuteNonQueryAsync();
        }
    }
}
