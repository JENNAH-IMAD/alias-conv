using BankStatementConverter.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
namespace BankStatementConverter.Infrastructure;

public class LocalFileStorage(IConfiguration config, IHostEnvironment environment) : IFileStorageService
{
    private readonly string root = Path.GetFullPath(config["Storage:Root"] ?? "uploads", environment.ContentRootPath);
    private string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new AppException(400, "Chemin de fichier invalide.");
        return path;
    }
    public async Task<string> SaveAsync(Stream stream, string category, string extension, CancellationToken ct)
    {
        Validation.Require((category == "input" && extension == "pdf") || (category == "exports" && extension is "csv" or "txt"), "Type de stockage invalide.");
        var key = $"{category}/{Guid.NewGuid():N}.{extension}";
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await stream.CopyToAsync(file, ct);
        }
        catch { File.Delete(path); throw; }
        return key;
    }
    public Stream Open(string key)
    {
        try
        {
            return File.OpenRead(Resolve(key));
        }
        catch (FileNotFoundException) { throw new AppException(404, "Le fichier est absent du stockage."); }
        catch (DirectoryNotFoundException) { throw new AppException(404, "Le fichier est absent du stockage."); }
    }
    public void Delete(string key)
    {
        var path = Resolve(key);
        try
        {
            File.Delete(path);
        }
        catch (DirectoryNotFoundException) { /* An already absent file is successfully removed. */ }
    }
}
