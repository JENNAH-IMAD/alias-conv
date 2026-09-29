using System.Security.Cryptography;
using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankStatementConverter.Infrastructure;

public record ManagedUserDto(Guid Id, string Name, string Email, string Role, DateTime CreatedAt, string[]? Permissions = null);
public record ManagedUserInput(string Name, string Email, string Role, string? Password);

public class UserManagementService(AppDbContext db)
{
    public static string SessionStamp(User user) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.PasswordHash + "|" + user.Role + "|" + user.Email + "|" + user.Username + (user.Permissions == null ? "" : "|permissions:" + string.Join(",", user.Permissions.OrderBy(x => x, StringComparer.Ordinal))))));
    public static void ValidateInput(ManagedUserInput input, bool creating)
    {
        Validation.Text(input.Name, "Nom", 100); Validation.Email(input.Email);
        Validation.Require(input.Role is "Admin" or "User", "Rôle invalide.");
        if (creating || !string.IsNullOrEmpty(input.Password)) Validation.Password(input.Password);
    }
    public static void ValidateSelfChange(Guid actor, Guid target, string role, bool deleting = false)
    {
        Validation.Require(actor != target || (!deleting && role == "Admin"), "Vous ne pouvez pas supprimer votre compte ni retirer votre propre rôle administrateur.");
    }
    public async Task<PageResult<ManagedUserDto>> ListAsync(int page, int pageSize, string? search, string? role, CancellationToken ct)
    {
        Validation.Require(page >= 1 && page <= 100000 && pageSize is >= 1 and <= 100, "Pagination invalide.");
        Validation.Require(string.IsNullOrEmpty(role) || role is "Admin" or "User", "Rôle invalide.");
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLowerInvariant(); query = query.Where(x => x.Username.ToLower().Contains(term) || x.Email.ToLower().Contains(term)); }
        if (!string.IsNullOrEmpty(role)) query = query.Where(x => x.Role == role);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Username).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new ManagedUserDto(x.Id, x.Username, x.Email, x.Role, x.CreatedAt, x.Permissions)).ToListAsync(ct);
        return new(items.Select(x => x with { Permissions = Permissions.Effective(x.Role, x.Permissions) }).ToArray(), total, page, pageSize);
    }
    public async Task<ManagedUserDto> SaveAsync(Guid actor, Guid? id, ManagedUserInput input, CancellationToken ct)
    {
        ValidateInput(input, id == null);
        if (id.HasValue) ValidateSelfChange(actor, id.Value, input.Role);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(81472026)", ct);
        var email = input.Email.Trim().ToLowerInvariant(); var name = input.Name.Trim();
        if (await db.Users.AnyAsync(x => x.Id != id && (x.Email == email || x.Username == name), ct)) throw new AppException(409, "Adresse e-mail ou nom utilisateur déjà utilisé.");
        var user = id.HasValue ? await db.Users.FindAsync(new object[] { id.Value }, ct) ?? throw new AppException(404, "Utilisateur introuvable.") : new User();
        if (user.Role == "Admin" && input.Role != "Admin" && await db.Users.CountAsync(x => x.Role == "Admin", ct) <= 1) throw new AppException(409, "Le dernier administrateur doit être conservé.");
        user.Username = name; user.Email = email; user.Role = input.Role;
        if (!string.IsNullOrEmpty(input.Password)) user.PasswordHash = new PasswordHasher<User>().HashPassword(user, input.Password);
        if (!id.HasValue) db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(user.Id, user.Username, user.Email, user.Role, user.CreatedAt, Permissions.Effective(user.Role, user.Permissions));
    }
    public async Task<ManagedUserDto> SetPermissionsAsync(Guid actor, Guid id, string[]? permissions, CancellationToken ct)
    {
        var selected = Permissions.Validate(permissions);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(81472026)", ct);
        if (!await db.Users.AnyAsync(x => x.Id == actor && x.Role == "Admin", ct)) throw new AppException(403, "Action réservée aux administrateurs.");
        var user = await db.Users.FindAsync(new object[] { id }, ct) ?? throw new AppException(404, "Utilisateur introuvable.");
        Validation.Require(user.Role != "Admin", "Les administrateurs conservent toutes les permissions.");
        user.Permissions = selected;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(user.Id, user.Username, user.Email, user.Role, user.CreatedAt, selected);
    }
    public async Task DeleteAsync(Guid actor, Guid id, CancellationToken ct)
    {
        ValidateSelfChange(actor, id, "User", true);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(81472026)", ct);
        var user = await db.Users.FindAsync(new object[] { id }, ct) ?? throw new AppException(404, "Utilisateur introuvable.");
        if (await db.History.AnyAsync(x => x.UserId == id, ct)) throw new AppException(409, "Cet utilisateur possède des conversions. Son compte ne peut pas être supprimé.");
        if (user.Role == "Admin" && await db.Users.CountAsync(x => x.Role == "Admin", ct) <= 1) throw new AppException(409, "Le dernier administrateur doit être conservé.");
        db.Users.Remove(user); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
}

