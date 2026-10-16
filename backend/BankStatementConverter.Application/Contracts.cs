using BankStatementConverter.Domain;
namespace BankStatementConverter.Application;

public class AppException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
public record PageResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public record ClientDto(Guid Id, string Name, string LegalName, string ICE, string IF, string RC, string Email, string Phone, string Country, string[] Banks, string Status = "Actif", Guid? PhotoVersion = null);
public record ClientInput(string Name, string LegalName, string ICE, string IF, string RC, string Email, string Phone, string Country = "Maroc");
public record BankDto(Guid Id, string Name, string Code, string Description, int Clients, string Status = "OK", int Accounts = 0, Guid? LogoVersion = null);
public record BankAccountSummary(Guid Id, string Client, string AccountName, string AccountNumber, string Currency);
public record BankInput(string Name, string Code, string Description);
public record BankAccountDto(Guid Id, Guid ClientId, Guid BankId, string Bank, string AccountNumber, string AccountName, string Currency, string Journal, string AccountCode);
public record BankAccountInput(Guid ClientId, Guid BankId, string AccountNumber, string AccountName, string Currency, string Journal, string AccountCode);
public record DocumentDto(Guid Id, Guid ClientId, Guid BankAccountId, string Client, string Bank, string AccountName, string AccountNumber, string FileName, DateTime CreatedAt);
public record ExportFieldDto(string FieldName, string SourceField, int Position, string? Format, bool Required);
public record ExportInput(string Name, string Type, string Description, string FileExtension, string Delimiter, string Encoding, string NumberCulture, bool IncludeHeader, bool IsActive, List<ExportFieldDto> Fields);
public record ExportDto(Guid Id, string Name, string Type, string Description, string FileExtension, string Delimiter, string Encoding, string NumberCulture, bool IncludeHeader, bool IsActive, List<ExportFieldDto> Fields);
public interface IFileStorageService
{
    Task<string> SaveAsync(Stream stream, string category, string extension, CancellationToken ct);
    Stream Open(string key);
    void Delete(string key);
}
