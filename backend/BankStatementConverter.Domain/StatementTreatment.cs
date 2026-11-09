namespace BankStatementConverter.Domain;

// Last successful preview per document/saved extract. Export archives remain immutable.
public sealed class StatementTreatment : Entity
{
    public Guid StatementId { get; set; }
    public Guid ScopeId { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public DateTime RequestedAt { get; set; }
}
