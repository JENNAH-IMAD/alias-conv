using System.Text;
using System.Text.Json;
using System.Security.Claims;
using BankStatementConverter.Application;
using BankStatementConverter.API;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using BankStatementConverter.Infrastructure.Accounting;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Npgsql;

static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
static Task<bool> Authorized(string action, params string[] permissions) => AuthorizedController("AccountingEntryModels", action, "POST", permissions);
static async Task<bool> AuthorizedController(string controller, string action, string method, params string[] permissions) {
    var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(permissions.Select(p => new Claim("permission", p)), "test")) };
    http.Request.Method = method;
    var descriptor = new ControllerActionDescriptor { ControllerName = controller, ActionName = action };
    var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), descriptor), new List<IFilterMetadata>());
    await new PermissionFilter().OnAuthorizationAsync(context);return context.Result == null;
}
Check(await Authorized("Preview", "documents.read"), "API_ReadOnlyPreviewAllowed");
Check(!await AuthorizedController("ClientWorkspace", "History", "GET", "documents.read") && await AuthorizedController("ClientWorkspace", "History", "GET", "history.read"), "History_ActivityRequiresDedicatedPermission");
Check(!await AuthorizedController("ExtractionProfiles", "History", "GET", "documents.read") && await AuthorizedController("ExtractionProfiles", "History", "GET", "history.read"), "History_ProfileActivityRequiresDedicatedPermission");
Check(!typeof(AccountingEntryModelsController).Assembly.GetTypes().Any(x => x.Name == "AccountingController"), "RemovalApi_NoJournalPlanController");
Check(await AuthorizedController("StatementTreatments", "Get", "GET", "documents.read") && !await AuthorizedController("StatementTreatments", "Get", "GET", "documents.write"), "Treatment_ReadRequiresDocumentPermission");
Check(!await Authorized("Save", "documents.read"), "API_SaveRequiresWritePermission");
Check(!await Authorized("Export", "documents.read") && await Authorized("Export", "documents.export"), "API_ExportRequiresExportPermission");
Check(!await Authorized("ModelFile", "documents.read") && await Authorized("ModelFile", "documents.export") && !await Authorized("ArchiveFile", "documents.read") && await Authorized("ArchiveFile", "documents.export"), "API_IndependentFilesRequireExportPermission");
Check(await AuthorizedController("EntryOutputTemplates", "List", "GET", "documents.read") && await AuthorizedController("EntryOutputTemplates", "List", "GET", "templates.read") && !await AuthorizedController("EntryOutputTemplates", "List", "GET", "documents.write"), "EntryCatalog_ReadPermissionsAllowDocumentSelector");
Check(!await AuthorizedController("EntryOutputTemplates", "Update", "PUT", "documents.write") && await AuthorizedController("EntryOutputTemplates", "Update", "PUT", "templates.write"), "EntryCatalog_EditRequiresTemplateWrite");
Check(!await AuthorizedController("EntryOutputTemplates", "Download", "GET", "templates.read") && await AuthorizedController("EntryOutputTemplates", "Download", "GET", "documents.export"), "EntryCatalog_DownloadRequiresExportPermission");
var config = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(args[0])).Build();
await LocalEntryStorageFixture.Verify();
var connection = config.GetConnectionString("Default")!;
await RemovalMigrationChecks.Run(connection);
var schema = "alias_entry_model_test_" + Guid.NewGuid().ToString("N");
await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
await new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin).ExecuteNonQueryAsync();
try
{
    var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false };
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scoped.ConnectionString).Options);
    await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    var client = new Client { Name = "Test client A", ICE = "111111111111111" }; var other = new Client { Name = "Test client B", ICE = "222222222222222" };
    var bank = new Bank { Name = "Test bank", Code = "TEST" };
    var account = new BankAccount { ClientId = client.Id, BankId = bank.Id, AccountNumber = "123456789012345678901234", AccountName = "Test bank account", Journal = "BQ1", AccountCode = "514100", Currency = "MAD" };
    var foreign = new BankAccount { ClientId = other.Id, BankId = bank.Id, AccountNumber = "223456789012345678901234", AccountName = "Other account", Journal = "BQ2", AccountCode = "514200" };
    var template = AccountingExportTemplates.Create("SAGEX3ENTRIES");
    var movement = new StatementTransaction { Sequence = 1, Date = new(2026, 11, 1), Description = "ACHAT FOURNISSEUR CAFE", Debit = 120, Reference = "T001", Currency = "MAD" };
    var extract = new StatementExtract(Guid.NewGuid(), "ACHAT", DateTime.UtcNow, 1, "MAD", [movement], 120, 0, -120);
    var source = new BankStatement { BankName = bank.Name, BankCode = bank.Code, AccountNumber = account.AccountNumber, Currency = "MAD", StatementNumber = "TEST-1",
        PeriodStart = new(2026, 11, 1), PeriodEnd = new(2026, 11, 30), OpeningBalance = 1000, ClosingBalance = 880, TotalDebit = 120, TotalCredit = 0,
        BalanceConvention = "CREDIT_INCREASES", Transactions = [movement], Extracts = [extract] };
    var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var record = new StatementImport { ClientId = client.Id, BankAccountId = account.Id, BankName = bank.Name, Currency = "MAD", StatementNumber = "TEST-1", Status = "VALIDATED", Version = 1, TransactionCount = 1, SourceFileName = "test.pdf", NormalizedJson = JsonSerializer.Serialize(source, json) };
    db.AddRange(client, other, bank, account, foreign, template, record); await db.SaveChangesAsync();
    var storage = new MemoryStorage();
    var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Test accountant")], "test")) } };
    var engine = new ExportEngine(new ExportSourceResolver(), new(), new(), new(), new(), new(), new());
    var statements = new StatementImportService(db, new StatementValidator(), engine, storage, http);
    var accounting = new EntryModelStore(db);
    // Retain generic export/archive coverage independently of the retired journal preparation module.
    var genericPreview = await statements.PreviewAsync(record.Id, template.Id, new(record.Version), default);
    var exported = await statements.ExportAsync(record.Id, template.Id, new(record.Version, genericPreview.TemplateFingerprint, AllowErrors: true), default);
    Check(exported.Content.Length > 0, "GenericExports_PreservedWithoutJournalPlans");
    await TreatmentChecks.Run(db, statements, record, template.Id, other.Id);
    await IndependentChecks.Run(db, accounting, statements, storage, http, client, account);
}
finally
{
    if (!schema.StartsWith("alias_entry_model_test_", StringComparison.Ordinal) || schema.Length != 55 || !schema[23..].All(char.IsAsciiHexDigit)) throw new Exception("Unsafe test schema cleanup");
    await new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin).ExecuteNonQueryAsync();
    Console.WriteLine("Disposable test schema removed; application data unchanged.");
}
sealed class MemoryStorage : IFileStorageService
{
    public Dictionary<string, byte[]> Files { get; } = [];
    public async Task<string> SaveAsync(Stream stream, string category, string extension, CancellationToken ct) { using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes, ct); var key = Guid.NewGuid().ToString("N"); Files[key] = bytes.ToArray(); return key; }
    public Stream Open(string key) => new MemoryStream(Files[key]);
    public void Delete(string key) => Files.Remove(key);
}

