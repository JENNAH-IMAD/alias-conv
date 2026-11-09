namespace BankStatementConverter.Domain;

public sealed class ClientEntryModelSet : Entity
{
    public Guid ClientId { get; set; }
    public string ModelsJson { get; set; } = "[]";
    public int Version { get; set; } = 1;
}
