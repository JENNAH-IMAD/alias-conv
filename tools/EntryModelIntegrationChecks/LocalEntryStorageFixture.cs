using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

sealed class LocalEntryStorageFixture : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "alias-entry-storage-" + Guid.NewGuid().ToString("N"));
    public LocalFileStorage Storage { get; }
    public LocalEntryStorageFixture()
    {
        Storage = new LocalFileStorage(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Root"] = root }).Build(), new Environment { ContentRootPath = root });
    }
    public static async Task Verify()
    {
        using var fixture = new LocalEntryStorageFixture();
        foreach (var extension in new[] { "txt", "csv", "tsv" })
        {
            var bytes = Encoding.UTF8.GetBytes("N° compte;Intitulé;Libellé;Banque;Montant HT\r\n512000;CDM;CHEQUE;Banque;100,00\r\n");
            var key = await fixture.Storage.SaveAsync(new MemoryStream(bytes), "entry-exports", extension, default);
            using var stream = fixture.Storage.Open(key); using var result = new MemoryStream(); await stream.CopyToAsync(result);
            if (!result.ToArray().SequenceEqual(bytes) || !key.StartsWith("entry-exports/")) throw new Exception("LocalEntryStorage_RoundTrip_" + extension);
            Console.WriteLine("PASS LocalEntryStorage_RoundTrip_" + extension);
        }
        foreach (var pair in new[] { ("entry-exports", "exe"), ("../outside", "txt"), ("exports", "exe") })
        {
            try { await fixture.Storage.SaveAsync(new MemoryStream([1]), pair.Item1, pair.Item2, default); }
            catch (AppException e) when (e.Status == 400) { continue; }
            throw new Exception("LocalEntryStorage_RejectInvalidCategoryAndExtension");
        }
        Console.WriteLine("PASS LocalEntryStorage_RejectInvalidCategoryAndExtension");
    }
    public void Dispose()
    {
        var path = Path.GetFullPath(root);
        var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        if (Path.GetDirectoryName(path) != parent || !Path.GetFileName(path).StartsWith("alias-entry-storage-", StringComparison.Ordinal)) throw new Exception("Unsafe test storage cleanup");
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }
    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "EntryStorageChecks";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
