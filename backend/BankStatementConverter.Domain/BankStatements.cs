using System.Text.Json.Serialization;
namespace BankStatementConverter.Domain;

// Input from an extraction/normalization system. No PDF or OCR dependencies.
public sealed record BankStatement
{
    public string ConversionType { get; init; } = "STATEMENT";
    public Guid Id { get; init; } = Guid.NewGuid();
    public string BankName { get; init; } = "";
    public string? BankCode
    {
        get; init;
    }
    public string? AccountNumber
    {
        get; init;
    }
    public string? AccountHolder
    {
        get; init;
    }
    public string? Currency
    {
        get; init;
    }
    public string? StatementNumber
    {
        get; init;
    }
    public DateOnly? PeriodStart
    {
        get; init;
    }
    public DateOnly? PeriodEnd
    {
        get; init;
    }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public decimal? OpeningBalance
    {
        get; init;
    }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public decimal? ClosingBalance
    {
        get; init;
    }
    public DateOnly? OpeningBalanceDate
    {
        get; init;
    }
    public DateOnly? OpeningBalanceValueDate
    {
        get; init;
    }
    public string? OpeningBalanceDescription
    {
        get; init;
    }
    public DateOnly? ClosingBalanceDate
    {
        get; init;
    }
    public DateOnly? ClosingBalanceValueDate
    {
        get; init;
    }
    public string? ClosingBalanceDescription
    {
        get; init;
    }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public decimal? TotalDebit
    {
        get; init;
    }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public decimal? TotalCredit
    {
        get; init;
    }
    // Explicit convention only: CREDIT_INCREASES or DEBIT_INCREASES.
    public string? BalanceConvention
    {
        get; init;
    }
    public string? Journal
    {
        get; init;
    }
    public string? Account
    {
        get; init;
    }
    public string? Analytic
    {
        get; init;
    }
    public string? RecordType
    {
        get; init;
    }
    public IReadOnlyList<StatementTransaction> Transactions { get; init; } = [];
    public IReadOnlyList<StatementExtract> Extracts { get; init; } = [];
    [JsonIgnore]
    public bool IsValidated
    {
        get; init;
    }
}

public sealed record StatementExtract(Guid Id, string FilterText, DateTime CreatedAt, int SourceVersion,
    string? Currency, IReadOnlyList<StatementTransaction> Transactions,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal TotalDebit,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal TotalCredit,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] decimal Balance,
    string ExcludeText = "");

public sealed record StatementTransaction
{
    public IReadOnlyList<int>? SourcePages
    {
        get; init;
    }
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Sequence
    {
        get; init;
    }
    public DateOnly? Date
    {
        get; init;
    }
    public DateOnly? ValueDate
    {
        get; init;
    }
    public string? Description
    {
        get; init;
    }
    public string? Reference
    {
        get; init;
    }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public decimal? Debit
    {
        get; init;
    }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public decimal? Credit
    {
        get; init;
    }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public decimal? Amount
    {
        get; init;
    }
    public string? Direction
    {
        get; init;
    }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public decimal? Balance
    {
        get; init;
    }
    public string? Currency
    {
        get; init;
    }
    public string? Journal
    {
        get; init;
    }
    public string? Account
    {
        get; init;
    }
    public string? Analytic
    {
        get; init;
    }
    public string? RecordType
    {
        get; init;
    }
}

// Separate persistence envelope, so exporting never mutates normalized data.
public class StatementImport : Entity
{
    public Guid? ClientId
    {
        get; set;
    }
    public Guid? BankAccountId
    {
        get; set;
    }
    public DateTime? FiledAt
    {
        get; set;
    }
    public string? ImportedByName
    {
        get; set;
    }
    public string? ModifiedByName
    {
        get; set;
    }
    public string BankName { get; set; } = "";
    public string? StatementNumber
    {
        get; set;
    }
    public string? Currency
    {
        get; set;
    }
    public DateOnly? PeriodStart
    {
        get; set;
    }
    public DateOnly? PeriodEnd
    {
        get; set;
    }
    public string Status { get; set; } = "NEEDS_REVIEW";
    public int Version { get; set; } = 1;
    public int TransactionCount
    {
        get; set;
    }
    public string NormalizedJson { get; set; } = "{}";
    public DateTime? ValidatedAt
    {
        get; set;
    }
    public string? SourceFileName
    {
        get; set;
    }
    public string? SourceStorageKey
    {
        get; set;
    }
    public string? ImportDiagnosticsJson
    {
        get; set;
    }
    public string? RawExtractionJson
    {
        get; set;
    }
}

public class ExportJob : Entity
{
    public string? StorageKey
    {
        get; set;
    }
    public string? StandardStorageKey
    {
        get; set;
    }
    public string? GeneratedByName
    {
        get; set;
    }
    public Guid StatementId
    {
        get; set;
    }
    public Guid TemplateId
    {
        get; set;
    }
    public int StatementVersion
    {
        get; set;
    }
    public string TemplateNameSnapshot { get; set; } = "";
    public string TemplateSnapshotJson { get; set; } = "{}";
    public int RowCount
    {
        get; set;
    }
    public string Extension { get; set; } = "";
    public string Encoding { get; set; } = "";
    public string Delimiter { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentSha256 { get; set; } = "";
    public string Status { get; set; } = "SUCCEEDED";
}

// No foreign keys: traceability survives permanent deletion of the source.
public class StatementActivity : Entity
{
    public string? ChangesJson
    {
        get; set;
    }
    public Guid StatementId
    {
        get; set;
    }
    public Guid? ClientId
    {
        get; set;
    }
    public Guid? BankAccountId
    {
        get; set;
    }
    public string ClientName { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string BankName { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Action { get; set; } = "";
    public string ActorName { get; set; } = "";
    public string? ActorId
    {
        get; set;
    }
    public int Version
    {
        get; set;
    }
}
