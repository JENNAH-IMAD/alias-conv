using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using BankStatementConverter.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

if(args.Length!=3)throw new ArgumentException("Expected project root, output directory and pg_dump path.");
var root=Path.GetFullPath(args[0]);var target=Path.GetFullPath(args[1]);
if(!target.StartsWith(Path.Combine(root,"output")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Snapshot output must stay inside project output.");
if(Directory.Exists(target)&&Directory.EnumerateFileSystemEntries(target).Any())throw new InvalidOperationException("Use a fresh snapshot directory.");
var api=Path.Combine(root,"backend/BankStatementConverter.API");
var config=new ConfigurationBuilder().AddJsonFile(Path.Combine(api,"appsettings.json")).AddJsonFile(Path.Combine(api,"appsettings.Development.json")).Build();
var connection=new NpgsqlConnectionStringBuilder(config.GetConnectionString("Default"));
var storage=Path.GetFullPath(config["Storage:Root"]??"uploads",api);
await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options);
var counts=new {users=await db.WorkspaceUsers.CountAsync(),clients=await db.Clients.CountAsync(),accounts=await db.BankAccounts.CountAsync(),statements=await db.StatementImports.CountAsync(),exports=await db.ExportJobs.CountAsync(),history=await db.StatementActivities.CountAsync(),exportModels=await db.ExportTemplates.CountAsync(),importModels=await db.ExtractionProfiles.CountAsync()};
Directory.CreateDirectory(target);var dump=Path.Combine(target,"database.dump");
var process=new ProcessStartInfo(Path.GetFullPath(args[2])){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
foreach(var a in new[]{"--host",connection.Host!,"--port",connection.Port.ToString(),"--username",connection.Username!,"--dbname",connection.Database!,"--format=custom","--no-owner","--no-acl","--exclude-table-data=public.\"WorkspaceSessions\"","--exclude-table-data=public.\"PendingFileDeletions\"","--file",dump})process.ArgumentList.Add(a);
process.Environment["PGPASSWORD"]=connection.Password!;
using(var child=Process.Start(process)!){await child.StandardError.ReadToEndAsync();await child.WaitForExitAsync();if(child.ExitCode!=0)throw new InvalidOperationException("Database export failed; original data unchanged.");}
var uploads=Path.Combine(target,"uploads");Directory.CreateDirectory(uploads);
var files=new List<object>();
foreach(var source in Directory.EnumerateFiles(storage,"*",SearchOption.AllDirectories))
{
    var relative=Path.GetRelativePath(storage,source);
    if(relative.Split(Path.DirectorySeparatorChar).Any(p=>p.StartsWith('.')) || (File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)continue;
    var destination=Path.GetFullPath(Path.Combine(uploads,relative));
    if(!destination.StartsWith(uploads+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Invalid file location.");
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(source,destination,false);
    using var stream=File.OpenRead(destination);files.Add(new {path=relative.Replace('\\','/'),sha256=Convert.ToHexString(SHA256.HashData(stream))});
}
using(var dumpStream=File.OpenRead(dump))File.WriteAllText(Path.Combine(target,"manifest.json"),JsonSerializer.Serialize(new{version=1,createdUtc=DateTime.UtcNow,counts,databaseSha256=Convert.ToHexString(SHA256.HashData(dumpStream)),files},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(new{counts,files=files.Count,credentialsTransferred=false,sessionsTransferred=false}));
