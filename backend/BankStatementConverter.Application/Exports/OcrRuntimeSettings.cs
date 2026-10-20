namespace BankStatementConverter.Application.Exports;

public sealed record OcrRuntimeSettings(string Provider, string CloudModel, string? CloudToken, string BaseUrl,
    string OpenRouterModel = "openrouter/free", string? OpenRouterToken = null, string AutoPaddleProvider = "LOCAL");
