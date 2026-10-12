namespace BankStatementConverter.Infrastructure;

// Stable attribution for the shared workspace, independent of database users.
public static class WorkspaceIdentity
{
    public static readonly Guid Id = Guid.Parse("a11a5000-0000-4000-8000-000000000001");
    public const string Name = "Alias Convert PDF";
}
