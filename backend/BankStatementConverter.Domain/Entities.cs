namespace BankStatementConverter.Domain;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public class User : Entity
{
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "User";
    // Null preserves the historical role defaults; an empty array grants no access.
    public string[]? Permissions { get; set; }
}
public class Client : Entity
{
    public byte[]? Photo { get; set; }
    public string? PhotoContentType { get; set; }
    public Guid? PhotoVersion { get; set; }
    public string Name { get; set; } = "";
    public string LegalName { get; set; } = "";
    public string ICE { get; set; } = "";
    public string IF { get; set; } = "";
    public string RC { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Country { get; set; } = "Maroc";
    public ICollection<BankAccount> Accounts { get; set; } = [];
}
public class Bank : Entity
{
    public byte[]? Logo { get; set; }
    public string? LogoContentType { get; set; }
    public Guid? LogoVersion { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public ICollection<BankAccount> Accounts { get; set; } = [];
}
public class BankAccount : Entity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public Guid BankId { get; set; }
    public Bank Bank { get; set; } = null!;
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string Currency { get; set; } = "MAD";
    public string Journal { get; set; } = "BQ";
    public string AccountCode { get; set; } = "512000";
}
public class BankStatementTemplate : Entity
{
    public Guid? BankId { get; set; }
    public Bank? Bank { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ParserKey { get; set; } = "delimited";
    public string Delimiter { get; set; } = ";";
    public string DateFormat { get; set; } = "dd/MM/yyyy";
    public string NumberCulture { get; set; } = "fr-FR";
    public int SkipLines { get; set; }
    public int DateColumn { get; set; }
    public int DescriptionColumn { get; set; } = 1;
    public int DebitColumn { get; set; } = 2;
    public int CreditColumn { get; set; } = 3;
    public int? BalanceColumn { get; set; } = 4;
    public int? ReferenceColumn { get; set; } = 5;
    public bool IsActive { get; set; } = true;
}
public class ExportTemplate : Entity
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "CUSTOMCSV";
    public string Description { get; set; } = "";
    public string FileExtension { get; set; } = "csv";
    public string Delimiter { get; set; } = ";";
    public string Encoding { get; set; } = "utf-8";
    public string NumberCulture { get; set; } = "fr-FR";
    public bool IncludeHeader { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public ICollection<ExportTemplateField> Fields { get; set; } = [];
}
public class ExportTemplateField : Entity
{
    public Guid ExportTemplateId { get; set; }
    public string FieldName { get; set; } = "";
    public string SourceField { get; set; } = "";
    public int Position { get; set; }
    public string? Format { get; set; }
    public bool Required { get; set; }
}
public class BankTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public DateOnly? ValueDate { get; set; }
    public string Account { get; set; } = "";
    public string Journal { get; set; } = "";
    public string Description { get; set; } = "";
    public string Reference { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Amount => Credit - Debit;
    public decimal? Balance { get; set; }
    public string Direction => Amount < 0 ? "D" : "C";
    public string Analytic { get; set; } = "";
}
public class ConversionHistory : Entity
{
    public Guid? ProcessedByUserId { get; set; }
    public string? ProcessedByName { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? ModifiedByUserId { get; set; }
    public string? ModifiedByName { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public Guid BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;
    public Guid? BankStatementTemplateId { get; set; }
    public BankStatementTemplate BankStatementTemplate { get; set; } = null!;
    public Guid? ExportTemplateId { get; set; }
    public ExportTemplate ExportTemplate { get; set; } = null!;
    public string InputFileName { get; set; } = "";
    public string? InputStorageKey { get; set; }
    public string? OutputStorageKey { get; set; }
    public string? StandardStorageKey { get; set; }
    public string? ExportLabel { get; set; }
    public string? OutputDelimiter { get; set; }
    public string? OutputEncoding { get; set; }
    public string? OutputFileName { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }
    public int TransactionCount { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class PendingFileDeletion : Entity { public string StorageKey { get; set; } = ""; }
