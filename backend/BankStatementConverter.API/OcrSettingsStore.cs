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
            config["OpenRouter:Model"] ?? "openrouter/free", config["OpenRouter:ApiKey"], config["Hybrid:AutoPaddleProvider"] ?? "LOCAL");
        if (File.Exists(path))
            current = JsonSerializer.Deserialize<OcrRuntimeSettings>(protector.Unprotect(File.ReadAllText(path))) ?? throw new InvalidOperationException("Configuration OCR illisible.");
        // Older local settings may still name a paid model; this deployment only permits free inference.
        if (current.OpenRouterModel != "openrouter/free" && !current.OpenRouterModel.EndsWith(":free", StringComparison.Ordinal))
            current = current with
            {
                OpenRouterModel = "openrouter/free"
            };
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
                s.OpenRouterModel,
                HasOpenRouterToken = !string.IsNullOrWhiteSpace(s.OpenRouterToken),
                s.AutoPaddleProvider
            };
        }
    }
    public void Save(OcrSettingsInput input)
    {
        lock (gate)
        {
            Validation.Require(input.Provider is "AUTO" or "AI_STUDIO" or "LOCAL" or "OPENROUTER", "Choisissez le mode automatique, OpenRouter, API PaddleOCR ou OCR local.");
            var autoPaddle = input.AutoPaddleProvider ?? current.AutoPaddleProvider;
            Validation.Require(autoPaddle is "LOCAL" or "AI_STUDIO", "Moteur Paddle du mode automatique invalide.");
            Validation.Require(input.CloudModel is "PP-OCRv6" or "PaddleOCR-VL-1.6", "Modèle PaddleOCR invalide.");
            var token = input.CloudToken is null ? current.CloudToken : input.CloudToken.Trim();
            Validation.Require(token is null || token.Length <= 4096 && !token.Any(char.IsWhiteSpace) && !token.Any(char.IsControl), "Jeton invalide.");
            Validation.Require(!(input.Provider == "AI_STUDIO" || input.Provider == "AUTO" && autoPaddle == "AI_STUDIO") || !string.IsNullOrWhiteSpace(token), "Saisissez votre jeton API PaddleOCR.");
            // Each provider has its own secret; switching must never forward a PaddleOCR token to OpenRouter.
            var openRouterToken = input.OpenRouterToken is null ? current.OpenRouterToken : input.OpenRouterToken.Trim();
            Validation.Require(openRouterToken is null || openRouterToken.Length <= 4096 && !openRouterToken.Any(char.IsWhiteSpace) && !openRouterToken.Any(char.IsControl), "Clé OpenRouter invalide.");
            Validation.Require(input.Provider != "OPENROUTER" || !string.IsNullOrWhiteSpace(openRouterToken), "Saisissez votre clé API OpenRouter.");
            var openRouterModel = input.OpenRouterModel?.Trim() ?? current.OpenRouterModel;
            Validation.Require(openRouterModel.Length is > 0 and <= 200 && openRouterModel.Contains('/') && openRouterModel.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_' or '.' or ':'), "Identifiant de modèle OpenRouter invalide.");
            Validation.Require(openRouterModel == "openrouter/free" || openRouterModel.EndsWith(":free", StringComparison.Ordinal), "Choisissez uniquement openrouter/free ou un modèle se terminant par :free.");
            var baseUrl = input.BaseUrl?.Trim() ?? current.BaseUrl;
            Validation.Require(baseUrl.Length <= 2000 && Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0, "Adresse du moteur local invalide.");
            var next = new OcrRuntimeSettings(input.Provider, input.CloudModel, token, baseUrl.TrimEnd('/'), openRouterModel, openRouterToken, autoPaddle);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, protector.Protect(JsonSerializer.Serialize(next)));
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.Move(temporary, path, true);
                current = next;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
public sealed record OcrSettingsInput(string Provider, string CloudModel, string? CloudToken = null, string? BaseUrl = null,
    string? OpenRouterModel = null, string? OpenRouterToken = null, string? AutoPaddleProvider = null);
