using System.Net.Mail;
using System.Text.Json;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankStatementConverter.Infrastructure;

public sealed class WorkspaceUserService(AppDbContext db)
{
    private static readonly PasswordHasher<WorkspaceUser> Hasher = new();
    // A fixed dummy hash makes unknown accounts follow the same expensive verification path.
    private static readonly string DummyHash = Hasher.HashPassword(new(), Guid.NewGuid().ToString("N"));
    public static UserDto Dto(WorkspaceUser u) => new(u.Id, u.Name, u.Email, u.Role, u.IsActive,
        u.Role == "Admin" ? Permissions.All : JsonSerializer.Deserialize<string[]>(u.PermissionsJson)!, u.CreatedAt, u.LastLoginAt, u.Version);
    public async Task<bool> NeedsSetup(CancellationToken ct) => !await db.WorkspaceUsers.AnyAsync(ct);
    private static string Email(string value)
    {
        value = value.Trim().ToLowerInvariant();
        if (value.Length > 254 || !MailAddress.TryCreate(value, out var email) || email.Address != value)
            throw new AppException(400, "Adresse e-mail invalide.");
        return value;
    }
    private static void Password(string value)
    {
        if (value.Length is < 12 or > 128)
            throw new AppException(400, "Utilisez un mot de passe de 12 à 128 caractères.");
    }
    private static void Assign(WorkspaceUser user, UserInput input)
    {
        Validation.Text(input.Name.Trim(), "Nom", 150);
        if (input.Role is not ("Admin" or "User"))
            throw new AppException(400, "Rôle invalide.");
        user.Name = input.Name.Trim();
        user.Email = Email(input.Email);
        user.Role = input.Role;
        user.IsActive = input.IsActive;
        user.PermissionsJson = JsonSerializer.Serialize(Permissions.Validate(input.Permissions));
        if (!string.IsNullOrEmpty(input.Password))
        {
            Password(input.Password);
            user.PasswordHash = Hasher.HashPassword(user, input.Password);
        }
    }
    public async Task<WorkspaceUser> Setup(SetupInput input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(867530901)", ct);
        if (await db.WorkspaceUsers.AnyAsync(ct))
            throw new AppException(409, "L’espace est déjà configuré. Connectez-vous.");
        var user = new WorkspaceUser();
        Assign(user, new(input.Name, input.Email, "Admin", true, Permissions.All, input.Password));
        if (string.IsNullOrEmpty(user.PasswordHash))
            throw new AppException(400, "Un mot de passe est nécessaire.");
        db.WorkspaceUsers.Add(user);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return user;
    }
    public async Task<WorkspaceUser> Login(LoginInput input, CancellationToken ct)
    {
        if (input.Email.Length > 254 || input.Password.Length > 128)
            throw new AppException(401, "E-mail ou mot de passe incorrect.");
        var email = input.Email.Trim().ToLowerInvariant();
        var user = await db.WorkspaceUsers.SingleOrDefaultAsync(u => u.Email == email, ct);
        var valid = Hasher.VerifyHashedPassword(user ?? new(), user?.PasswordHash ?? DummyHash, input.Password);
        if (user == null || !user.IsActive || valid == PasswordVerificationResult.Failed)
            throw new AppException(401, "E-mail ou mot de passe incorrect.");
        if (valid == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = Hasher.HashPassword(user, input.Password);
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return user;
    }
    public async Task<PageResult<UserDto>> List(int page, int pageSize, CancellationToken ct)
    {
        Validation.Require(page >= 1 && pageSize is >= 1 and <= 100, "Pagination invalide.");
        return new((await db.WorkspaceUsers.AsNoTracking().OrderBy(u => u.Name).ThenBy(u => u.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct)).Select(Dto).ToList(), await db.WorkspaceUsers.CountAsync(ct), page, pageSize);
    }
    public async Task<UserDto> Save(Guid? id, UserInput input, Guid actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(867530901)", ct);
        var user = id == null ? new WorkspaceUser() : await db.WorkspaceUsers.SingleOrDefaultAsync(u => u.Id == id, ct) ?? throw new AppException(404, "Utilisateur introuvable.");
        if (id != null && input.Version != user.Version)
            throw new AppException(409, "Cet utilisateur a changé. Rechargez sa fiche.");
        if (id == actor && (!input.IsActive || input.Role != "Admin"))
            throw new AppException(400, "Vous ne pouvez pas retirer votre propre accès administrateur.");
        if (user.IsActive && user.Role == "Admin" && (!input.IsActive || input.Role != "Admin") && !await db.WorkspaceUsers.AnyAsync(u => u.Id != user.Id && u.IsActive && u.Role == "Admin", ct))
            throw new AppException(400, "Conservez au moins un administrateur actif.");
        var oldRole = user.Role;
        var oldPermissions = user.PermissionsJson;
        var oldActive = user.IsActive;
        Assign(user, input);
        if (await db.WorkspaceUsers.AnyAsync(u => u.Id != user.Id && u.Email == user.Email, ct))
            throw new AppException(409, "Cette adresse e-mail est déjà utilisée.");
        if (id == null)
        {
            if (string.IsNullOrEmpty(user.PasswordHash))
                throw new AppException(400, "Un mot de passe est nécessaire pour créer un utilisateur.");
            db.WorkspaceUsers.Add(user);
        }
        else
        {
            user.Version++;
            if (oldRole != user.Role || oldPermissions != user.PermissionsJson || oldActive != user.IsActive || !string.IsNullOrEmpty(input.Password))
                user.SessionVersion++;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Dto(user);
    }
    public async Task Delete(Guid id, int version, Guid actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(867530901)", ct);
        if (!await db.WorkspaceUsers.AnyAsync(u => u.Id == actor && u.IsActive && u.Role == "Admin", ct))
            throw new AppException(403, "Seul un administrateur actif peut supprimer un utilisateur.");
        var user = await db.WorkspaceUsers.SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new AppException(404, "Utilisateur introuvable.");
        if (id == actor)
            throw new AppException(400, "Vous ne pouvez pas supprimer votre propre compte.");
        if (user.Version != version)
            throw new AppException(409, "Cet utilisateur a changé. Rechargez sa fiche avant de le supprimer.");
        if (user.IsActive && user.Role == "Admin" && !await db.WorkspaceUsers.AnyAsync(u => u.Id != id && u.IsActive && u.Role == "Admin", ct))
            throw new AppException(400, "Conservez au moins un administrateur actif.");
        await db.WorkspaceSessions.Where(s => s.UserId == id).ExecuteDeleteAsync(ct);
        db.WorkspaceUsers.Remove(user);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
