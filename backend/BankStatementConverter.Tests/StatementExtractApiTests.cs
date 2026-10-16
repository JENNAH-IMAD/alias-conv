using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BankStatementConverter.Application;
using BankStatementConverter.Application.Exports;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure.Exports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace BankStatementConverter.Tests;

public class StatementExtractApiTests
{
    [Fact, Trait("Category", "Integration")]
    public async Task SavedExtractSurvivesCorrectionsPreservesValidationAndRejectsStaleVersions()
    {
        var configured = Environment.GetEnvironmentVariable("TEST_DATABASE_URL");
        if (string.IsNullOrWhiteSpace(configured))
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(root.FullName, "backend/BankStatementConverter.API/appsettings.Development.json")))
                root = root.Parent ?? throw new DirectoryNotFoundException("Configuration de test introuvable.");
            using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName, "backend/BankStatementConverter.API/appsettings.Development.json")));
            configured = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("Default").GetString();
        }
        var name = "alias_extract_test_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(configured) { Database = "postgres" }.ConnectionString);
        await connection.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection))
            await command.ExecuteNonQueryAsync();
        try
        {
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing").ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = new NpgsqlConnectionStringBuilder(configured) { Database = name }.ConnectionString,
                ["Database:AutoMigrate"] = "true",
                ["Seed:DemoData"] = "false",
                ["ArchiveCleanup:Enabled"] = "false"
            })));
            using var http = factory.CreateClient();
            http.DefaultRequestHeaders.Add("X-Alias-Request", "1");
            (await http.PostAsJsonAsync("/api/auth/setup", new SetupInput("Test", "extract@example.test", "Fictional-password-2026"))).EnsureSuccessStatusCode();
            using var created = await http.PostAsJsonAsync("/api/statement-imports", ExportFixtures.Statement);
            created.EnsureSuccessStatusCode();
            var source = (await created.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            using var validation = await http.PostAsJsonAsync($"/api/statement-imports/{source.Id}/validate", new StatementVersionRequest(source.Version));
            validation.EnsureSuccessStatusCode();
            source = (await validation.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            source = (await http.GetFromJsonAsync<StatementDetail>($"/api/statement-imports/{source.Id}", StatementImportService.Json))!;
            using var savedResponse = await http.PostAsJsonAsync($"/api/statement-imports/{source.Id}/extracts", new SaveStatementExtractRequest(source.Version, "frais bancaires"));
            savedResponse.EnsureSuccessStatusCode();
            var saved = (await savedResponse.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal("VALIDATED", saved.Status);
            Assert.Equal(source.ValidatedAt, saved.ValidatedAt);
            Assert.Equal(source.Version + 1, saved.Version);
            var extract = Assert.Single(saved.Statement.Extracts);
            var archive = await http.GetFromJsonAsync<JsonElement>("/api/client-workspace/extracts");
            Assert.Equal(1, archive.GetProperty("total").GetInt32());
            var archivedItem = Assert.Single(archive.GetProperty("items").EnumerateArray());
            Assert.Equal(extract.Id, archivedItem.GetProperty("id").GetGuid());
            Assert.Equal(source.Id, archivedItem.GetProperty("statementId").GetGuid());
            Assert.Equal("125.50", archivedItem.GetProperty("totalDebit").GetString());
            var noMatch = await http.GetFromJsonAsync<JsonElement>("/api/client-workspace/extracts?search=absent");
            Assert.Equal(0, noMatch.GetProperty("total").GetInt32());
            var preview = await http.GetFromJsonAsync<StatementExtract>($"/api/statement-imports/{source.Id}/extracts/{extract.Id}", StatementImportService.Json);
            Assert.Equal(extract.Id, preview!.Id);
            Assert.Single(preview.Transactions);
            using var extractImportResponse = await http.PostAsJsonAsync("/api/statement-imports", ExportFixtures.Statement with
            {
                ConversionType = "EXTRACT"
            });
            extractImportResponse.EnsureSuccessStatusCode();
            var extractImport = (await extractImportResponse.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal("EXTRACT", extractImport.Statement.ConversionType);
            var extractSources = await http.GetFromJsonAsync<JsonElement>("/api/client-workspace/statements?conversionType=EXTRACT");
            Assert.Equal(extractImport.Id, Assert.Single(extractSources.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
            var statementSources = await http.GetFromJsonAsync<JsonElement>("/api/client-workspace/statements?conversionType=STATEMENT");
            Assert.Equal(source.Id, Assert.Single(statementSources.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
            using var excludedSave = await http.PostAsJsonAsync($"/api/statement-imports/{extractImport.Id}/extracts", new SaveStatementExtractRequest(extractImport.Version, "", "client"));
            excludedSave.EnsureSuccessStatusCode();
            var scopedDetail = (await excludedSave.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            var scopedExtract = Assert.Single(scopedDetail.Statement.Extracts);
            Assert.Single(scopedExtract.Transactions);
            Assert.Equal("client", scopedExtract.ExcludeText);
            var excludedArchive = await http.GetFromJsonAsync<JsonElement>("/api/client-workspace/extracts?search=client");
            Assert.Equal(scopedExtract.Id, Assert.Single(excludedArchive.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
            using (var structuredPdf = UglyToad.PdfPig.PdfDocument.Open(await http.GetByteArrayAsync($"/api/statement-imports/{extractImport.Id}/normalized-pdf?version={scopedDetail.Version}&extractId={scopedExtract.Id}&preview=true")))
            {
                var pdfText = string.Join('\n', structuredPdf.GetPages().Select(p => p.Text));
                Assert.Contains("Extrait de relevé bancaire", pdfText);
                Assert.Contains("FRAIS BANCAIRES", pdfText);
                Assert.DoesNotContain("VIREMENT CLIENT", pdfText);
            }
            ExportInput Input(ExportTemplate t) => new(t.Name, t.Type, t.Description, t.FileExtension, t.Delimiter, t.Encoding, t.NumberCulture, t.IncludeHeader, true, t.Fields.Select(f => new ExportFieldDto(f.FieldName, f.SourceField, f.Position, f.Format, f.Required)).ToList());
            using var templateResponse = await http.PostAsJsonAsync("/api/export-templates", Input(ExportFixtures.Csv()));
            templateResponse.EnsureSuccessStatusCode();
            var csvTemplate = (await templateResponse.Content.ReadFromJsonAsync<ExportDto>())!;
            using var previewResponse = await http.PostAsJsonAsync($"/api/statement-imports/{extractImport.Id}/exports/{csvTemplate.Id}/preview", new ExportRequest(scopedDetail.Version, ExtractId: scopedExtract.Id));
            previewResponse.EnsureSuccessStatusCode();
            var scopedPreview = (await previewResponse.Content.ReadFromJsonAsync<ExportPreview>(StatementImportService.Json))!;
            Assert.Equal(scopedExtract.Id, scopedPreview.ExtractId);
            Assert.Single(scopedPreview.Rows);
            Assert.Contains("FRAIS BANCAIRES", string.Join(';', scopedPreview.Rows[0]));
            Assert.DoesNotContain("VIREMENT", scopedPreview.Text);
            using var scopedExportResponse = await http.PostAsJsonAsync($"/api/statement-imports/{extractImport.Id}/exports/{csvTemplate.Id}", new ExportRequest(scopedDetail.Version, scopedPreview.TemplateFingerprint, AllowErrors: true, ExtractId: scopedExtract.Id));
            scopedExportResponse.EnsureSuccessStatusCode();
            var scopedFile = await scopedExportResponse.Content.ReadAsStringAsync();
            Assert.Contains("FRAIS BANCAIRES", scopedFile);
            Assert.DoesNotContain("VIREMENT CLIENT", scopedFile);
            using var fullPreviewResponse = await http.PostAsJsonAsync($"/api/statement-imports/{extractImport.Id}/exports/{csvTemplate.Id}/preview", new ExportRequest(scopedDetail.Version));
            fullPreviewResponse.EnsureSuccessStatusCode();
            var fullPreview = (await fullPreviewResponse.Content.ReadFromJsonAsync<ExportPreview>(StatementImportService.Json))!;
            Assert.Null(fullPreview.ExtractId);
            Assert.Equal(2, fullPreview.TotalRows);
            Assert.Contains("VIREMENT CLIENT", fullPreview.Text);
            Assert.Contains("FRAIS BANCAIRES", fullPreview.Text);
            using var sageTemplateResponse = await http.PostAsJsonAsync("/api/export-templates", Input(ExportFixtures.X3(false)));
            sageTemplateResponse.EnsureSuccessStatusCode();
            var sageTemplate = (await sageTemplateResponse.Content.ReadFromJsonAsync<ExportDto>())!;
            using var sagePreviewResponse = await http.PostAsJsonAsync($"/api/statement-imports/{extractImport.Id}/exports/{sageTemplate.Id}/preview", new ExportRequest(scopedDetail.Version, ExtractId: scopedExtract.Id));
            sagePreviewResponse.EnsureSuccessStatusCode();
            var sagePreview = (await sagePreviewResponse.Content.ReadFromJsonAsync<ExportPreview>(StatementImportService.Json))!;
            Assert.Equal(3, sagePreview.TotalRows);
            Assert.Contains("FRAIS BANCAIRES", sagePreview.Text);
            Assert.DoesNotContain("VIREMENT CLIENT", sagePreview.Text);
            using var editedSage = await http.PostAsJsonAsync($"/api/statement-imports/{extractImport.Id}/exports/{sageTemplate.Id}", new ExportRequest(scopedDetail.Version, sagePreview.TemplateFingerprint, AllowErrors: true, EditedText: "TXT MANUEL EXTRAIT\r\n", ExtractId: scopedExtract.Id));
            editedSage.EnsureSuccessStatusCode();
            Assert.Equal("TXT MANUEL EXTRAIT\r\n", await editedSage.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.Conflict, (await http.DeleteAsync($"/api/statement-imports/{extractImport.Id}/extracts/{scopedExtract.Id}?version=0")).StatusCode);
            using var resetResponse = await http.DeleteAsync($"/api/statement-imports/{extractImport.Id}/extracts/{scopedExtract.Id}?version={scopedDetail.Version}");
            resetResponse.EnsureSuccessStatusCode();
            var reset = (await resetResponse.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Empty(reset.Statement.Extracts);
            Assert.Equal(2, reset.Statement.Transactions.Count);
            using var directDownload = await http.PostAsJsonAsync($"/api/statement-imports/{extractImport.Id}/exports/{csvTemplate.Id}", new ExportRequest(reset.Version, AllowErrors: true));
            directDownload.EnsureSuccessStatusCode();
            var directFile = await directDownload.Content.ReadAsStringAsync();
            Assert.Contains("VIREMENT CLIENT", directFile);
            Assert.Contains("FRAIS BANCAIRES", directFile);
            var invalidCorrection = reset.Statement with
            {
                Transactions = reset.Statement.Transactions.Select(t => t with { Description = "MODIFIÉ" }).ToList()
            };
            Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync($"/api/statement-imports/{extractImport.Id}/extracts", new SaveStatementExtractRequest(reset.Version, "frais", Statement: invalidCorrection))).StatusCode);
            var afterRejected = await http.GetFromJsonAsync<StatementDetail>($"/api/statement-imports/{extractImport.Id}", StatementImportService.Json);
            Assert.Equal(reset.Version, afterRejected!.Version);
            Assert.Equal("FRAIS BANCAIRES", afterRejected.Statement.Transactions[1].Description);
            Assert.Empty(afterRejected.Statement.Extracts);
            var correction = reset.Statement with
            {
                TotalDebit = 10m,
                Transactions = reset.Statement.Transactions.Select(t => t.Debit.HasValue ? t with { Debit = 10m, Amount = 10m } : t).ToList()
            };
            using var correctionResponse = await http.PostAsJsonAsync($"/api/statement-imports/{extractImport.Id}/extracts", new SaveStatementExtractRequest(reset.Version, "frais", Statement: correction));
            correctionResponse.EnsureSuccessStatusCode();
            var corrected = (await correctionResponse.Content.ReadFromJsonAsync<StatementDetail>(StatementImportService.Json))!;
            Assert.Equal(reset.Version + 1, corrected.Version);
            Assert.Equal(10m, Assert.Single(corrected.Statement.Extracts).TotalDebit);
            Assert.Equal(10m, corrected.Statement.Transactions[1].Debit);
            Assert.Equal(125.50m, extract.TotalDebit);
            Assert.Equal(0m, extract.TotalCredit);
            Assert.Equal(-125.50m, extract.Balance);
            Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync($"/api/statement-imports/{source.Id}/extracts", new SaveStatementExtractRequest(source.Version, "frais"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync($"/api/statement-imports/{source.Id}/extracts", new SaveStatementExtractRequest(saved.Version, "absent"))).StatusCode);
            var changed = saved.Statement with
            {
                Extracts = [],
                Transactions = saved.Statement.Transactions.Select(t => t with { Description = "MODIFIÉ", Debit = t.Debit.HasValue ? 1m : null }).ToList()
            };
            (await http.PutAsJsonAsync($"/api/statement-imports/{source.Id}?version={saved.Version}", changed)).EnsureSuccessStatusCode();
            var reloaded = await http.GetFromJsonAsync<StatementDetail>($"/api/statement-imports/{source.Id}", StatementImportService.Json);
            Assert.Equal(extract.Id, Assert.Single(reloaded!.Statement.Extracts).Id);
            var csv = await http.GetStringAsync($"/api/statement-imports/{source.Id}/extracts/{extract.Id}/file");
            Assert.Contains("FRAIS BANCAIRES", csv);
            Assert.Contains("125,50", csv);
            Assert.DoesNotContain("MODIFIÉ", csv);
            Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/statement-imports/{source.Id}/extracts/{Guid.NewGuid()}/file")).StatusCode);
            using var clientResponse = await http.PostAsJsonAsync("/api/clients", new ClientInput("Client extraits", "Société fictive", "000000000000111", "111", "111", "client@example.test", "+212600000000"));
            clientResponse.EnsureSuccessStatusCode();
            var client = (await clientResponse.Content.ReadFromJsonAsync<ClientDto>())!;
            using var bankResponse = await http.PostAsJsonAsync("/api/banks", new BankInput("BANK OF AFRICA", "BOA", ""));
            bankResponse.EnsureSuccessStatusCode();
            var bank = (await bankResponse.Content.ReadFromJsonAsync<BankDto>())!;
            using var accountResponse = await http.PostAsJsonAsync("/api/bank-accounts", new BankAccountInput(client.Id, bank.Id, "DEMO001", "Compte extraits", "MAD", "BQ1", "512100"));
            accountResponse.EnsureSuccessStatusCode();
            var account = (await accountResponse.Content.ReadFromJsonAsync<BankAccountDto>())!;
            (await http.PutAsJsonAsync($"/api/statement-imports/{source.Id}/association", new StatementAssociation(reloaded.Version, client.Id, account.Id))).EnsureSuccessStatusCode();
            var clientExtracts = await http.GetFromJsonAsync<JsonElement>($"/api/client-workspace/{client.Id}/extracts");
            var clientExtract = Assert.Single(clientExtracts.GetProperty("items").EnumerateArray());
            Assert.Equal(client.Id, clientExtract.GetProperty("clientId").GetGuid());
            Assert.Equal(client.Name, clientExtract.GetProperty("clientName").GetString());
            Assert.Equal(account.AccountName, clientExtract.GetProperty("accountName").GetString());
            using var readerResponse = await http.PostAsJsonAsync("/api/users", new UserInput("Lecteur", "reader@example.test", "User", true, Permissions.Reader, "Fictional-password-2026"));
            readerResponse.EnsureSuccessStatusCode();
            using var reader = factory.CreateClient();
            reader.DefaultRequestHeaders.Add("X-Alias-Request", "1");
            (await reader.PostAsJsonAsync("/api/auth/login", new LoginInput("reader@example.test", "Fictional-password-2026"))).EnsureSuccessStatusCode();
            (await reader.GetAsync($"/api/client-workspace/{client.Id}/extracts")).EnsureSuccessStatusCode();
            (await reader.GetAsync($"/api/statement-imports/{source.Id}/extracts/{extract.Id}")).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync($"/api/statement-imports/{source.Id}/extracts/{extract.Id}/file")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync($"/api/statement-imports/{source.Id}/extracts", new SaveStatementExtractRequest(reloaded.Version, "frais"))).StatusCode);
            (await http.PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsJsonAsync($"/api/statement-imports/{source.Id}/extracts", new SaveStatementExtractRequest(reloaded.Version, "frais"))).StatusCode);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
