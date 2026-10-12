namespace BankStatementConverter.Application.Exports;

public sealed record OcrRuntimeSettings(string Provider, string CloudModel, string? CloudToken, string BaseUrl);
