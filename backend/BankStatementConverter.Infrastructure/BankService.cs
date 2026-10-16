using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using Microsoft.EntityFrameworkCore;

namespace BankStatementConverter.Infrastructure;

public class BankService(AppDbContext db)
{
    private IQueryable<BankDto> Project(IQueryable<Bank> banks) => banks.Select(b => new BankDto(
        b.Id, b.Name, b.Code, b.Description, b.Accounts.Select(a => a.ClientId).Distinct().Count(),
        "Actif", b.Accounts.Count, b.LogoVersion));
    private static void ValidatePage(int page, int pageSize) => Validation.Require(page is >= 1 and <= 100000 && pageSize is >= 1 and <= 100, "Pagination invalide.");
    public async Task<PageResult<BankDto>> ListAsync(int page, int pageSize, string? search, string sort, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        Validation.Require(search == null || search.Length <= 200, "Recherche : 200 caractères maximum.");
        Validation.Require(sort is "name" or "name-desc" or "code", "Tri invalide.");
        var q = db.Banks.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(b => b.Name.ToLower().Contains(term) || b.Code.ToLower().Contains(term) || b.Description.ToLower().Contains(term));
        }
        var sorted = sort switch
        {
            "name-desc" => q.OrderByDescending(b => b.Name),
            "code" => q.OrderBy(b => b.Code),
            _ => q.OrderBy(b => b.Name)
        };
        return new(await Project(sorted.ThenBy(b => b.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct), await q.CountAsync(ct), page, pageSize);
    }
    public async Task<BankDto> GetAsync(Guid id, CancellationToken ct) => await Project(db.Banks.AsNoTracking().Where(b => b.Id == id)).SingleOrDefaultAsync(ct) ?? throw new AppException(404, "Banque introuvable.");
    public async Task<BankDto> SaveAsync(Guid? id, BankInput input, CancellationToken ct)
    {
        Validation.Text(input.Name, "Nom de la banque", 200);
        Validation.Text(input.Code, "Code de la banque", 30);
        Validation.Require(input.Description == null || input.Description.Length <= 2000, "Description : 2000 caractères maximum.");
        var name = input.Name.Trim();
        var code = input.Code.Trim().ToUpperInvariant();
        Validation.Require(System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z0-9][A-Z0-9_-]{0,29}$"), "Code : 1 à 30 lettres, chiffres, tirets ou underscores, commençant par une lettre ou un chiffre.");
        var bank = id.HasValue ? await db.Banks.FindAsync([id.Value], ct) ?? throw new AppException(404, "Banque introuvable.") : new Bank();
        if (await db.Banks.AnyAsync(b => b.Id != bank.Id && b.Code.ToUpper() == code, ct))
            throw new AppException(409, "Ce code bancaire est déjà utilisé.");
        bank.Name = name;
        bank.Code = code;
        bank.Description = input.Description?.Trim() ?? "";
        if (!id.HasValue)
            db.Banks.Add(bank);
        await db.SaveChangesAsync(ct);
        return await GetAsync(bank.Id, ct);
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var bank = await db.Banks.FindAsync([id], ct) ?? throw new AppException(404, "Banque introuvable.");
        var accounts = await db.BankAccounts.CountAsync(a => a.BankId == id, ct);
        if (accounts > 0)
            throw new AppException(409, "Cette banque possède des comptes. Utilisez la confirmation détaillée pour supprimer les éléments liés.");
        db.Banks.Remove(bank);
        await db.SaveChangesAsync(ct);
    }
    public async Task<PageResult<BankAccountSummary>> AccountsAsync(Guid id, int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        await GetAsync(id, ct);
        var q = db.BankAccounts.AsNoTracking().Where(a => a.BankId == id);
        return new(await q.OrderBy(a => a.AccountName).ThenBy(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(a => new BankAccountSummary(a.Id, a.Client.Name, a.AccountName, a.AccountNumber, a.Currency)).ToListAsync(ct), await q.CountAsync(ct), page, pageSize);
    }
}
