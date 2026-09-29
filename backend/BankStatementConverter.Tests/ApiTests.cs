using System.Net;
using Microsoft.EntityFrameworkCore;
using BankStatementConverter.Infrastructure;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using BankStatementConverter.Application;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace BankStatementConverter.Tests;

public class ApiTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostgreSqlAuthenticationCrudConversionAndAuthorization()
    {
        var connection = Environment.GetEnvironmentVariable("TEST_DATABASE_URL");
        Assert.False(string.IsNullOrWhiteSpace(connection), "Définir TEST_DATABASE_URL vers une base PostgreSQL de test dédiée.");
        var suffix = Guid.NewGuid().ToString("N"); var password = "TestA1" + Convert.ToHexString(RandomNumberGenerator.GetBytes(18));
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", connection);
        Environment.SetEnvironmentVariable("Jwt__Key", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        Environment.SetEnvironmentVariable("Seed__AdminEmail", $"admin-{suffix}@example.test");
        Environment.SetEnvironmentVariable("Seed__AdminPassword", password);
        Environment.SetEnvironmentVariable("Database__AutoMigrate", "true");
        Environment.SetEnvironmentVariable("Storage__Root", Path.Combine(Path.GetTempPath(), "bankconverter-tests", suffix));
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider()));
        using var http = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsJsonAsync("/api/auth/login",new LoginDto($"admin-{suffix}@example.test","WrongPassword12"))).StatusCode);
        var login = await http.PostAsJsonAsync("/api/auth/login", new LoginDto($"admin-{suffix}@example.test",password)); login.EnsureSuccessStatusCode();
        var session = (await login.Content.ReadFromJsonAsync<AuthResult>())!; Assert.Equal("Admin",session.User.Role);
        http.DefaultRequestHeaders.Authorization = new("Bearer", session.Token);
        var invalid = await http.PostAsJsonAsync("/api/clients",new ClientInput("", "", "123", "", "", "invalid", "")); Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
        var ice = DateTime.UtcNow.Ticks.ToString()[^15..];
        var clientInput = new ClientInput("Test", "Fictif", ice,"IFTEST","RCTEST","client@example.test","+212600000000");
        var clientResponse = await http.PostAsJsonAsync("/api/clients",clientInput); clientResponse.EnsureSuccessStatusCode();
        var client = (await clientResponse.Content.ReadFromJsonAsync<ClientDto>())!;
        Assert.Equal(HttpStatusCode.Conflict,(await http.PostAsJsonAsync("/api/clients",clientInput)).StatusCode);
        var updated = await http.PutAsJsonAsync($"/api/clients/{client.Id}",clientInput with {Name="Modifié"}); updated.EnsureSuccessStatusCode(); Assert.Equal("Modifié",(await updated.Content.ReadFromJsonAsync<ClientDto>())!.Name);
        var photoPath = $"/api/clients/{client.Id}/photo";
        using (var anonymous = factory.CreateClient()) Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(photoPath)).StatusCode);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aOZkAAAAASUVORK5CYII=");
        async Task<HttpResponseMessage> UploadPhoto(byte[] bytes) { using var form = new MultipartFormDataContent(); var content = new ByteArrayContent(bytes); content.Headers.ContentType = new("image/png"); form.Add(content,"file","logo.png"); return await http.PutAsync(photoPath,form); }
        (await UploadPhoto(png)).EnsureSuccessStatusCode();
        var withPhoto = (await http.GetFromJsonAsync<ClientDto>($"/api/clients/{client.Id}"))!;
        Assert.NotNull(withPhoto.PhotoVersion);
        using (var photo = System.Text.Json.JsonDocument.Parse(await http.GetStringAsync(photoPath))) Assert.Equal("data:image/png;base64,"+Convert.ToBase64String(png), photo.RootElement.GetProperty("dataUrl").GetString());
        (await UploadPhoto(png)).EnsureSuccessStatusCode();
        Assert.NotEqual(withPhoto.PhotoVersion,(await http.GetFromJsonAsync<ClientDto>($"/api/clients/{client.Id}"))!.PhotoVersion);
        Assert.Equal(HttpStatusCode.BadRequest,(await UploadPhoto(System.Text.Encoding.UTF8.GetBytes("<svg></svg>"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await UploadPhoto(new byte[2*1024*1024+1])).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await http.DeleteAsync(photoPath)).StatusCode);
        Assert.Null((await http.GetFromJsonAsync<ClientDto>($"/api/clients/{client.Id}"))!.PhotoVersion);
        using (var photo = System.Text.Json.JsonDocument.Parse(await http.GetStringAsync(photoPath))) Assert.Equal(System.Text.Json.JsonValueKind.Null,photo.RootElement.GetProperty("dataUrl").ValueKind);
        var bankResponse = await http.PostAsJsonAsync("/api/banks",new BankInput("Banque fictive test",suffix[..24],"Fixture")); bankResponse.EnsureSuccessStatusCode(); var bank=(await bankResponse.Content.ReadFromJsonAsync<BankDto>())!;
        photoPath = $"/api/banks/{bank.Id}/logo";
        using (var anonymous = factory.CreateClient()) Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(photoPath)).StatusCode);
        (await UploadPhoto(png)).EnsureSuccessStatusCode();
        var withLogo = (await http.GetFromJsonAsync<BankDto>($"/api/banks/{bank.Id}"))!;
        Assert.NotNull(withLogo.LogoVersion);
        using (var photo = System.Text.Json.JsonDocument.Parse(await http.GetStringAsync(photoPath))) Assert.Equal("data:image/png;base64,"+Convert.ToBase64String(png), photo.RootElement.GetProperty("dataUrl").GetString());
        (await UploadPhoto(png)).EnsureSuccessStatusCode();
        Assert.NotEqual(withLogo.LogoVersion,(await http.GetFromJsonAsync<BankDto>($"/api/banks/{bank.Id}"))!.LogoVersion);
        Assert.Equal(HttpStatusCode.BadRequest,(await UploadPhoto(System.Text.Encoding.UTF8.GetBytes("<svg></svg>"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await UploadPhoto(new byte[2*1024*1024+1])).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await http.DeleteAsync(photoPath)).StatusCode);
        Assert.Null((await http.GetFromJsonAsync<BankDto>($"/api/banks/{bank.Id}"))!.LogoVersion);
        using (var photo = System.Text.Json.JsonDocument.Parse(await http.GetStringAsync(photoPath))) Assert.Equal(System.Text.Json.JsonValueKind.Null,photo.RootElement.GetProperty("dataUrl").ValueKind);
        var accountResponse = await http.PostAsJsonAsync("/api/bank-accounts", new BankAccountInput(client.Id,bank.Id,"TEST","Compte test","MAD","BQ","512000")); accountResponse.EnsureSuccessStatusCode(); var account=(await accountResponse.Content.ReadFromJsonAsync<BankAccountDto>())!;
        var profileResponse = await http.PostAsJsonAsync("/api/bank-statement-templates",new StatementInput(bank.Id,"Fictif","", "delimited",";","dd/MM/yyyy","fr-FR",0,0,1,2,3,4,5,true)); profileResponse.EnsureSuccessStatusCode(); var profile=(await profileResponse.Content.ReadFromJsonAsync<StatementDto>())!;
        var bankDetails = (await http.GetFromJsonAsync<BankDto>($"/api/banks/{bank.Id}"))!;
        Assert.Equal(1, bankDetails.Accounts); Assert.Equal(2, bankDetails.Profiles); Assert.Equal(2, bankDetails.ActiveProfiles); Assert.Equal(1, bankDetails.Clients);
        Assert.Single((await http.GetFromJsonAsync<PageResult<BankAccountSummary>>($"/api/banks/{bank.Id}/accounts"))!.Items);
        Assert.Equal(2, (await http.GetFromJsonAsync<PageResult<BankProfileSummary>>($"/api/banks/{bank.Id}/profiles"))!.Items.Count);
        var filtered = await http.GetFromJsonAsync<PageResult<BankDto>>($"/api/banks?search={bank.Code.ToLowerInvariant()}&status=ready&sort=code&pageSize=1");
        Assert.Single(filtered!.Items); Assert.Equal(bank.Id, filtered.Items[0].Id);
        Assert.Empty((await http.GetFromJsonAsync<PageResult<BankDto>>($"/api/banks?search={bank.Code}&status=missing"))!.Items);
        var blockedDelete = await http.DeleteAsync($"/api/banks/{bank.Id}"); Assert.Equal(HttpStatusCode.Conflict, blockedDelete.StatusCode); Assert.Contains("compte(s)", await blockedDelete.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync("/api/banks", new BankInput("Doublon", bank.Code.ToLowerInvariant(), ""))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/banks", new BankInput(" ", "CODE", ""))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/banks", new BankInput("Banque", "BAD CODE", ""))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/banks?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync("/api/banks?sort=invalid")).StatusCode);
        var temporaryResponse = await http.PostAsJsonAsync("/api/banks", new BankInput("  Banque temporaire  ", "tmp" + suffix[..20], " Notes "));
        temporaryResponse.EnsureSuccessStatusCode(); var temporary = (await temporaryResponse.Content.ReadFromJsonAsync<BankDto>())!;
        Assert.Equal("Banque temporaire", temporary.Name); Assert.Equal("Notes", temporary.Description); Assert.Equal(0, temporary.Accounts); Assert.Equal("Profil actif", temporary.Status);
        var bankUpdate = await http.PutAsJsonAsync($"/api/banks/{temporary.Id}", new BankInput("Banque renommée", temporary.Code, "Description modifiée")); bankUpdate.EnsureSuccessStatusCode();
        Assert.Equal("Banque renommée", (await bankUpdate.Content.ReadFromJsonAsync<BankDto>())!.Name);
        Assert.Equal(HttpStatusCode.NoContent, (await http.DeleteAsync($"/api/banks/{temporary.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/banks/{temporary.Id}")).StatusCode);
        var exports=(await http.GetFromJsonAsync<PageResult<ExportDto>>("/api/export-templates"))!;
        (await http.PutAsJsonAsync($"/api/banks/{bank.Id}",new BankInput("Banque fictive modifiée",bank.Code,"Fixture"))).EnsureSuccessStatusCode();
        (await http.PutAsJsonAsync($"/api/bank-accounts/{account.Id}",new BankAccountInput(client.Id,bank.Id,"TEST","Compte modifié","MAD","BQ","512000"))).EnsureSuccessStatusCode();
        (await http.PutAsJsonAsync($"/api/bank-statement-templates/{profile.Id}",new StatementInput(bank.Id,"Fictif modifié","", "delimited",";","dd/MM/yyyy","fr-FR",0,0,1,2,3,4,5,true))).EnsureSuccessStatusCode();
        var customInput=new ExportInput("Test export","CUSTOMCSV","Test","csv",";","utf-8","fr-FR",true,true,[new("Date","Date",0,"yyyy-MM-dd",true)]);
        var customResponse=await http.PostAsJsonAsync("/api/export-templates",customInput);customResponse.EnsureSuccessStatusCode();var custom=(await customResponse.Content.ReadFromJsonAsync<ExportDto>())!;
        customInput.Fields.Add(new("Libellé","Description",1,null,true));
        var customUpdate=await http.PutAsJsonAsync($"/api/export-templates/{custom.Id}",customInput);Assert.True(customUpdate.IsSuccessStatusCode,await customUpdate.Content.ReadAsStringAsync());Assert.Equal(2,(await customUpdate.Content.ReadFromJsonAsync<ExportDto>())!.Fields.Count);
        Assert.Equal(HttpStatusCode.NoContent,(await http.DeleteAsync($"/api/export-templates/{custom.Id}")).StatusCode);
        Guid conversionId=default;
        foreach(var export in exports.Items.GroupBy(x=>x.Type).Select(x=>x.First()))
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(client.Id.ToString()),"ClientId");form.Add(new StringContent(account.Id.ToString()),"BankAccountId");form.Add(new StringContent(profile.Id.ToString()),"BankStatementTemplateId");form.Add(new StringContent(export.Id.ToString()),"ExportTemplateId");
            var pdf=new ByteArrayContent(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"Fixtures","fictional-statement.pdf"))); pdf.Headers.ContentType=new("application/pdf");form.Add(pdf,"File","fixture.pdf");
            var response=await http.PostAsync("/api/conversions",form); Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());
            var result=(await response.Content.ReadFromJsonAsync<ConversionResult>())!; conversionId=result.History.Id; Assert.Equal("Completed",result.History.Status);Assert.Equal(2,result.Transactions.Count);
            var download=await http.GetAsync($"/api/conversions/{conversionId}/download");download.EnsureSuccessStatusCode();Assert.NotEmpty(await download.Content.ReadAsByteArrayAsync());
        }
        var archivePath = $"/api/client-workspace/{client.Id}/archives";
        using (var anonymous = factory.CreateClient()) Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(archivePath)).StatusCode);
        var firstArchives = await http.GetFromJsonAsync<List<System.Text.Json.JsonElement>>(archivePath);
        Assert.Equal(3,firstArchives!.Count);
        var oldestId = firstArchives.Last().GetProperty("id").GetGuid();
        List<string> oldKeys;
        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var old = await db.History.SingleAsync(x=>x.Id==oldestId);
            oldKeys = new[] {old.InputStorageKey!,old.StandardStorageKey!,old.OutputStorageKey!}.ToList();
        }
        var standardText = await http.GetStringAsync(archivePath+$"/{oldestId}/files/standard");
        Assert.DoesNotContain("Date;ValueDate;Reference;Description;Debit;Credit;Balance;Amount;Direction;Account;Journal;Analytic",standardText);
        Assert.Equal(2,standardText.Split("\r\n",StringSplitOptions.RemoveEmptyEntries).Length);
        (await http.GetAsync(archivePath+$"/{oldestId}/files/original")).EnsureSuccessStatusCode();
        (await http.GetAsync(archivePath+$"/{oldestId}/files/export")).EnsureSuccessStatusCode();
        async Task<HttpResponseMessage> ImportArchive(bool valid=true) {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(client.Id.ToString()),"ClientId");form.Add(new StringContent(account.Id.ToString()),"BankAccountId");
            form.Add(new StringContent(profile.Id.ToString()),"BankStatementTemplateId");form.Add(new StringContent(exports.Items.First().Id.ToString()),"ExportTemplateId");
            var content = new ByteArrayContent(valid ? await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"Fixtures","fictional-statement.pdf")) : System.Text.Encoding.UTF8.GetBytes("%PDF-invalid"));
            content.Headers.ContentType=new("application/pdf");form.Add(content,"File","monthly.pdf");return await http.PostAsync("/api/conversions",form);
        }
        for(var month=4;month<=6;month++){var response=await ImportArchive();response.EnsureSuccessStatusCode();conversionId=(await response.Content.ReadFromJsonAsync<ConversionResult>())!.History.Id;}
        var retained=(await http.GetFromJsonAsync<List<System.Text.Json.JsonElement>>(archivePath))!;
        Assert.Equal(5,retained.Count);Assert.DoesNotContain(retained,x=>x.GetProperty("id").GetGuid()==oldestId);
        Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync(archivePath+$"/{oldestId}/files/original")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync($"/api/client-workspace/{Guid.NewGuid()}/archives/{conversionId}/files/standard")).StatusCode);
        Assert.False((await ImportArchive(false)).IsSuccessStatusCode);
        var afterFailure=(await http.GetFromJsonAsync<List<System.Text.Json.JsonElement>>(archivePath))!;
        Assert.Equal(retained.Select(x=>x.GetProperty("id").GetGuid()),afterFailure.Select(x=>x.GetProperty("id").GetGuid()));
        var simultaneous=await Task.WhenAll(ImportArchive(),ImportArchive());
        foreach(var response in simultaneous)response.EnsureSuccessStatusCode();
        Assert.Equal(5,(await http.GetFromJsonAsync<List<System.Text.Json.JsonElement>>(archivePath))!.Count);
        var storageRoot=Environment.GetEnvironmentVariable("Storage__Root")!;
        for(var attempt=0;attempt<30&&oldKeys.Any(key=>File.Exists(Path.Combine(storageRoot,key)));attempt++)await Task.Delay(500);
        Assert.All(oldKeys,key=>Assert.False(File.Exists(Path.Combine(storageRoot,key))));
        using (var scope=factory.Services.CreateScope()) Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().History.AnyAsync(x=>x.Id==oldestId));
        Assert.Equal(HttpStatusCode.Conflict,(await http.DeleteAsync($"/api/clients/{client.Id}")).StatusCode);
        var disposableInput=clientInput with {ICE=(long.Parse(ice)+1).ToString("D15")};
        var disposable=await http.PostAsJsonAsync("/api/clients",disposableInput);disposable.EnsureSuccessStatusCode();var disposableId=(await disposable.Content.ReadFromJsonAsync<ClientDto>())!.Id;
        Assert.Equal(HttpStatusCode.NoContent,(await http.DeleteAsync($"/api/clients/{disposableId}")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync($"/api/clients/{disposableId}")).StatusCode);
        using var other=factory.CreateClient();var email=$"user-{suffix}@example.test";
        (await other.PostAsJsonAsync("/api/auth/register",new RegisterDto(suffix,email,password))).EnsureSuccessStatusCode();
        var otherLogin=await other.PostAsJsonAsync("/api/auth/login",new LoginDto(email,password));var otherSession=(await otherLogin.Content.ReadFromJsonAsync<AuthResult>())!;Assert.Equal("User",otherSession.User.Role);other.DefaultRequestHeaders.Authorization=new("Bearer",otherSession.Token);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.PostAsJsonAsync("/api/banks",new BankInput("Unauthorized","BAD",""))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.PutAsJsonAsync($"/api/banks/{bank.Id}",new BankInput("Unauthorized","BAD",""))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.DeleteAsync($"/api/banks/{bank.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.DeleteAsync($"/api/banks/{bank.Id}/logo")).StatusCode);
        using (var form = new MultipartFormDataContent()) { form.Add(new ByteArrayContent(png), "file", "logo.png"); Assert.Equal(HttpStatusCode.Forbidden,(await other.PutAsync($"/api/banks/{bank.Id}/logo",form)).StatusCode); }
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/api/conversions/{conversionId}/download")).StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<PageResult<HistoryDto>>("/api/history"))!.Items);
        Assert.Equal(5,(await other.GetFromJsonAsync<List<System.Text.Json.JsonElement>>(archivePath))!.Count);
        // Two-stage import: no profile/export required, PDF accessible before processing.
        using var stagedForm = new MultipartFormDataContent();
        stagedForm.Add(new StringContent(client.Id.ToString()),"clientId");stagedForm.Add(new StringContent(account.Id.ToString()),"bankAccountId");
        var stagedPdf = new ByteArrayContent(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"Fixtures","fictional-statement.pdf")));stagedPdf.Headers.ContentType=new("application/pdf");stagedForm.Add(stagedPdf,"file","staged.pdf");
        var importedResponse=await http.PostAsync("/api/documents",stagedForm);importedResponse.EnsureSuccessStatusCode();
        var imported=(await importedResponse.Content.ReadFromJsonAsync<HistoryDto>())!;Assert.Equal("Pending",imported.Status);
        Assert.Equal(5,(await http.GetFromJsonAsync<List<System.Text.Json.JsonElement>>(archivePath))!.Count);
        var documents=(await http.GetFromJsonAsync<List<System.Text.Json.JsonElement>>(archivePath+"?includePending=true"))!;
        Assert.Contains(documents,x=>x.GetProperty("id").GetGuid()==imported.Id&&x.GetProperty("status").GetString()=="Pending");
        (await http.GetAsync(archivePath+$"/{imported.Id}/files/original")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync(archivePath+$"/{imported.Id}/preview/standard")).StatusCode);
        string replacedKey;
        using(var scope=factory.Services.CreateScope())replacedKey=(await scope.ServiceProvider.GetRequiredService<AppDbContext>().History.SingleAsync(x=>x.Id==imported.Id)).InputStorageKey!;
        using(var editForm=new MultipartFormDataContent()){
            editForm.Add(new StringContent(client.Id.ToString()),"clientId");editForm.Add(new StringContent(account.Id.ToString()),"bankAccountId");
            var replacement=new ByteArrayContent(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,"Fixtures","fictional-statement.pdf")));replacement.Headers.ContentType=new("application/pdf");editForm.Add(replacement,"file","replacement.pdf");
            Assert.Equal(HttpStatusCode.NoContent,(await other.PutAsync($"/api/documents/{imported.Id}",editForm)).StatusCode);
        }
        using(var invalidEdit=new MultipartFormDataContent()){
            invalidEdit.Add(new StringContent(client.Id.ToString()),"clientId");invalidEdit.Add(new StringContent(Guid.NewGuid().ToString()),"bankAccountId");
            Assert.Equal(HttpStatusCode.BadRequest,(await http.PutAsync($"/api/documents/{imported.Id}",invalidEdit)).StatusCode);
        }
        using(var scope=factory.Services.CreateScope()){
            var edited=await scope.ServiceProvider.GetRequiredService<AppDbContext>().History.SingleAsync(x=>x.Id==imported.Id);
            Assert.Equal(otherSession.User.Id, edited.ModifiedByUserId); Assert.Equal(otherSession.User.Name, edited.ModifiedByName); Assert.NotNull(edited.ModifiedAt);
            Assert.Equal("replacement.pdf",edited.InputFileName);Assert.NotEqual(replacedKey,edited.InputStorageKey);Assert.Equal("Pending",edited.Status);
        }
        var badProfile=await http.PostAsJsonAsync("/api/bank-statement-templates",new StatementInput(bank.Id,"Invalid date profile","","delimited",";","yyyy-MM-dd","fr-FR",0,0,1,2,3,4,5,true));badProfile.EnsureSuccessStatusCode();var badProfileId=(await badProfile.Content.ReadFromJsonAsync<StatementDto>())!.Id;
        var failedProcessing=await http.PostAsJsonAsync($"/api/documents/{imported.Id}/process",new {profileId=badProfileId,exportId=exports.Items.First().Id});Assert.False(failedProcessing.IsSuccessStatusCode);
        (await http.GetAsync(archivePath+$"/{imported.Id}/files/original")).EnsureSuccessStatusCode();
        Assert.Equal("Failed",(await http.GetFromJsonAsync<List<System.Text.Json.JsonElement>>(archivePath+"?includePending=true"))!.Single(x=>x.GetProperty("id").GetGuid()==imported.Id).GetProperty("status").GetString());
        using(var anonymous=factory.CreateClient())Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync($"/api/documents/{imported.Id}/process",new{profileId=profile.Id,exportId=exports.Items.First().Id})).StatusCode);
        var processed=await other.PostAsJsonAsync($"/api/documents/{imported.Id}/process",new{profileId=profile.Id,exportId=exports.Items.First().Id});processed.EnsureSuccessStatusCode();var processedHistory = (await processed.Content.ReadFromJsonAsync<ConversionResult>())!.History; Assert.Equal(imported.Id, processedHistory.Id);
        Assert.Equal(otherSession.User.Name, processedHistory.ProcessedByName); Assert.NotNull(processedHistory.ProcessedAt); Assert.Equal(otherSession.User.Name, processedHistory.ModifiedByName); Assert.False(string.IsNullOrWhiteSpace(processedHistory.AccountName));
        Assert.DoesNotContain("conversion_", processedHistory.OutputFileName!);
        var attributedArchive = (await http.GetFromJsonAsync<List<System.Text.Json.JsonElement>>(archivePath))!.Single(x => x.GetProperty("id").GetGuid() == imported.Id);
        Assert.Equal(otherSession.User.Name, attributedArchive.GetProperty("processedByName").GetString()); Assert.Equal(otherSession.User.Name, attributedArchive.GetProperty("modifiedByName").GetString());
        using(var preview=System.Text.Json.JsonDocument.Parse(await http.GetStringAsync(archivePath+$"/{imported.Id}/preview/standard")))Assert.Equal(3,preview.RootElement.GetProperty("rows").GetArrayLength());
        (await http.GetAsync(archivePath+$"/{imported.Id}/preview/export")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict,(await http.PostAsJsonAsync($"/api/documents/{imported.Id}/process",new{profileId=profile.Id,exportId=exports.Items.First().Id})).StatusCode);
        Assert.Equal(5,(await http.GetFromJsonAsync<List<System.Text.Json.JsonElement>>(archivePath))!.Count);
        List<string> completedOutputKeys;
        using(var scope=factory.Services.CreateScope()){var h=await scope.ServiceProvider.GetRequiredService<AppDbContext>().History.SingleAsync(x=>x.Id==imported.Id);completedOutputKeys=new[]{h.StandardStorageKey,h.OutputStorageKey}.OfType<string>().ToList();}
        using(var completedEdit=new MultipartFormDataContent()){
            completedEdit.Add(new StringContent(client.Id.ToString()),"clientId");completedEdit.Add(new StringContent(account.Id.ToString()),"bankAccountId");
            Assert.Equal(HttpStatusCode.NoContent,(await http.PutAsync($"/api/documents/{imported.Id}",completedEdit)).StatusCode);
        }
        using(var scope=factory.Services.CreateScope()){
            var editedCompleted=await scope.ServiceProvider.GetRequiredService<AppDbContext>().History.SingleAsync(x=>x.Id==imported.Id);
            Assert.Equal("Pending",editedCompleted.Status);Assert.Null(editedCompleted.StandardStorageKey);Assert.Null(editedCompleted.OutputStorageKey);Assert.Equal(0,editedCompleted.TransactionCount);
        }
        using(var anonymous=factory.CreateClient())Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.DeleteAsync($"/api/documents/{imported.Id}")).StatusCode);
        List<string> deletedKeys;
        using(var scope=factory.Services.CreateScope()){var h=await scope.ServiceProvider.GetRequiredService<AppDbContext>().History.SingleAsync(x=>x.Id==imported.Id);deletedKeys=new[]{h.InputStorageKey!,replacedKey}.Concat(completedOutputKeys).ToList();}
        Assert.Equal(HttpStatusCode.NoContent,(await http.DeleteAsync($"/api/documents/{imported.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync(archivePath+$"/{imported.Id}/files/original")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await http.DeleteAsync($"/api/documents/{imported.Id}")).StatusCode);
        for(var attempt=0;attempt<30&&deletedKeys.Any(key=>File.Exists(Path.Combine(storageRoot,key)));attempt++)await Task.Delay(500);
        Assert.All(deletedKeys,key=>Assert.False(File.Exists(Path.Combine(storageRoot,key))));
        var deletionPath=$"/api/catalog-deletions/banks/{bank.Id}";
        using(var anonymous=factory.CreateClient()){
            Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(deletionPath)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync(deletionPath,new{version=""})).StatusCode);
        }
        var deletion=(await http.GetFromJsonAsync<DeletionPreview>(deletionPath))!;
        Assert.True(deletion.Documents>0);Assert.Equal(1,deletion.Accounts);
        Assert.Equal(HttpStatusCode.Conflict,(await http.PostAsJsonAsync(deletionPath,new{version="stale"})).StatusCode);
        (await http.GetAsync($"/api/banks/{bank.Id}")).EnsureSuccessStatusCode();
        List<string> cascadeKeys;
        using(var scope=factory.Services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var histories=await db.History.Where(h=>h.ClientId==client.Id).ToListAsync();
            cascadeKeys=histories.SelectMany(h=>new[]{h.InputStorageKey,h.StandardStorageKey,h.OutputStorageKey}).OfType<string>().ToList();
        }
        Assert.Equal(HttpStatusCode.NoContent,(await http.PostAsJsonAsync(deletionPath,new{version=deletion.Version})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync($"/api/banks/{bank.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync($"/api/bank-accounts/{account.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync($"/api/bank-statement-templates/{profile.Id}")).StatusCode);
        (await http.GetAsync($"/api/clients/{client.Id}")).EnsureSuccessStatusCode();
        for(var attempt=0;attempt<30&&cascadeKeys.Any(key=>File.Exists(Path.Combine(storageRoot,key)));attempt++)await Task.Delay(500);
        Assert.All(cascadeKeys,key=>Assert.False(File.Exists(Path.Combine(storageRoot,key))));
        // Client deletion preserves its bank and blocks while a document is processing.
        var remainingBank=new BankStatementConverter.Domain.Bank{Name="Preserved",Code="KEEP"+suffix[..20]};
        var remainingAccount=new BankStatementConverter.Domain.BankAccount{BankId=remainingBank.Id,ClientId=client.Id,AccountNumber="CASCADE"};
        var pending=new BankStatementConverter.Domain.ConversionHistory{ClientId=client.Id,BankAccountId=remainingAccount.Id,UserId=session.User.Id,Status="Processing",InputFileName="test.pdf"};
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();db.AddRange(remainingBank,remainingAccount,pending);await db.SaveChangesAsync();}
        var clientDeletionPath=$"/api/catalog-deletions/clients/{client.Id}";
        var processingPreview=(await http.GetFromJsonAsync<DeletionPreview>(clientDeletionPath))!;
        Assert.True(processingPreview.Processing);
        Assert.Equal(HttpStatusCode.Conflict,(await http.PostAsJsonAsync(clientDeletionPath,new{version=processingPreview.Version})).StatusCode);
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var h=await db.History.FindAsync(pending.Id);h!.Status="Pending";await db.SaveChangesAsync();}
        Assert.Equal(HttpStatusCode.Conflict,(await http.PostAsJsonAsync(clientDeletionPath,new{version=processingPreview.Version})).StatusCode);
        var clientPreview=(await http.GetFromJsonAsync<DeletionPreview>(clientDeletionPath))!;
        Assert.Equal(1,clientPreview.Accounts);Assert.Equal(1,clientPreview.Documents);
        Assert.Equal(HttpStatusCode.NoContent,(await http.PostAsJsonAsync(clientDeletionPath,new{version=clientPreview.Version})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync($"/api/clients/{client.Id}")).StatusCode);
        (await http.GetAsync($"/api/banks/{remainingBank.Id}")).EnsureSuccessStatusCode();
        (await http.GetAsync("/api/dashboard")).EnsureSuccessStatusCode();(await http.GetAsync("/swagger/v1/swagger.json")).EnsureSuccessStatusCode();
        var shared = Assert.Single((await http.GetFromJsonAsync<PageResult<StatementDto>>("/api/bank-statement-templates"))!.Items, p => p.BankId == null && p.ParserKey == "multibank-auto");
        Assert.Equal("Toutes les banques", shared.Bank);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await Seed.RunAsync(db, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
            Assert.Equal(1, await db.StatementTemplates.CountAsync(p => p.BankId == null));
        }
        var invalidShared = new StatementInput(null,"Invalid","","delimited",";","dd/MM/yyyy","fr-FR",0,0,1,2,3,4,5,true);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/bank-statement-templates", invalidShared)).StatusCode);
        var autoClientResponse = await http.PostAsJsonAsync("/api/clients", clientInput with { ICE = (long.Parse(ice) + 9).ToString("D15") });
        autoClientResponse.EnsureSuccessStatusCode();
        var autoClient = (await autoClientResponse.Content.ReadFromJsonAsync<ClientDto>())!;
        var fixtureRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (fixtureRoot != null && !fixtureRoot.GetDirectories("relv*").Any()) fixtureRoot = fixtureRoot.Parent;
        Assert.NotNull(fixtureRoot);
        var fixtureDirectory = fixtureRoot.GetDirectories("relv*").Single().FullName;
        var sampleNames = new[] { "releve_test_01_attijariwafa.pdf", "releve_banque_populaire_exemple_01.pdf", "releve_bmce_exemple_01.pdf", "releve_credit_agricole_exemple_01.pdf", "releve_societe_generale_exemple_01.pdf" };
        var expectedCounts = new[] { 16, 42, 36, 22, 18 };
        for (int i = 0; i < sampleNames.Length; i++)
        {
            var autoBankResponse = await http.PostAsJsonAsync("/api/banks", new BankInput("Multibank test " + i, "AUTO" + i + suffix[..12], ""));
            autoBankResponse.EnsureSuccessStatusCode();
            var autoBank = (await autoBankResponse.Content.ReadFromJsonAsync<BankDto>())!;
            var autoAccountResponse = await http.PostAsJsonAsync("/api/bank-accounts", new BankAccountInput(autoClient.Id, autoBank.Id, "AUTO", "Automatic test", "MAD", "BQ", "512000"));
            autoAccountResponse.EnsureSuccessStatusCode();
            var autoAccount = (await autoAccountResponse.Content.ReadFromJsonAsync<BankAccountDto>())!;
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(autoClient.Id.ToString()), "ClientId");
            form.Add(new StringContent(autoAccount.Id.ToString()), "BankAccountId");
            var content = new ByteArrayContent(await File.ReadAllBytesAsync(Path.Combine(fixtureDirectory, sampleNames[i])));
            content.Headers.ContentType = new("application/pdf"); form.Add(content, "File", sampleNames[i]);
            var upload = await http.PostAsync("/api/documents", form); upload.EnsureSuccessStatusCode();
            var document = (await upload.Content.ReadFromJsonAsync<HistoryDto>())!;
            var response = await http.PostAsJsonAsync($"/api/documents/{document.Id}/process", new { profileId = shared.Id, exportId = exports.Items.First().Id });
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            var converted = (await response.Content.ReadFromJsonAsync<ConversionResult>())!;
            Assert.Equal(expectedCounts[i], converted.Transactions.Count);
            (await http.GetAsync($"/api/conversions/{document.Id}/download")).EnsureSuccessStatusCode();
        }
    }
}
