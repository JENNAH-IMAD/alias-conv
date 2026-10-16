using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
namespace BankStatementConverter.Tests;

public class StatementExportsApiTests
{
    [Fact, Trait("Category", "Integration")]
    public async Task ImportReviewPreviewDownloadSnapshotsStaleVersionsAndDecimalsWorkEndToEnd()
    {
        // Create and drop only a newly generated database. Never test writes against the workspace database.
        var configured = Environment.GetEnvironmentVariable("TEST_DATABASE_URL");
        if (string.IsNullOrWhiteSpace(configured))
        {
            var root = StatementPdfExtractorTests.RepositoryRoot();
            using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "backend/BankStatementConverter.API/appsettings.Development.json")));
            configured = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("Default").GetString();
        }
        var adminCs = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(adminCs.ConnectionString);
        await admin.OpenAsync();
        var name = "alias_export_test_" + Guid.NewGuid().ToString("N");
        var storageRoot = Path.Combine(Path.GetTempPath(), name);
        await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin))
            await command.ExecuteNonQueryAsync();
        try
        {
            var testCs = new NpgsqlConnectionStringBuilder(configured) { Database = name };
            Assert.StartsWith("alias_export_test_", testCs.Database);
            var ocr = new AutomaticPdfExtractionTests.OcrStub();
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing").ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = testCs.ConnectionString,
                ["Database:AutoMigrate"] = "true",
                ["Seed:DemoData"] = "false",
                ["ArchiveCleanup:Enabled"] = "false",
                ["Storage:Root"] = storageRoot,
                ["Ocr:Provider"] = "LOCAL"
            })).ConfigureServices(services => services.AddSingleton<IPdfOcrClient>(ocr)));
            using var http = factory.CreateClient();
            http.DefaultRequestHeaders.Add("X-Alias-Request", "1");
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/clients")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/client-workspace/statements")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync($"/api/statement-imports/processing/{Guid.NewGuid()}")).StatusCode);
            var initialSession = await http.GetFromJsonAsync<JsonElement>("/api/auth/session");
            Assert.True(initialSession.GetProperty("setupRequired").GetBoolean());
            using var setup = await http.PostAsJsonAsync("/api/auth/setup", new SetupInput("Administrateur test", "admin@example.test", "Fictional-password-2026"));
            setup.EnsureSuccessStatusCode();
            var adminUser = (await setup.Content.ReadFromJsonAsync<UserDto>())!;
            Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync("/api/auth/setup", new SetupInput("Autre admin", "other@example.test", "Fictional-password-2026"))).StatusCode);
            async Task<ExportDto> Template(ExportTemplate template)
            {
                var input = new ExportInput(template.Name, template.Type, template.Description, template.FileExtension, template.Delimiter, template.Encoding, template.NumberCulture, template.IncludeHeader, template.IsActive,
                    template.Fields.Select(f => new ExportFieldDto(f.FieldName, f.SourceField, f.Position, f.Format, f.Required)).ToList());
                using var response = await http.PostAsJsonAsync("/api/export-templates", input);
                response.EnsureSuccessStatusCode();
                return (await response.Content.ReadFromJsonAsync<ExportDto>())!;
            }
            var models = new[] { await Template(ExportFixtures.Csv()), await Template(ExportFixtures.Sage(false)), await Template(ExportFixtures.Sage(true)), await Template(ExportFixtures.X3(false)), await Template(ExportFixtures.X3(true)) };
            var before = await http.GetStringAsync("/api/export-templates?pageSize=100");
            using var created = await http.PostAsJsonAsync("/api/statement-imports", ExportFixtures.Statement);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var detail = (await created.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal("NEEDS_REVIEW", detail.Status);
            Assert.Equal(1, detail.Version);
            Assert.False(detail.Statement.IsValidated);
            using var preparedResponse = await http.PostAsJsonAsync($"/api/statement-imports/{detail.Id}/prepare", new PrepareStatementExportRequest(detail.Version, models[0].Id));
            preparedResponse.EnsureSuccessStatusCode();
            var prepared = (await preparedResponse.Content.ReadFromJsonAsync<PreparedStatementExport>(StatementImportService.Json))!;
            Assert.Equal(detail.Version + 1, prepared.Detail.Version);
            Assert.Equal("NEEDS_REVIEW", prepared.Detail.Status);
            Assert.Equal(new decimal?[] { 13500m, 13374.50m }, prepared.Detail.Statement.Transactions.Select(t => t.Balance));
            detail = prepared.Detail;
            Assert.Equal(models[0].Id, prepared.Preview.Template.Id);
            Assert.Equal(detail.Version, prepared.Preview.StatementVersion);
            Assert.False(prepared.Preview.IsValidated);
            Assert.Empty(prepared.Preview.Errors);
            using var stalePreparation = await http.PostAsJsonAsync($"/api/statement-imports/{detail.Id}/prepare", new PrepareStatementExportRequest(0, models[0].Id));
            Assert.Equal(HttpStatusCode.Conflict, stalePreparation.StatusCode);
            var missingDirection = ExportFixtures.Statement with
            {
                Transactions = ExportFixtures.Statement.Transactions.Select(t => t with { Direction = null }).ToList()
            };
            using var missingImport = await http.PostAsJsonAsync("/api/statement-imports", missingDirection);
            missingImport.EnsureSuccessStatusCode();
            var missingDetail = (await missingImport.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            using var completedResponse = await http.PostAsJsonAsync($"/api/statement-imports/{missingDetail.Id}/prepare", new PrepareStatementExportRequest(missingDetail.Version, models[2].Id));
            completedResponse.EnsureSuccessStatusCode();
            var completed = (await completedResponse.Content.ReadFromJsonAsync<PreparedStatementExport>(StatementImportService.Json))!;
            Assert.Equal(missingDetail.Version + 1, completed.Detail.Version);
            Assert.Equal("NEEDS_REVIEW", completed.Detail.Status);
            Assert.Equal(new[] { "CREDIT", "DEBIT" }, completed.Detail.Statement.Transactions.Select(t => t.Direction));
            Assert.Contains(completed.Detail.Warnings, w => w.Code == "EXPORT_FIELD_COMPLETED" && w.SourceField == "Direction");
            Assert.Equal(completed.Detail.Version, completed.Preview.StatementVersion);
            Assert.Empty(completed.Preview.Errors);
            using var idempotent = await http.PostAsJsonAsync($"/api/statement-imports/{missingDetail.Id}/prepare", new PrepareStatementExportRequest(completed.Detail.Version, models[2].Id));
            idempotent.EnsureSuccessStatusCode();
            Assert.Equal(completed.Detail.Version, (await idempotent.Content.ReadFromJsonAsync<PreparedStatementExport>(StatementImportService.Json))!.Detail.Version);
            var first = models[0];
            var request = new ExportRequest(detail.Version);
            using var provisional = await http.PostAsJsonAsync($"/api/statement-imports/{detail.Id}/exports/{first.Id}/preview", request);
            provisional.EnsureSuccessStatusCode();
            var preview = (await provisional.Content.ReadFromJsonAsync<ExportPreview>())!;
            Assert.False(preview.IsValidated);
            Assert.Equal(2, preview.Rows.Count);
            using var denied = await http.PostAsJsonAsync($"/api/statement-imports/{detail.Id}/exports/{first.Id}", request);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, denied.StatusCode);
            using var validated = await http.PostAsJsonAsync($"/api/statement-imports/{detail.Id}/validate", new StatementVersionRequest(detail.Version));
            validated.EnsureSuccessStatusCode();
            detail = (await validated.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal("VALIDATED", detail.Status);
            using var stale = await http.PostAsJsonAsync($"/api/statement-imports/{detail.Id}/exports/{first.Id}/preview", request);
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            var fixtures = new[] { "custom_csv_prepared_fr.csv", "sage100_windows1252.txt", "sage100_standard.txt", "sagex3_utf8.txt", "sagex3_ascii.txt" };
            for (var i = 0; i < models.Length; i++)
            {
                var model = models[i];
                using var displayed = await http.PostAsJsonAsync($"/api/statement-imports/{detail.Id}/exports/{model.Id}/preview", new ExportRequest(detail.Version));
                displayed.EnsureSuccessStatusCode();
                preview = (await displayed.Content.ReadFromJsonAsync<ExportPreview>())!;
                Assert.Empty(preview.Errors);
                Assert.True(preview.IsValidated);
                using var download = await http.PostAsJsonAsync($"/api/statement-imports/{detail.Id}/exports/{model.Id}", new ExportRequest(detail.Version, preview.TemplateFingerprint));
                download.EnsureSuccessStatusCode();
                var bytes = await download.Content.ReadAsByteArrayAsync();
                Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "golden", fixtures[i])), bytes);
                Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
                Assert.DoesNotContain(ExportFixtures.Statement.AccountNumber!, download.Content.Headers.ContentDisposition!.ToString());
                Assert.Equal(model.FileExtension == "csv" ? "text/csv" : "text/plain", download.Content.Headers.ContentType?.MediaType);
            }
            Assert.Equal(before, await http.GetStringAsync("/api/export-templates?pageSize=100"));
            // Row editing persists additions/removals/bulk values, then regenerates actual CSV and Sage files.
            // Original source/archives are untouched, and the row sequence may contain gaps after deletion.
            var rowEditId = Guid.NewGuid();
            using var rowImport = await http.PostAsJsonAsync("/api/statement-imports", ExportFixtures.Statement with
            {
                Id = rowEditId
            });
            rowImport.EnsureSuccessStatusCode();
            var rowDetail = (await rowImport.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            var correctedRows = new[] {
                rowDetail.Statement.Transactions[0] with {Description="LIBELLE CORRIGE",Currency="MAD"},
                rowDetail.Statement.Transactions[1] with {Id=Guid.NewGuid(),Sequence=3,Date=new(2026,9,3),ValueDate=new(2026,9,3),Description="LIGNE AJOUTEE",Debit=null,Credit=42.12345678m,Amount=42.12345678m,Direction="CREDIT",Currency="MAD"}
            };
            var correctedStatement = rowDetail.Statement with
            {
                Transactions = correctedRows,
                TotalDebit = 0m,
                TotalCredit = 12542.12345678m,
                ClosingBalance = 13542.12345678m
            };
            using var rowSave = await http.PutAsJsonAsync($"/api/statement-imports/{rowDetail.Id}?version={rowDetail.Version}", correctedStatement);
            rowSave.EnsureSuccessStatusCode();
            var savedRows = (await rowSave.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal("NEEDS_REVIEW", savedRows.Status);
            Assert.Equal(42.12345678m, savedRows.Statement.Transactions[1].Credit);
            Assert.Equal(correctedRows.Select(r => r.Id), savedRows.Statement.Transactions.Select(r => r.Id));
            Assert.Equal(HttpStatusCode.Conflict, (await http.PutAsJsonAsync($"/api/statement-imports/{rowDetail.Id}?version={rowDetail.Version}", correctedStatement)).StatusCode);
            using var rowValidation = await http.PostAsJsonAsync($"/api/statement-imports/{rowDetail.Id}/validate", new StatementVersionRequest(savedRows.Version));
            rowValidation.EnsureSuccessStatusCode();
            var validatedRows = (await rowValidation.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            foreach (var editedModel in new[] { models[0], models[1], models[3] })
            {
                using var rowPreview = await http.PostAsJsonAsync($"/api/statement-imports/{rowDetail.Id}/exports/{editedModel.Id}/preview", new ExportRequest(validatedRows.Version));
                rowPreview.EnsureSuccessStatusCode();
                var rowFile = (await rowPreview.Content.ReadFromJsonAsync<ExportPreview>())!;
                Assert.Empty(rowFile.Errors);
                Assert.Equal(editedModel.Type == "SAGEX3" ? 4 : 2, rowFile.TotalRows);
                using var rowDownload = await http.PostAsJsonAsync($"/api/statement-imports/{rowDetail.Id}/exports/{editedModel.Id}", new ExportRequest(validatedRows.Version, rowFile.TemplateFingerprint));
                rowDownload.EnsureSuccessStatusCode();
                var rowContent = Encoding.UTF8.GetString(await rowDownload.Content.ReadAsByteArrayAsync());
                Assert.Contains("LIBELLE CORRIGE", rowContent);
                Assert.Contains("LIGNE AJOUTEE", rowContent);
                Assert.DoesNotContain("FRAIS BANCAIRES", rowContent);
                if (editedModel.Type == "SAGEX3")
                {
                    var fileLines = rowContent.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                    Assert.Equal(new[] { 9, 10, 10, 9 }, fileLines.Select(line => line.Split(';').Length));
                    Assert.EndsWith(";2;3", fileLines[2]);
                    Assert.EndsWith(";3;4", fileLines[3]);
                }
            }
            var history = (await http.GetFromJsonAsync<PageResult<ExportJobDto>>($"/api/statement-imports/{detail.Id}/exports"))!;
            Assert.Equal(5, history.Total);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var jobs = await db.ExportJobs.AsNoTracking().ToListAsync();
                Assert.All(jobs, j => { Assert.Contains("\"sourceField\"", j.TemplateSnapshotJson); Assert.Equal(models.Single(m => m.Id == j.TemplateId).Type == "SAGEX3" ? 4 : 2, j.RowCount); Assert.Equal(64, j.ContentSha256.Length); });
                Assert.Equal(35, await db.Set<ExportTemplateField>().CountAsync());
            }
            // A changed template cannot silently replace the one displayed in the preview.
            // Text edits archive exact bytes; forced output is auditable without changing statement validation.
            const string manuallyEdited = "Date;Libellé;Débit;Crédit;Solde\r\n01/09/2026;\"MANUEL;\nSUITE\";;12500.00;\r\n";
            using var manualPreviewResponse = await http.PostAsJsonAsync($"/api/statement-imports/{detail.Id}/exports/{first.Id}/preview", new ExportRequest(detail.Version));
            manualPreviewResponse.EnsureSuccessStatusCode();
            var manualPreview = (await manualPreviewResponse.Content.ReadFromJsonAsync<ExportPreview>())!;
            Assert.NotNull(manualPreview.Text);
            Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync($"/api/statement-imports/{detail.Id}/exports/{first.Id}", new ExportRequest(detail.Version, EditedText: manuallyEdited))).StatusCode);
            using var manualDownload = await http.PostAsJsonAsync($"/api/statement-imports/{detail.Id}/exports/{first.Id}", new ExportRequest(detail.Version, manualPreview.TemplateFingerprint, EditedText: manuallyEdited));
            manualDownload.EnsureSuccessStatusCode();
            Assert.Equal(manuallyEdited, Encoding.UTF8.GetString(await manualDownload.Content.ReadAsByteArrayAsync()));
            var editedHistory = (await http.GetFromJsonAsync<PageResult<ExportJobDto>>($"/api/statement-imports/{detail.Id}/exports"))!;
            var manualJob = Assert.Single(editedHistory.Items, j => j.Status == "EDITED");
            Assert.Equal(1, manualJob.RowCount);
            using var manualArchive = await http.GetAsync($"/api/statement-imports/{detail.Id}/exports/archives/{manualJob.Id}/file");
            Assert.Equal(manuallyEdited, Encoding.UTF8.GetString(await manualArchive.Content.ReadAsByteArrayAsync()));
            var invalidForForce = ExportFixtures.Statement with
            {
                Id = Guid.NewGuid(),
                IsValidated = false,
                Transactions = [ExportFixtures.Statement.Transactions[0] with { Date = null, Description = "عملية تجريبية" }]
            };
            using var forceImport = await http.PostAsJsonAsync("/api/statement-imports", invalidForForce);
            forceImport.EnsureSuccessStatusCode();
            var forceDetail = (await forceImport.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            using var forcePreviewResponse = await http.PostAsJsonAsync($"/api/statement-imports/{forceDetail.Id}/exports/{models[4].Id}/preview", new ExportRequest(forceDetail.Version));
            forcePreviewResponse.EnsureSuccessStatusCode();
            var forcePreview = (await forcePreviewResponse.Content.ReadFromJsonAsync<ExportPreview>())!;
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await http.PostAsJsonAsync($"/api/statement-imports/{forceDetail.Id}/exports/{models[4].Id}", new ExportRequest(forceDetail.Version, forcePreview.TemplateFingerprint))).StatusCode);
            using var forceDownload = await http.PostAsJsonAsync($"/api/statement-imports/{forceDetail.Id}/exports/{models[4].Id}", new ExportRequest(forceDetail.Version, forcePreview.TemplateFingerprint, AllowErrors: true));
            forceDownload.EnsureSuccessStatusCode();
            var forceBytes = await forceDownload.Content.ReadAsByteArrayAsync();
            Assert.Equal(forcePreview.Text, Encoding.UTF8.GetString(forceBytes));
            Assert.Equal("utf-8", forceDownload.Content.Headers.ContentType!.CharSet);
            var forceHistory = (await http.GetFromJsonAsync<PageResult<ExportJobDto>>($"/api/statement-imports/{forceDetail.Id}/exports"))!;
            var forceJob = Assert.Single(forceHistory.Items);
            Assert.Equal("FORCED", forceJob.Status);
            Assert.Equal("utf-8", forceJob.Encoding);
            using var forceArchive = await http.GetAsync($"/api/statement-imports/{forceDetail.Id}/exports/archives/{forceJob.Id}/file");
            Assert.Equal(forceBytes, await forceArchive.Content.ReadAsByteArrayAsync());
            var forceArchivePreview = await http.GetFromJsonAsync<JsonElement>($"/api/statement-imports/{forceDetail.Id}/exports/archives/{forceJob.Id}/preview");
            Assert.Equal("FORCED", forceArchivePreview.GetProperty("status").GetString());
            Assert.Equal(forcePreview.Text, forceArchivePreview.GetProperty("text").GetString());
            using var forcedAudit = JsonDocument.Parse(forceArchivePreview.GetProperty("templateSnapshotJson").GetString()!);
            Assert.True(forcedAudit.RootElement.GetProperty("downloadWithErrors").GetBoolean());
            Assert.NotEmpty(forcedAudit.RootElement.GetProperty("diagnostics").GetProperty("errors").EnumerateArray());
            var forceAfter = (await http.GetFromJsonAsync<StatementDetail>($"/api/statement-imports/{forceDetail.Id}", StatementImportService.Json))!;
            Assert.Equal("NEEDS_REVIEW", forceAfter.Status);
            Assert.Equal(forceDetail.Version, forceAfter.Version);
            Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync($"/api/statement-imports/{forceDetail.Id}/exports/{models[4].Id}", new ExportRequest(forceDetail.Version + 1, forcePreview.TemplateFingerprint, AllowErrors: true))).StatusCode);
            var rename = new ExportInput(first.Name + " modifié", first.Type, first.Description, first.FileExtension, first.Delimiter, first.Encoding, first.NumberCulture, first.IncludeHeader, first.IsActive, first.Fields);
            using var oldPreviewResponse = await http.PostAsJsonAsync($"/api/statement-imports/{detail.Id}/exports/{first.Id}/preview", new ExportRequest(detail.Version));
            var oldPreview = (await oldPreviewResponse.Content.ReadFromJsonAsync<ExportPreview>())!;
            (await http.PutAsJsonAsync($"/api/export-templates/{first.Id}", rename)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync($"/api/statement-imports/{detail.Id}/exports/{first.Id}", new ExportRequest(detail.Version, oldPreview.TemplateFingerprint))).StatusCode);
            // Editing a validated statement returns it to review and preserves exact decimal strings through the API.
            var revised = detail.Statement with
            {
                OpeningBalance = 12345678901234567890.12345678m,
                ClosingBalance = null
            };
            using var edit = await http.PutAsJsonAsync($"/api/statement-imports/{detail.Id}?version={detail.Version}", revised);
            edit.EnsureSuccessStatusCode();
            var raw = await edit.Content.ReadAsStringAsync();
            Assert.Contains("12345678901234567890.12345678", raw);
            var edited = (await edit.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal("NEEDS_REVIEW", edited.Status);
            Assert.Null(edited.ValidatedAt);
            Assert.Equal(revised.OpeningBalance, edited.Statement.OpeningBalance);
            using var exactPreview = await http.PostAsJsonAsync($"/api/statement-imports/{detail.Id}/exports/{first.Id}/preview", new ExportRequest(edited.Version));
            exactPreview.EnsureSuccessStatusCode();
            using var exactJson = JsonDocument.Parse(await exactPreview.Content.ReadAsStringAsync());
            Assert.Equal("12345678901234567890.12345678", exactJson.RootElement.GetProperty("openingBalance").GetString());
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await http.PostAsJsonAsync($"/api/statement-imports/{detail.Id}/exports/{first.Id}", new ExportRequest(edited.Version))).StatusCode);
            using var invalidImport = await http.PostAsJsonAsync("/api/statement-imports", ExportFixtures.Statement with
            {
                Transactions = [ExportFixtures.Statement.Transactions[0] with { Debit = 1m, Credit = 1m }]
            });
            invalidImport.EnsureSuccessStatusCode();
            var invalid = (await invalidImport.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await http.PostAsJsonAsync($"/api/statement-imports/{invalid.Id}/validate", new StatementVersionRequest(invalid.Version))).StatusCode);
            // PDF -> native text extraction -> review -> preview -> download, with exact original retained.
            var banks = (await http.GetFromJsonAsync<PageResult<BankDto>>("/api/banks?pageSize=100"))!;
            var bank = banks.Items.First();
            var pdfBytes = await File.ReadAllBytesAsync(StatementPdfExtractorTests.Fixture("releve_test_01_attijariwafa.pdf"));
            async Task<HttpResponseMessage> ImportPdf(byte[] bytes, string fileName, BankDto? selectedBank = null, Guid? processingId = null)
            {
                using var form = new MultipartFormDataContent();
                if (processingId.HasValue)
                    form.Add(new StringContent(processingId.Value.ToString()), "processingId");
                form.Add(new StringContent((selectedBank ?? bank).Id.ToString()), "bankId");
                form.Add(new StringContent("MAD"), "currency");
                var file = new ByteArrayContent(bytes);
                file.Headers.ContentType = new("application/pdf");
                form.Add(file, "file", fileName);
                return await http.PostAsync("/api/statement-imports/pdf", form);
            }
            var trackedId = Guid.NewGuid();
            using var importedPdf = await ImportPdf(pdfBytes, "releve.pdf", processingId: trackedId);
            Assert.Equal(HttpStatusCode.Created, importedPdf.StatusCode);
            var completedProgress = (await http.GetFromJsonAsync<BankStatementConverter.API.ProcessingStatus>($"/api/statement-imports/processing/{trackedId}"))!;
            Assert.Equal("done", completedProgress.Stage);
            Assert.Equal(100, completedProgress.Percent);
            Assert.Equal(HttpStatusCode.Conflict, (await ImportPdf(pdfBytes, "duplicate.pdf", processingId: trackedId)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await ImportPdf(pdfBytes, "empty-id.pdf", processingId: Guid.Empty)).StatusCode);
            var pdfDetail = (await importedPdf.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal("releve.pdf", pdfDetail.SourceFileName);
            Assert.Equal("NEEDS_REVIEW", pdfDetail.Status);
            Assert.Equal(16, pdfDetail.Statement.Transactions.Count);
            Assert.Contains(pdfDetail.Warnings, w => w.Code == "PDF_REVIEW_REQUIRED");
            Assert.Equal(89540.25m, pdfDetail.Statement.OpeningBalance);
            Assert.Equal(116677.35m, pdfDetail.Statement.ClosingBalance);
            // Completing old missing fields reuses retained extraction and respects manual entries.
            var rawBeforeMetadata = await http.GetStringAsync($"/api/statement-imports/{pdfDetail.Id}/extraction");
            using var clearMetadata = await http.PutAsJsonAsync($"/api/statement-imports/{pdfDetail.Id}?version={pdfDetail.Version}",
                pdfDetail.Statement with
                {
                    StatementNumber = "MANUEL-2026",
                    TotalDebit = null,
                    TotalCredit = null,
                    BalanceConvention = null
                });
            clearMetadata.EnsureSuccessStatusCode();
            pdfDetail = (await clearMetadata.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            var metadataVersion = pdfDetail.Version;
            using var completeMetadata = await http.PostAsJsonAsync($"/api/statement-imports/{pdfDetail.Id}/complete-metadata", new StatementVersionRequest(metadataVersion));
            completeMetadata.EnsureSuccessStatusCode();
            pdfDetail = (await completeMetadata.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal(metadataVersion + 1, pdfDetail.Version);
            Assert.Equal("NEEDS_REVIEW", pdfDetail.Status);
            Assert.Equal("MANUEL-2026", pdfDetail.Statement.StatementNumber);
            Assert.Equal(pdfDetail.Statement.Transactions.Sum(t => t.Debit ?? 0m), pdfDetail.Statement.TotalDebit);
            Assert.Equal(pdfDetail.Statement.Transactions.Sum(t => t.Credit ?? 0m), pdfDetail.Statement.TotalCredit);
            Assert.Equal("CREDIT_INCREASES", pdfDetail.Statement.BalanceConvention);
            Assert.Equal(rawBeforeMetadata, await http.GetStringAsync($"/api/statement-imports/{pdfDetail.Id}/extraction"));
            Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync($"/api/statement-imports/{pdfDetail.Id}/complete-metadata", new StatementVersionRequest(metadataVersion))).StatusCode);
            using var original = await http.GetAsync($"/api/statement-imports/{pdfDetail.Id}/original");
            original.EnsureSuccessStatusCode();
            Assert.Equal(pdfBytes, await original.Content.ReadAsByteArrayAsync());
            Assert.Equal("application/pdf", original.Content.Headers.ContentType?.MediaType);
            using var pdfValidated = await http.PostAsJsonAsync($"/api/statement-imports/{pdfDetail.Id}/validate", new StatementVersionRequest(pdfDetail.Version));
            pdfValidated.EnsureSuccessStatusCode();
            pdfDetail = (await pdfValidated.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            using var pdfPreview = await http.PostAsJsonAsync($"/api/statement-imports/{pdfDetail.Id}/exports/{first.Id}/preview", new ExportRequest(pdfDetail.Version));
            pdfPreview.EnsureSuccessStatusCode();
            var displayedPdf = (await pdfPreview.Content.ReadFromJsonAsync<ExportPreview>())!;
            Assert.Empty(displayedPdf.Errors);
            Assert.Equal(16, displayedPdf.Rows.Count);
            using var pdfExport = await http.PostAsJsonAsync($"/api/statement-imports/{pdfDetail.Id}/exports/{first.Id}", new ExportRequest(pdfDetail.Version, displayedPdf.TemplateFingerprint));
            pdfExport.EnsureSuccessStatusCode();
            Assert.Contains("1280,50", Encoding.UTF8.GetString(await pdfExport.Content.ReadAsByteArrayAsync()));
            // User's March example: both preview and HTTP attachment contain all 24 Sage X3 rows.
            var agricole = banks.Items.Single(b => b.Code == "CA");
            var caBytes = await File.ReadAllBytesAsync(StatementPdfExtractorTests.Fixture("releve_credit_agricole_exemple_02.pdf"));
            using var importedCa = await ImportPdf(caBytes, "credit_agricole_mars.pdf", agricole);
            importedCa.EnsureSuccessStatusCode();
            var caDetail = (await importedCa.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal("NATIVE_PDF", caDetail.Extraction!.Method);
            var caRaw = (await http.GetFromJsonAsync<PdfTextDocument>($"/api/statement-imports/{caDetail.Id}/extraction"))!;
            Assert.Single(caRaw.Pages);
            Assert.NotEmpty(caRaw.Pages[0].Boxes);
            Assert.Equal(new DateOnly(2026, 3, 1), caDetail.Statement.OpeningBalanceDate);
            Assert.Equal(new DateOnly(2026, 3, 31), caDetail.Statement.ClosingBalanceDate);
            using var caValidated = await http.PostAsJsonAsync($"/api/statement-imports/{caDetail.Id}/validate", new StatementVersionRequest(caDetail.Version));
            caValidated.EnsureSuccessStatusCode();
            caDetail = (await caValidated.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            var x3 = models[3];
            using var caPreviewResponse = await http.PostAsJsonAsync($"/api/statement-imports/{caDetail.Id}/exports/{x3.Id}/preview", new ExportRequest(caDetail.Version));
            caPreviewResponse.EnsureSuccessStatusCode();
            var caPreview = (await caPreviewResponse.Content.ReadFromJsonAsync<ExportPreview>())!;
            Assert.Empty(caPreview.Errors);
            Assert.Equal(24, caPreview.TotalRows);
            Assert.Equal(10, caPreview.Columns.Count);
            Assert.Equal("OPENING_BALANCE", caPreview.RowKinds![0]);
            Assert.Equal("CLOSING_BALANCE", caPreview.RowKinds[^1]);
            using var caExport = await http.PostAsJsonAsync($"/api/statement-imports/{caDetail.Id}/exports/{x3.Id}", new ExportRequest(caDetail.Version, caPreview.TemplateFingerprint));
            caExport.EnsureSuccessStatusCode();
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "golden", "sagex3_credit_agricole_032026.txt")), await caExport.Content.ReadAsByteArrayAsync());
            var caHistory = (await http.GetFromJsonAsync<PageResult<ExportJobDto>>($"/api/statement-imports/{caDetail.Id}/exports"))!;
            Assert.Equal(24, Assert.Single(caHistory.Items).RowCount);
            using var caOriginal = await http.GetAsync($"/api/statement-imports/{caDetail.Id}/original");
            Assert.Equal(caBytes, await caOriginal.Content.ReadAsByteArrayAsync());
            using var badSignature = await ImportPdf("not pdf"u8.ToArray(), "faux.pdf");
            Assert.Equal(HttpStatusCode.BadRequest, badSignature.StatusCode);
            using var corrupt = await ImportPdf("%PDF-invalid"u8.ToArray(), "cassé.pdf");
            Assert.Equal(HttpStatusCode.UnprocessableEntity, corrupt.StatusCode);
            var blankBuilder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
            blankBuilder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            using var blank = await ImportPdf(blankBuilder.Build(), "scan.pdf");
            Assert.Equal(HttpStatusCode.UnprocessableEntity, blank.StatusCode);
            Assert.Contains("OCR", await blank.Content.ReadAsStringAsync());
            Assert.Equal(2, Directory.EnumerateFiles(storageRoot, "*.pdf", SearchOption.AllDirectories).Count());
            // Unusual scans preserve stage 1 even when automatic adaptation cannot identify a table.
            ocr.Result = caRaw with
            {
                Method = "PADDLE_OCR",
                Pages = caRaw.Pages.Select(p => p with { Boxes = p.Boxes.Where(b => b.Top < 238 || b.Top > 250).ToList() }).ToList()
            };
            using var needsMapping = await ImportPdf(blankBuilder.Build(), "a-adapter.pdf", agricole);
            needsMapping.EnsureSuccessStatusCode();
            var unmapped = (await needsMapping.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.True(unmapped.Extraction!.NeedsMapping);
            Assert.Empty(unmapped.Statement.Transactions);
            Assert.Contains(unmapped.Warnings, w => w.Code == "EXTRACTION_MAPPING_REQUIRED");
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await http.PostAsJsonAsync($"/api/statement-imports/{unmapped.Id}/validate", new StatementVersionRequest(unmapped.Version))).StatusCode);
            using var adapted = await http.PostAsJsonAsync($"/api/statement-imports/{unmapped.Id}/adapt", new AdaptExtractionRequest(unmapped.Version, new(Date: 8, ValueDate: 58, Description: 15.5, Debit: 72.5, Credit: 85.9, HeaderTop: 30)));
            adapted.EnsureSuccessStatusCode();
            var mapped = (await adapted.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.False(mapped.Extraction!.NeedsMapping);
            Assert.Equal(22, mapped.Statement.Transactions.Count);
            Assert.Equal(unmapped.Version + 1, mapped.Version);
            Assert.Equal("NEEDS_REVIEW", mapped.Status);
            Assert.DoesNotContain(mapped.Warnings, w => w.Code == "EXTRACTION_MAPPING_REQUIRED");
            Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync($"/api/statement-imports/{unmapped.Id}/adapt", new AdaptExtractionRequest(unmapped.Version, new()))).StatusCode);
            var preservedRaw = (await http.GetFromJsonAsync<PdfTextDocument>($"/api/statement-imports/{unmapped.Id}/extraction"))!;
            Assert.Equal(JsonSerializer.Serialize(ocr.Result.Pages, StatementImportService.Json), JsonSerializer.Serialize(preservedRaw.Pages, StatementImportService.Json));
            Assert.Equal(new PdfColumnMapping(Date: 8, ValueDate: 58, Description: 15.5, Debit: 72.5, Credit: 85.9, HeaderTop: 30), preservedRaw.LastMapping);
            // Hosted VL response: preserve both pages/JSON, adapt real cells, then download all X3 rows.
            var beforeVlModels = await http.GetStringAsync("/api/export-templates?pageSize=100");
            ocr.Result = PaddleAiStudioTests.Document();
            var twoPages = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
            twoPages.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            twoPages.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            var scanned = twoPages.Build();
            var bmce = banks.Items.Single(b => b.Code == "BMCE");
            using var vlImported = await ImportPdf(scanned, "bmce-scan.pdf", bmce);
            vlImported.EnsureSuccessStatusCode();
            var vlDetail = (await vlImported.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal("PADDLE_OCR_VL_1_6", vlDetail.Extraction!.Method);
            Assert.Equal(36, vlDetail.Statement.Transactions.Count);
            Assert.NotNull(vlDetail.Statement.OpeningBalance);
            Assert.NotNull(vlDetail.Statement.ClosingBalance);
            var vlRaw = (await http.GetFromJsonAsync<PdfTextDocument>($"/api/statement-imports/{vlDetail.Id}/extraction"))!;
            Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(ocr.Result, StatementImportService.Json)), System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(vlRaw, StatementImportService.Json))));
            using var vlAdapt = await http.PostAsJsonAsync($"/api/statement-imports/{vlDetail.Id}/adapt", new AdaptExtractionRequest(vlDetail.Version, new(TableColumns: new())));
            vlAdapt.EnsureSuccessStatusCode();
            vlDetail = (await vlAdapt.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal(36, vlDetail.Statement.Transactions.Count);
            Assert.Equal("NEEDS_REVIEW", vlDetail.Status);
            using var vlValidated = await http.PostAsJsonAsync($"/api/statement-imports/{vlDetail.Id}/validate", new StatementVersionRequest(vlDetail.Version));
            vlValidated.EnsureSuccessStatusCode();
            vlDetail = (await vlValidated.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            using var vlPreviewResponse = await http.PostAsJsonAsync($"/api/statement-imports/{vlDetail.Id}/exports/{x3.Id}/preview", new ExportRequest(vlDetail.Version));
            vlPreviewResponse.EnsureSuccessStatusCode();
            var vlPreview = (await vlPreviewResponse.Content.ReadFromJsonAsync<ExportPreview>())!;
            Assert.Empty(vlPreview.Errors);
            Assert.Equal(38, vlPreview.TotalRows);
            Assert.Equal("1", vlPreview.Rows[0][7]);
            Assert.Equal("3", vlPreview.Rows[^1][7]);
            Assert.Equal("38", vlPreview.Rows[^1][8]);
            using var vlExport = await http.PostAsJsonAsync($"/api/statement-imports/{vlDetail.Id}/exports/{x3.Id}", new ExportRequest(vlDetail.Version, vlPreview.TemplateFingerprint));
            vlExport.EnsureSuccessStatusCode();
            var expectedVl = await ExportFixtures.Engine.GenerateAsync(vlDetail.Statement with
            {
                IsValidated = true
            }, ExportFixtures.X3(false), default);
            Assert.Equal(expectedVl.Content, await vlExport.Content.ReadAsByteArrayAsync());
            using var vlOriginal = await http.GetAsync($"/api/statement-imports/{vlDetail.Id}/original");
            Assert.Equal(scanned, await vlOriginal.Content.ReadAsByteArrayAsync());
            Assert.Equal(beforeVlModels, await http.GetStringAsync("/api/export-templates?pageSize=100"));
            // Repair a previously empty import from its retained original PDF without another cloud call.
            ocr.Result = NativeTableRecoveryTests.Ocr();
            var bodyPdf = NativeTableRecoveryTests.Pdf();
            using var recoverImport = await ImportPdf(bodyPdf, "fictional-body.pdf", bmce);
            recoverImport.EnsureSuccessStatusCode();
            var pendingRecovery = (await recoverImport.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal(3, pendingRecovery.Statement.Transactions.Count); // Fresh imports are recovered immediately.
            // Reproduce the old persisted empty state in this test's disposable database only.
            using (var scope = factory.Services.CreateScope())
            {
                var testDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var legacy = await testDb.StatementImports.SingleAsync(r => r.Id == pendingRecovery.Id);
                legacy.NormalizedJson = JsonSerializer.Serialize(pendingRecovery.Statement with
                {
                    Transactions = [],
                    OpeningBalance = null,
                    ClosingBalance = null,
                    TotalDebit = null,
                    TotalCredit = null
                }, StatementImportService.Json);
                legacy.TransactionCount = 0;
                legacy.RawExtractionJson = JsonSerializer.Serialize(ocr.Result, StatementImportService.Json);
                await testDb.SaveChangesAsync();
            }
            pendingRecovery = (await http.GetFromJsonAsync<StatementDetail>($"/api/statement-imports/{pendingRecovery.Id}", StatementImportService.Json))!;
            Assert.Empty(pendingRecovery.Statement.Transactions);
            Assert.True(pendingRecovery.Extraction!.NeedsMapping);
            var ocrCalls = ocr.Calls;
            using var recoveredResponse = await http.PostAsJsonAsync($"/api/statement-imports/{pendingRecovery.Id}/adapt", new AdaptExtractionRequest(pendingRecovery.Version));
            recoveredResponse.EnsureSuccessStatusCode();
            var recoveredDetail = (await recoveredResponse.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal(ocrCalls, ocr.Calls);
            Assert.Equal(3, recoveredDetail.Statement.Transactions.Count);
            Assert.False(recoveredDetail.Extraction!.NeedsMapping);
            Assert.Equal("NEEDS_REVIEW", recoveredDetail.Status);
            Assert.Equal(-100m, recoveredDetail.Statement.OpeningBalance);
            Assert.Equal(40m, recoveredDetail.Statement.ClosingBalance);
            Assert.Equal(60m, recoveredDetail.Statement.TotalDebit);
            Assert.Equal(200m, recoveredDetail.Statement.TotalCredit);
            Assert.Equal("CREDIT_INCREASES", recoveredDetail.Statement.BalanceConvention);
            var recoveredRaw = (await http.GetFromJsonAsync<PdfTextDocument>($"/api/statement-imports/{recoveredDetail.Id}/extraction"))!;
            Assert.Equal(2, recoveredRaw.NativePages!.Count);
            Assert.Equal(JsonSerializer.Serialize(ocr.Result.Pages, StatementImportService.Json), JsonSerializer.Serialize(recoveredRaw.Pages, StatementImportService.Json));
            Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync($"/api/statement-imports/{pendingRecovery.Id}/adapt", new AdaptExtractionRequest(pendingRecovery.Version))).StatusCode);
            // Legacy partial extraction: missing dates and a date year concatenated to credit.
            // Re-adaptation also replaces the totals computed from those corrupt movements.
            using (var scope = factory.Services.CreateScope())
            {
                var testDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var legacy = await testDb.StatementImports.SingleAsync(r => r.Id == recoveredDetail.Id);
                var corruptRows = recoveredDetail.Statement.Transactions.Select(t => t with
                {
                    Date = null,
                    Credit = t.Credit.HasValue ? t.Credit + 2026000m : null,
                    Amount = t.Credit.HasValue ? t.Credit + 2026000m : t.Amount
                }).ToList();
                legacy.NormalizedJson = JsonSerializer.Serialize(recoveredDetail.Statement with
                {
                    Transactions = corruptRows,
                    TotalCredit = corruptRows.Sum(t => t.Credit ?? 0m)
                }, StatementImportService.Json);
                legacy.ImportDiagnosticsJson = JsonSerializer.Serialize(new[] { new ExportError("PDF_TOTALS_COMPUTED", "Calculated from old movements") }, StatementImportService.Json);
                await testDb.SaveChangesAsync();
            }
            using var repairedResponse = await http.PostAsJsonAsync($"/api/statement-imports/{recoveredDetail.Id}/adapt", new AdaptExtractionRequest(recoveredDetail.Version));
            repairedResponse.EnsureSuccessStatusCode();
            recoveredDetail = (await repairedResponse.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal("NEEDS_REVIEW", recoveredDetail.Status);
            Assert.Equal(60m, recoveredDetail.Statement.TotalDebit);
            Assert.Equal(200m, recoveredDetail.Statement.TotalCredit);
            Assert.All(recoveredDetail.Statement.Transactions, t => Assert.NotNull(t.Date));
            Assert.Equal(ocrCalls, ocr.Calls);
            using var recoveryValidation = await http.PostAsJsonAsync($"/api/statement-imports/{recoveredDetail.Id}/validate", new StatementVersionRequest(recoveredDetail.Version));
            recoveryValidation.EnsureSuccessStatusCode();
            recoveredDetail = (await recoveryValidation.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            using var recoveryPreviewResponse = await http.PostAsJsonAsync($"/api/statement-imports/{recoveredDetail.Id}/exports/{models[4].Id}/preview", new ExportRequest(recoveredDetail.Version));
            recoveryPreviewResponse.EnsureSuccessStatusCode();
            var recoveryPreview = (await recoveryPreviewResponse.Content.ReadFromJsonAsync<ExportPreview>())!;
            Assert.Empty(recoveryPreview.Errors);
            Assert.Equal(5, recoveryPreview.TotalRows);
            using var recoveryFile = await http.PostAsJsonAsync($"/api/statement-imports/{recoveredDetail.Id}/exports/{models[4].Id}", new ExportRequest(recoveredDetail.Version, recoveryPreview.TemplateFingerprint));
            recoveryFile.EnsureSuccessStatusCode();
            Assert.Equal("txt", recoveryFile.Content.Headers.ContentDisposition!.FileNameStar!.Split('.').Last());
            Assert.Equal((await ExportFixtures.Engine.GenerateAsync(recoveredDetail.Statement with
            {
                IsValidated = true
            }, ExportFixtures.X3(true), default)).Content, await recoveryFile.Content.ReadAsByteArrayAsync());
            await VerifyAccessAndArchives(factory, http, adminUser, bmce, recoveredDetail, models[4], await recoveryFile.Content.ReadAsByteArrayAsync());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var command = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", admin);
            await command.ExecuteNonQueryAsync();
            // Remove only this test's newly generated temporary storage directory.
            Assert.Equal(Path.Combine(Path.GetTempPath(), name), Path.GetFullPath(storageRoot));
            if (Directory.Exists(storageRoot))
                Directory.Delete(storageRoot, true);
        }
    }
    private static async Task VerifyAccessAndArchives(WebApplicationFactory<Program> factory, HttpClient admin, UserDto adminUser, BankDto bank, StatementDetail statement, ExportDto template, byte[] exactFile)
    {
        using var clientResponse = await admin.PostAsJsonAsync("/api/clients", new ClientInput("Client archives", "Société fictive", "000000000000111", "111", "111", "archives@example.test", "+212600000000"));
        clientResponse.EnsureSuccessStatusCode();
        var client = (await clientResponse.Content.ReadFromJsonAsync<ClientDto>())!;
        using var otherResponse = await admin.PostAsJsonAsync("/api/clients", new ClientInput("Autre client", "Société fictive", "000000000000112", "112", "112", "other@example.test", "+212600000001"));
        otherResponse.EnsureSuccessStatusCode();
        var other = (await otherResponse.Content.ReadFromJsonAsync<ClientDto>())!;
        using var accountResponse = await admin.PostAsJsonAsync("/api/bank-accounts", new BankAccountInput(client.Id, bank.Id, "DEMO001", "Compte archives", "MAD", "BQ1", "512100"));
        accountResponse.EnsureSuccessStatusCode();
        var account = (await accountResponse.Content.ReadFromJsonAsync<BankAccountDto>())!;
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/statement-imports/{statement.Id}/association", new StatementAssociation(statement.Version, other.Id, account.Id))).StatusCode);
        using var associatedResponse = await admin.PutAsJsonAsync($"/api/statement-imports/{statement.Id}/association", new StatementAssociation(statement.Version, client.Id, account.Id));
        associatedResponse.EnsureSuccessStatusCode();
        var associated = (await associatedResponse.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
        Assert.Equal(client.Id, associated.ClientId);
        Assert.Equal(account.Id, associated.BankAccountId);
        Assert.Equal("BQ1", associated.Statement.Journal);
        Assert.Equal("512100", associated.Statement.Account);
        Assert.Equal("NEEDS_REVIEW", associated.Status);
        Assert.Equal("Administrateur test", associated.ModifiedByName);
        using var archiveResponse = await admin.GetAsync($"/api/client-workspace/{client.Id}/statements");
        archiveResponse.EnsureSuccessStatusCode();
        var archives = await archiveResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, archives.GetProperty("total").GetInt32());
        Assert.Equal(statement.Id.ToString(), archives.GetProperty("items")[0].GetProperty("id").GetString());
        var otherArchives = await admin.GetFromJsonAsync<JsonElement>($"/api/client-workspace/{other.Id}/statements");
        Assert.Equal(0, otherArchives.GetProperty("total").GetInt32());
        // The shared client workspace includes every stored import, including unassigned and unvalidated PDFs.
        var allImports = (await admin.GetFromJsonAsync<PageResult<StatementSummary>>("/api/statement-imports?pageSize=100"))!;
        var allArchives = await admin.GetFromJsonAsync<JsonElement>("/api/client-workspace/statements?pageSize=100");
        Assert.Equal(allImports.Total, allArchives.GetProperty("total").GetInt32());
        Assert.Equal(allImports.Items.Select(s => s.Id).Order(), allArchives.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).Order());
        var clientArchive = allArchives.GetProperty("items").EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == statement.Id);
        Assert.Equal(client.Name, clientArchive.GetProperty("clientName").GetString());
        Assert.Equal(associated.Statement.StatementNumber, clientArchive.GetProperty("statementNumber").GetString());
        var unassigned = await admin.GetFromJsonAsync<JsonElement>("/api/client-workspace/statements?unassigned=true&pageSize=100");
        Assert.Equal(allImports.Total - 1, unassigned.GetProperty("total").GetInt32());
        Assert.All(unassigned.GetProperty("items").EnumerateArray(), s => Assert.Equal(JsonValueKind.Null, s.GetProperty("clientId").ValueKind));
        Assert.Contains(unassigned.GetProperty("items").EnumerateArray(), s => s.GetProperty("sourceFileName").ValueKind == JsonValueKind.String && s.GetProperty("status").GetString() == "NEEDS_REVIEW");
        var searched = await admin.GetFromJsonAsync<JsonElement>("/api/client-workspace/statements?search=client%20archives");
        Assert.Equal(1, searched.GetProperty("total").GetInt32());
        var reviewed = await admin.GetFromJsonAsync<JsonElement>("/api/client-workspace/statements?status=VALIDATED&pageSize=100");
        Assert.All(reviewed.GetProperty("items").EnumerateArray(), s => Assert.Equal("VALIDATED", s.GetProperty("status").GetString()));
        var firstPage = await admin.GetFromJsonAsync<JsonElement>("/api/client-workspace/statements?page=1&pageSize=2");
        var nextPage = await admin.GetFromJsonAsync<JsonElement>("/api/client-workspace/statements?page=2&pageSize=2");
        Assert.Equal(2, firstPage.GetProperty("items").GetArrayLength());
        Assert.Equal(2, nextPage.GetProperty("items").GetArrayLength());
        Assert.Empty(firstPage.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).Intersect(nextPage.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("id").GetGuid())));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/client-workspace/statements?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/client-workspace/statements?status=INVALID")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/client-workspace/{Guid.NewGuid()}/statements")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.GetAsync($"/api/catalog-deletions/clients/{client.Id}")).StatusCode);
        var jobs = (await admin.GetFromJsonAsync<PageResult<ExportJobDto>>($"/api/statement-imports/{statement.Id}/exports"))!;
        var job = Assert.Single(jobs.Items);
        Assert.True(job.HasFile);
        Assert.Equal("Administrateur test", job.GeneratedByName);
        (await admin.PutAsJsonAsync($"/api/export-templates/{template.Id}", new ExportInput(template.Name + " modifié", template.Type, template.Description, template.FileExtension, template.Delimiter, template.Encoding, template.NumberCulture, template.IncludeHeader, template.IsActive, template.Fields))).EnsureSuccessStatusCode();
        // Archived bytes survive subsequent statement revisions and template edits.
        using var savedFile = await admin.GetAsync($"/api/statement-imports/{statement.Id}/exports/archives/{job.Id}/file");
        savedFile.EnsureSuccessStatusCode();
        Assert.Equal(exactFile, await savedFile.Content.ReadAsByteArrayAsync());
        using var standard = await admin.GetAsync($"/api/statement-imports/{statement.Id}/exports/archives/{job.Id}/file?standard=true");
        standard.EnsureSuccessStatusCode();
        Assert.Contains("Libellé", Encoding.UTF8.GetString(await standard.Content.ReadAsByteArrayAsync()));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/statement-imports/{Guid.NewGuid()}/exports/archives/{job.Id}/file")).StatusCode);
        var preview = await admin.GetFromJsonAsync<JsonElement>($"/api/statement-imports/{statement.Id}/exports/archives/{job.Id}/preview");
        Assert.Equal(Encoding.ASCII.GetString(exactFile), preview.GetProperty("text").GetString());
        Assert.Equal(template.Name, preview.GetProperty("templateNameSnapshot").GetString());
        // Multipart PDF uploads are filed directly under their client/account and reject incompatible associations before OCR.
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(bank.Id.ToString()), "bankId");
        form.Add(new StringContent("MAD"), "currency");
        form.Add(new StringContent(client.Id.ToString()), "clientId");
        form.Add(new StringContent(account.Id.ToString()), "bankAccountId");
        var pdf = new ByteArrayContent(NativeTableRecoveryTests.Pdf());
        pdf.Headers.ContentType = new("application/pdf");
        form.Add(pdf, "file", "client-archive.pdf");
        using var imported = await admin.PostAsync("/api/statement-imports/pdf", form);
        imported.EnsureSuccessStatusCode();
        var filed = (await imported.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
        Assert.Equal(client.Id, filed.ClientId);
        Assert.Equal(account.Id, filed.BankAccountId);
        Assert.Equal("Administrateur test", filed.ImportedByName);
        Assert.Equal("BQ1", filed.Statement.Journal);
        using var userResponse = await admin.PostAsJsonAsync("/api/users", new UserInput("Lecteur", "reader@example.test", "User", true, Permissions.Reader, "Fictional-password-2026"));
        userResponse.EnsureSuccessStatusCode();
        var readerUser = (await userResponse.Content.ReadFromJsonAsync<UserDto>())!;
        using var reader = factory.CreateClient();
        reader.DefaultRequestHeaders.Add("X-Alias-Request", "1");
        Assert.Equal(HttpStatusCode.Unauthorized, (await reader.PostAsJsonAsync("/api/auth/login", new LoginInput(readerUser.Email, "Incorrect-password"))).StatusCode);
        (await reader.PostAsJsonAsync("/api/auth/login", new LoginInput(readerUser.Email, "Fictional-password-2026"))).EnsureSuccessStatusCode();
        (await reader.GetAsync($"/api/client-workspace/{client.Id}/statements")).EnsureSuccessStatusCode();
        (await reader.GetAsync($"/api/statement-imports/{statement.Id}/exports/archives/{job.Id}/preview")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync($"/api/statement-imports/{statement.Id}/exports/{template.Id}", new ExportRequest(associated.Version, AllowErrors: true, EditedText: "TEXTE MANUEL"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/client-workspace/statements")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsJsonAsync($"/api/statement-imports/{statement.Id}?version={associated.Version}", associated.Statement with
        {
            Transactions = []
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync($"/api/statement-imports/{statement.Id}/validate", new StatementVersionRequest(associated.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync($"/api/statement-imports/{filed.Id}/complete-metadata", new StatementVersionRequest(filed.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync($"/api/statement-imports/{filed.Id}/prepare", new PrepareStatementExportRequest(filed.Version, Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync($"/api/statement-imports/{statement.Id}/exports/archives/{job.Id}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync($"/api/statement-imports/{statement.Id}/original?download=true")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync($"/api/statement-imports/{statement.Id}/original?download=True")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync("/api/clients", new ClientInput("Interdit", "Interdit", "000000000000113", "113", "113", "blocked@example.test", "+212600000000"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/users", new UserInput("Permissions invalides", "bad@example.test", "User", true, ["documents.export"], "Fictional-password-2026"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync("/api/statement-imports/delete-selection", new[] { new StatementVersionToDelete(filed.Id, filed.Version) })).StatusCode);
        var granted = new UserInput(readerUser.Name, readerUser.Email, "User", true, Permissions.Accountant, null, readerUser.Version);
        using var grantResponse = await admin.PutAsJsonAsync($"/api/users/{readerUser.Id}", granted);
        grantResponse.EnsureSuccessStatusCode();
        readerUser = (await grantResponse.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await reader.GetAsync("/api/clients")).StatusCode); // Session revoked immediately after permissions change.
        (await reader.PostAsJsonAsync("/api/auth/login", new LoginInput(readerUser.Email, "Fictional-password-2026"))).EnsureSuccessStatusCode();
        (await reader.GetAsync($"/api/statement-imports/{statement.Id}/exports/archives/{job.Id}/file")).EnsureSuccessStatusCode();
        (await reader.PostAsJsonAsync($"/api/statement-imports/{statement.Id}/validate", new StatementVersionRequest(associated.Version))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PutAsJsonAsync($"/api/export-templates/{template.Id}", new ExportInput(template.Name, template.Type, template.Description, template.FileExtension, template.Delimiter, template.Encoding, template.NumberCulture, template.IncludeHeader, template.IsActive, template.Fields))).StatusCode);
        using var restrict = await admin.PutAsJsonAsync($"/api/users/{readerUser.Id}", new UserInput(readerUser.Name, readerUser.Email, "User", true, ["banks.read", "banks.write"], null, readerUser.Version));
        restrict.EnsureSuccessStatusCode();
        readerUser = (await restrict.Content.ReadFromJsonAsync<UserDto>())!;
        (await reader.PostAsJsonAsync("/api/auth/login", new LoginInput(readerUser.Email, "Fictional-password-2026"))).EnsureSuccessStatusCode();
        (await reader.GetAsync("/api/banks")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync($"/api/banks/{bank.Id}/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync($"/api/catalog-deletions/banks/{bank.Id}", new
        {
            version = "untrusted"
        })).StatusCode);
        (await admin.PutAsJsonAsync($"/api/users/{readerUser.Id}", granted with
        {
            IsActive = false,
            Version = readerUser.Version
        })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await reader.GetAsync("/api/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await reader.PostAsJsonAsync("/api/auth/login", new LoginInput(readerUser.Email, "Fictional-password-2026"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/users/{adminUser.Id}", new UserInput(adminUser.Name, adminUser.Email, "User", true, Permissions.Reader, null, adminUser.Version))).StatusCode);
        using var unsafeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        using var noHeader = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await noHeader.SendAsync(unsafeRequest)).StatusCode);

        // Five statements per account: real deletion of the source, exports and queued files;
        // traceability remains readable after the underlying statement disappears.
        var traceBefore = await admin.GetFromJsonAsync<JsonElement>($"/api/client-workspace/history?statementId={statement.Id}");
        Assert.Contains(traceBefore.GetProperty("items").EnumerateArray(), x => x.GetProperty("action").GetString() == "MODIFIED");
        Assert.Contains(traceBefore.GetProperty("items").EnumerateArray(), x => x.GetProperty("action").GetString() == "EXPORTED");
        using var anotherAccountResponse = await admin.PostAsJsonAsync("/api/bank-accounts", new BankAccountInput(client.Id, bank.Id, "DEMO002", "Second compte", "MAD", "BQ2", "512200"));
        anotherAccountResponse.EnsureSuccessStatusCode();
        var anotherAccount = (await anotherAccountResponse.Content.ReadFromJsonAsync<BankAccountDto>())!;
        async Task<StatementDetail> AddToAccount(Guid accountId)
        {
            using var response = await admin.PostAsJsonAsync("/api/statement-imports", ExportFixtures.Statement with
            {
                BankName = bank.Name,
                BankCode = bank.Code
            });
            response.EnsureSuccessStatusCode();
            var added = (await response.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            using var association = await admin.PutAsJsonAsync($"/api/statement-imports/{added.Id}/association", new StatementAssociation(added.Version, client.Id, accountId));
            association.EnsureSuccessStatusCode();
            return (await association.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
        }
        var isolated = await AddToAccount(anotherAccount.Id);
        string? sourceKey;
        using (var scope = factory.Services.CreateScope())
            sourceKey = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().StatementImports.SingleAsync(x => x.Id == filed.Id)).SourceStorageKey;
        for (var i = 0; i < 5; i++)
            await AddToAccount(account.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/statement-imports/{statement.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/statement-imports/{filed.Id}/original")).StatusCode);
        (await admin.GetAsync($"/api/statement-imports/{isolated.Id}")).EnsureSuccessStatusCode();
        var remaining = await admin.GetFromJsonAsync<JsonElement>($"/api/client-workspace/{client.Id}/statements?pageSize=100");
        Assert.Equal(5, remaining.GetProperty("items").EnumerateArray().Count(x => x.GetProperty("bankAccountId").GetGuid() == account.Id));
        var deletedHistory = await admin.GetFromJsonAsync<JsonElement>($"/api/client-workspace/history?statementId={statement.Id}");
        var deletion = Assert.Single(deletedHistory.GetProperty("items").EnumerateArray(), x => x.GetProperty("action").GetString() == "DELETED");
        Assert.Equal("Administrateur test", deletion.GetProperty("actorName").GetString());
        Assert.Equal(client.Name, deletion.GetProperty("clientName").GetString());
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await database.StatementImports.AnyAsync(x => x.Id == statement.Id || x.Id == filed.Id));
            Assert.False(await database.ExportJobs.AnyAsync(x => x.StatementId == statement.Id));
            Assert.True(await database.PendingFileDeletions.AnyAsync(x => x.StorageKey == sourceKey));
        }
        await Task.WhenAll(AddToAccount(account.Id), AddToAccount(account.Id));
        var concurrent = await admin.GetFromJsonAsync<JsonElement>($"/api/client-workspace/{client.Id}/statements?pageSize=100");
        Assert.Equal(5, concurrent.GetProperty("items").EnumerateArray().Count(x => x.GetProperty("bankAccountId").GetGuid() == account.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/client-workspace/history?page=0")).StatusCode);
        using var completionCreate = await admin.PostAsJsonAsync("/api/statement-imports", ExportFixtures.Statement);
        completionCreate.EnsureSuccessStatusCode();
        var completion = (await completionCreate.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/statement-imports/{completion.Id}/download-complete", new StatementVersionRequest(completion.Version))).StatusCode);
        (await admin.PostAsJsonAsync($"/api/statement-imports/{completion.Id}/exports/{template.Id}", new ExportRequest(completion.Version, AllowErrors: true))).EnsureSuccessStatusCode();
        using var completedResponse = await admin.PostAsJsonAsync($"/api/statement-imports/{completion.Id}/download-complete", new StatementVersionRequest(completion.Version));
        completedResponse.EnsureSuccessStatusCode();
        var completed = (await completedResponse.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
        Assert.Equal("EXPORTED", completed.Status);
        Assert.Null(completed.ValidatedAt);
        Assert.Equal(completion.Version + 1, completed.Version);
        StatementDetail profileSource;
        using (var scope = factory.Services.CreateScope())
        {
            var rawDocument = new PdfTextDocument("PADDLE_OCR", [new(1,600,800,[],Tables:[new(new List<IReadOnlyList<string>> {
                new[]{"Jour mouvement","Valeur","Texte mouvement","Débit","Crédit"},
                new[]{"10/03/2026","11/03/2026","TEST CLIENT","","50,00"}
            })])]);
            profileSource = await scope.ServiceProvider.GetRequiredService<StatementImportService>().CreateExtractedAsync(new(ExportFixtures.Statement, [], rawDocument), default);
        }
        var profileMapping = new PdfColumnMapping(TableColumns: new PdfTableColumnMapping());
        var profileAliases = new Dictionary<string, string[]> { { "Date", ["Jour mouvement"] }, { "Description", ["Texte mouvement"] } };
        var profileInput = new
        {
            Name = "Modèle test réutilisable",
            StatementId = profileSource.Id,
            StatementVersion = profileSource.Version,
            Mapping = profileMapping,
            Aliases = profileAliases,
            IsActive = true,
            Version = 1
        };
        using var profileResponse = await admin.PostAsJsonAsync("/api/extraction-profiles", profileInput);
        profileResponse.EnsureSuccessStatusCode();
        var profile = (await profileResponse.Content.ReadFromJsonAsync<ExtractionProfile>(StatementImportService.Json))!;
        Assert.DoesNotContain("TEST CLIENT", profile.SampleJson);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/extraction-profiles/{profile.Id}", profileInput with
        {
            Version = 999
        })).StatusCode);
        using var updatedProfileResponse = await admin.PutAsJsonAsync($"/api/extraction-profiles/{profile.Id}", profileInput with
        {
            Name = "Modèle test modifié"
        });
        updatedProfileResponse.EnsureSuccessStatusCode();
        profile = (await updatedProfileResponse.Content.ReadFromJsonAsync<ExtractionProfile>(StatementImportService.Json))!;
        Assert.Equal(2, profile.Version);
        using var applyResponse = await admin.PostAsJsonAsync($"/api/statement-imports/{profileSource.Id}/adapt", new AdaptExtractionRequest(profileSource.Version, ProfileId: profile.Id));
        applyResponse.EnsureSuccessStatusCode();
        var profileApplied = (await applyResponse.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
        Assert.Equal("TEST CLIENT", Assert.Single(profileApplied.Statement.Transactions).Description);
        var applicationHistory = await admin.GetFromJsonAsync<JsonElement>($"/api/client-workspace/history?statementId={profileSource.Id}");
        Assert.Contains(applicationHistory.GetProperty("items").EnumerateArray(), x => x.GetProperty("action").GetString() == "MODEL_APPLIED");
        (await admin.PostAsJsonAsync("/api/statement-imports/delete-selection", new[] { new StatementVersionToDelete(profileApplied.Id, profileApplied.Version) })).EnsureSuccessStatusCode();
        Assert.Contains((await admin.GetFromJsonAsync<List<ExtractionProfile>>("/api/extraction-profiles"))!, x => x.Id == profile.Id);
        (await admin.DeleteAsync($"/api/extraction-profiles/{profile.Id}?version={profile.Version}")).EnsureSuccessStatusCode();
        var profileHistory = await admin.GetFromJsonAsync<JsonElement>("/api/extraction-profiles/history");
        Assert.Contains(profileHistory.GetProperty("items").EnumerateArray(), x => x.GetProperty("profileId").GetGuid() == profile.Id && x.GetProperty("action").GetString() == "DELETED");
        using var verifiedResponse = await admin.PostAsJsonAsync($"/api/statement-imports/{completed.Id}/validate", new StatementVersionRequest(completed.Version));
        verifiedResponse.EnsureSuccessStatusCode();
        var verified = (await verifiedResponse.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
        Assert.Equal("VALIDATED", verified.Status);
        Assert.NotNull(verified.ValidatedAt);
        var detailedHistory = await admin.GetFromJsonAsync<JsonElement>($"/api/client-workspace/history?statementId={completed.Id}");
        Assert.Contains(detailedHistory.GetProperty("items").EnumerateArray(), x => x.GetProperty("action").GetString() == "DOWNLOAD_COMPLETED" && x.GetProperty("changesJson").GetString()!.Contains("EXPORTED"));
        using var deleteCreate = await admin.PostAsJsonAsync("/api/statement-imports", ExportFixtures.Statement);
        deleteCreate.EnsureSuccessStatusCode();
        var toDelete = (await deleteCreate.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/statement-imports/delete-selection", new[] { new StatementVersionToDelete(toDelete.Id, toDelete.Version + 1) })).StatusCode);
        (await admin.GetAsync($"/api/statement-imports/{toDelete.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/statement-imports/delete-selection", new[] { new StatementVersionToDelete(toDelete.Id, toDelete.Version), new StatementVersionToDelete(Guid.NewGuid(), 1) })).StatusCode);
        (await admin.GetAsync($"/api/statement-imports/{toDelete.Id}")).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/statement-imports/delete-selection", new[] { new StatementVersionToDelete(toDelete.Id, toDelete.Version) })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/statement-imports/{toDelete.Id}")).StatusCode);
        var manualHistory = await admin.GetFromJsonAsync<JsonElement>($"/api/client-workspace/history?statementId={toDelete.Id}");
        Assert.Contains(manualHistory.GetProperty("items").EnumerateArray(), x => x.GetProperty("action").GetString() == "DELETED");
        (await admin.PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/clients")).StatusCode);
    }
}
