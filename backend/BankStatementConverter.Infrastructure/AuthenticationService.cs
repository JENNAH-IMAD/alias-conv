using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
namespace BankStatementConverter.Infrastructure;

public class AuthenticationService(AppDbContext db, IConfiguration config, ILogger<AuthenticationService> logger) : IAuthenticationService
{
    private readonly PasswordHasher<User> hasher = new();
    public async Task<UserDto> RegisterAsync(RegisterDto input, CancellationToken ct)
    {
        Validation.Text(input.Username, "Nom utilisateur", 100); Validation.Email(input.Email); Validation.Password(input.Password);
        var email = input.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email || x.Username == input.Username.Trim(), ct)) throw new AppException(409, "Adresse e-mail ou nom utilisateur déjà utilisé.");
        var user = new User { Username = input.Username.Trim(), Email = email };
        user.PasswordHash = hasher.HashPassword(user, input.Password);
        db.Users.Add(user); await db.SaveChangesAsync(ct);
        return new(user.Id, user.Username, user.Email, user.Role, Permissions.Effective(user.Role, user.Permissions));
    }
    public async Task<AuthResult> LoginAsync(LoginDto input, CancellationToken ct)
    {
        Validation.Email(input.Email); Validation.Text(input.Password, "Mot de passe", 128);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == input.Email.Trim().ToLowerInvariant(), ct);
        if (user == null || hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
        { logger.LogInformation("Tentative de connexion refusée"); throw new AppException(401, "Identifiants invalides."); }
        var expires = DateTime.UtcNow.AddMinutes(60);
        var token = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"],
            [new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.Username), new(ClaimTypes.Role, user.Role), new("session_stamp", UserManagementService.SessionStamp(user))],
            expires: expires, signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
        logger.LogInformation("Connexion réussie pour {UserId}", user.Id);
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires, new(user.Id, user.Username, user.Email, user.Role, Permissions.Effective(user.Role, user.Permissions)));
    }
}

