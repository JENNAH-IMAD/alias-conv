using BankStatementConverter.Domain;
namespace BankStatementConverter.Application;

public class AppException(int status, string message) : Exception(message) { public int Status { get; } = status; }
public record PageResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public record UserDto(Guid Id, string Name, string Email, string Role, string[]? Permissions = null);
public record AuthResult(string Token, DateTime ExpiresAt, UserDto User);
public record LoginDto(string Email, string Password);
public record RegisterDto(string Username, string Email, string Password);
public record ClientDto(Guid Id, string Name, string LegalName, string ICE, string IF, string RC, string Email, string Phone, string Country, string[] Banks, string Status = "Actif", Guid? PhotoVersion = null);
public record ClientInput(string Name, string LegalName, string ICE, string IF, string RC, string Email, string Phone, string Country = "Maroc");
public record BankDto(Guid Id, string Name, string Code, string Description, int Clients, string Status = "OK", int Accounts = 0, int Profiles = 0, int ActiveProfiles = 0, Guid? LogoVersion = null);
public record BankAccountSummary(Guid Id, string Client, string AccountName, string AccountNumber, string Currency);
public record BankProfileSummary(Guid Id, string Name, string ParserKey, bool IsActive);
public record BankInput(string Name, string Code, string Description);
public record BankAccountDto(Guid Id, Guid ClientId, Guid BankId, string Bank, string AccountNumber, string AccountName, string Currency, string Journal, string AccountCode);
public record BankAccountInput(Guid ClientId, Guid BankId, string AccountNumber, string AccountName, string Currency, string Journal, string AccountCode);
public record StatementInput(Guid? BankId, string Name, string Description, string ParserKey, string Delimiter, string DateFormat, string NumberCulture, int SkipLines, int DateColumn, int DescriptionColumn, int DebitColumn, int CreditColumn, int? BalanceColumn, int? ReferenceColumn, bool IsActive);
public record StatementDto(Guid Id, Guid? BankId, string Bank, string Name, string Description, string ParserKey, string Delimiter, string DateFormat, string NumberCulture, int SkipLines, int DateColumn, int DescriptionColumn, int DebitColumn, int CreditColumn, int? BalanceColumn, int? ReferenceColumn, bool IsActive);
public record ExportFieldDto(string FieldName, string SourceField, int Position, string? Format, bool Required);
public record ExportInput(string Name, string Type, string Description, string FileExtension, string Delimiter, string Encoding, string NumberCulture, bool IncludeHeader, bool IsActive, List<ExportFieldDto> Fields);
public record ExportDto(Guid Id, string Name, string Type, string Description, string FileExtension, string Delimiter, string Encoding, string NumberCulture, bool IncludeHeader, bool IsActive, List<ExportFieldDto> Fields);
public record HistoryDto(Guid Id, Guid ClientId, Guid BankAccountId, string Client, string Bank, string InputFileName, string? OutputFileName, string Status, string? ErrorMessage, int Count, DateTime CreatedAt, DateTime? CompletedAt, string? AccountName = null, string? AccountNumber = null, string? ProcessedByName = null, DateTime? ProcessedAt = null, string? ModifiedByName = null, DateTime? ModifiedAt = null);
public record ConversionResult(HistoryDto History, IReadOnlyList<BankTransaction> Transactions);
public record ConversionInput(Guid ClientId, Guid BankAccountId, Guid BankStatementTemplateId, Guid ExportTemplateId, string FileName, string ContentType, long Length, Stream File);
public interface IPdfExtractionService { Task<string> ExtractAsync(Stream stream, CancellationToken ct); }
public interface IOcrService { Task<string> ExtractAsync(Stream stream, CancellationToken ct); }
public interface IBankStatementParser { string Key { get; } IReadOnlyList<BankTransaction> Parse(string text, BankStatementTemplate template); }
public interface ITransactionExporter { string Type { get; } byte[] Export(IEnumerable<BankTransaction> transactions, ExportTemplate template); }
public interface IFileStorageService
{
    Task<string> SaveAsync(Stream stream, string category, string extension, CancellationToken ct);
    Stream Open(string key);
    void Delete(string key);
}
public interface IAuthenticationService
{
    Task<AuthResult> LoginAsync(LoginDto input, CancellationToken ct);
    Task<UserDto> RegisterAsync(RegisterDto input, CancellationToken ct);
}
public interface IConversionService
{
    Task<HistoryDto> ImportAsync(ConversionInput input, Guid userId, CancellationToken ct);
    Task<ConversionResult> ProcessAsync(Guid id, Guid profileId, Guid exportId, Guid actorId, CancellationToken ct);
    Task<ConversionResult> ConvertAsync(ConversionInput input, Guid userId, CancellationToken ct);
    Task<(Stream Content, string Name)> DownloadAsync(Guid id, Guid userId, bool admin, CancellationToken ct);
}
