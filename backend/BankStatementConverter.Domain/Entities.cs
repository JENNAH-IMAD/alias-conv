namespace BankStatementConverter.Domain;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public class Client : Entity
{
    public byte[]? Photo
    {
        get; set;
    }
    public string? PhotoContentType
    {
        get; set;
    }
    public Guid? PhotoVersion
    {
        get; set;
    }
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
    public byte[]? Logo
    {
        get; set;
    }
    public string? LogoContentType
    {
        get; set;
    }
    public Guid? LogoVersion
    {
        get; set;
    }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public ICollection<BankAccount> Accounts { get; set; } = [];
}
public class BankAccount : Entity
{
    public Guid ClientId
    {
        get; set;
    }
    public Client Client { get; set; } = null!;
    public Guid BankId
    {
        get; set;
    }
    public Bank Bank { get; set; } = null!;
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string Currency { get; set; } = "MAD";
    public string Journal { get; set; } = "BQ";
    public string AccountCode { get; set; } = "512000";
}
public class Document : Entity
{
    public Guid ClientId
    {
        get; set;
    }
    public Client Client { get; set; } = null!;
    public Guid BankAccountId
    {
        get; set;
    }
    public BankAccount BankAccount { get; set; } = null!;
    public string FileName { get; set; } = "";
    public string StorageKey { get; set; } = "";
}
public class PendingFileDeletion : Entity
{
    public string StorageKey { get; set; } = "";
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
    public Guid ExportTemplateId
    {
        get; set;
    }
    public string FieldName { get; set; } = "";
    public string SourceField { get; set; } = "";
    public int Position
    {
        get; set;
    }
    public string? Format
    {
        get; set;
    }
    public bool Required
    {
        get; set;
    }
}

public class ExtractionProfile : Entity
{
    public string Name { get; set; } = "";
    public string BankCode { get; set; } = "";
    public string Currency { get; set; } = "";
    public string MappingJson { get; set; } = "{}";
    public string SampleJson { get; set; } = "{}";
    public string AliasesJson { get; set; } = "{}";
    public string CreatedByName { get; set; } = "";
    public string ModifiedByName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
}
public class ExtractionProfileActivity : Entity
{
    public Guid ProfileId
    {
        get; set;
    }
    public string ProfileName { get; set; } = "";
    public string Action { get; set; } = "";
    public string ActorName { get; set; } = "";
    public int Version
    {
        get; set;
    }
    public string SnapshotJson { get; set; } = "{}";
}
