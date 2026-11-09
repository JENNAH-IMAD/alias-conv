using System.Text.Json;
using BankStatementConverter.Application;
using Validation = BankStatementConverter.Infrastructure.Validation;
using BankStatementConverter.Application.Exports;
using Microsoft.AspNetCore.DataProtection;

namespace BankStatementConverter.API;

public sealed class OcrSettingsStore
{
    private readonly object gate = new();
    private readonly IDataProtector protector;
    private readonly string path;
    private OcrRuntimeSettings current;
    public OcrSettingsStore(IConfiguration config, IWebHostEnvironment environment, IDataProtectionProvider protection)
    {
        protector = protection.CreateProtector("Alias.OcrSettings.v1");
        var root = Path.GetFullPath(config["Storage:Root"] ?? "uploads", environment.ContentRootPath);
        path = Path.Combine(root, ".settings", "ocr.protected");
        current = new(config["Ocr:Provider"] ?? "LOCAL", config["Ocr:CloudModel"] ?? "PP-OCRv6", config["Ocr:CloudToken"], config["Ocr:BaseUrl"] ?? "http://127.0.0.1:8091",
            config["Hybrid:AutoPaddleProvider"] ?? "LOCAL");
        string? saved = null;
        if (File.Exists(path))
        {
            saved = protector.Unprotect(File.ReadAllText(path));
            current = JsonSerializer.Deserialize<OcrRuntimeSettings>(saved) ?? throw new InvalidOperationException("Configuration OCR illisible.");
        }
        // Migrate retired providers and discard their fields and secrets from the protected settings.
        current = current with
        {
            Provider = IsSupportedProvider(current.Provider) ? current.Provider : "AUTO",
            AutoPaddleProvider = current.AutoPaddleProvider == "AI_STUDIO" && !string.IsNullOrWhiteSpace(current.CloudToken)
                ? "AI_STUDIO" : "LOCAL"
        };
        if (saved != null && saved != JsonSerializer.Serialize(current))
            Write(current);
    }
    public OcrRuntimeSettings Snapshot
    {
        get
        {
            lock (gate)
                return current;
        }
    }
    public object PublicSettings
    {
        get
        {
            var s = Snapshot;
            return new
            {
                s.Provider,
                s.CloudModel,
                s.BaseUrl,
                HasToken = !string.IsNullOrWhiteSpace(s.CloudToken),
                ApiUrl = "https://paddleocr.aistudio-app.com/api/v2/ocr/jobs",
                s.AutoPaddleProvider
            };
        }
    }
    public void Save(OcrSettingsInput input)
    {
        lock (gate)
        {
            Validation.Require(IsSupportedProvider(input.Provider), "Choisissez le mode automatique, API PaddleOCR ou OCR local.");
            var autoPaddle = input.AutoPaddleProvider ?? current.AutoPaddleProvider;
            Validation.Require(autoPaddle is "LOCAL" or "AI_STUDIO", "Moteur Paddle du mode automatique invalide.");
            Validation.Require(input.CloudModel is "PP-OCRv6" or "PaddleOCR-VL-1.6", "Modèle PaddleOCR invalide.");
            var token = input.CloudToken is null ? current.CloudToken : input.CloudToken.Trim();
            Validation.Require(token is null || token.Length <= 4096 && !token.Any(char.IsWhiteSpace) && !token.Any(char.IsControl), "Jeton invalide.");
            Validation.Require(!(input.Provider == "AI_STUDIO" || input.Provider == "AUTO" && autoPaddle == "AI_STUDIO") || !string.IsNullOrWhiteSpace(token), "Saisissez votre jeton API PaddleOCR.");
            var baseUrl = input.BaseUrl?.Trim() ?? current.BaseUrl;
            Validation.Require(baseUrl.Length <= 2000 && Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0, "Adresse du moteur local invalide.");
            var next = new OcrRuntimeSettings(input.Provider, input.CloudModel, token, baseUrl.TrimEnd('/'), autoPaddle);
            Write(next);
            current = next;
        }
    }

    private static bool IsSupportedProvider(string provider) => provider is "AUTO" or "AI_STUDIO" or "LOCAL";

    private void Write(OcrRuntimeSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, protector.Protect(JsonSerializer.Serialize(settings)));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
public sealed record OcrSettingsInput(string Provider, string CloudModel, string? CloudToken = null, string? BaseUrl = null,
    string? AutoPaddleProvider = null);
