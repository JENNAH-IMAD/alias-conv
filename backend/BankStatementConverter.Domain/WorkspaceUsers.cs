namespace BankStatementConverter.Domain;

public sealed class WorkspaceUser : Entity
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public string PermissionsJson { get; set; } = "[]";
    public int Version { get; set; } = 1;
    public int SessionVersion { get; set; } = 1;
    public DateTime? LastLoginAt { get; set; }
}
public sealed class WorkspaceSession : Entity
{
    public Guid UserId { get; set; }
    public WorkspaceUser User { get; set; } = null!;
    public int UserSessionVersion { get; set; }
    public DateTime ExpiresAt { get; set; }
}
