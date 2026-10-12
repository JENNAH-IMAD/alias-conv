using System.Linq.Expressions;
using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using Microsoft.EntityFrameworkCore;
namespace BankStatementConverter.Infrastructure;

public interface ICatalogDefinition<TEntity, TInput, TDto> where TEntity : Entity
{
    IQueryable<TEntity> Query(AppDbContext db);
    Expression<Func<TEntity, TDto>> Projection { get; }
    Task ValidateAsync(TInput input, AppDbContext db, CancellationToken ct);
    void Apply(TInput input, TEntity entity, AppDbContext db);
}
public class CatalogService<TEntity, TInput, TDto>(AppDbContext db, ICatalogDefinition<TEntity, TInput, TDto> definition) where TEntity : Entity, new()
{
    public async Task<PageResult<TDto>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        Validation.Require(page >= 1 && pageSize is >= 1 and <= 100, "Pagination invalide (1 à 100 éléments).");
        var q = definition.Query(db).AsNoTracking();
        return new(await q.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(definition.Projection).ToListAsync(ct), await q.CountAsync(ct), page, pageSize);
    }
    public async Task<TDto> GetAsync(Guid id, CancellationToken ct) => await definition.Query(db).AsNoTracking().Where(x => x.Id == id).Select(definition.Projection).FirstOrDefaultAsync(ct) ?? throw new AppException(404, "Élément introuvable.");
    public async Task<TDto> SaveAsync(Guid? id, TInput input, CancellationToken ct)
    {
        foreach (var property in typeof(TInput).GetProperties().Where(p => p.PropertyType == typeof(string)))
            Validation.Require(((string?)property.GetValue(input))?.Length <= 2000, $"{property.Name} : maximum 2000 caractères.");
        await definition.ValidateAsync(input, db, ct);
        var entity = id.HasValue ? await definition.Query(db).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException(404, "Élément introuvable.") : new TEntity();
        if(entity is BankAccount account && input is BankAccountInput changed && id.HasValue &&
           (account.ClientId!=changed.ClientId || account.BankId!=changed.BankId || account.Currency!=changed.Currency) &&
           (await db.StatementImports.AnyAsync(s=>s.BankAccountId==account.Id,ct) || await db.Documents.AnyAsync(d=>d.BankAccountId==account.Id,ct)))
            throw new AppException(409,"Ce compte possède des relevés archivés. Créez un autre compte pour changer de client, de banque ou de devise.");
        definition.Apply(input, entity, db);
        if (!id.HasValue) db.Add(entity);
        await db.SaveChangesAsync(ct);
        return await GetAsync(entity.Id, ct);
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.Set<TEntity>().FindAsync([id], ct) ?? throw new AppException(404, "Élément introuvable.");
        if (entity is Client && (await db.BankAccounts.AnyAsync(a => a.ClientId == id, ct) || await db.Documents.AnyAsync(h => h.ClientId == id, ct)))
            throw new AppException(409, "Ce client possède des comptes ou des relevés. Utilisez la confirmation détaillée pour supprimer le client et ses éléments liés.");
        db.Remove(entity); await db.SaveChangesAsync(ct);
    }
}
public abstract class CatalogDefinition<TEntity, TInput, TDto> : ICatalogDefinition<TEntity, TInput, TDto> where TEntity : Entity
{
    public virtual IQueryable<TEntity> Query(AppDbContext db) => db.Set<TEntity>();
    public abstract Expression<Func<TEntity, TDto>> Projection { get; }
    public abstract Task ValidateAsync(TInput input, AppDbContext db, CancellationToken ct);
    public virtual void Apply(TInput input, TEntity entity, AppDbContext db)
    {
        // Copier uniquement les propriétés déclarées dans le DTO d’entrée, sans identifiant, rôle ni horodatage.
        foreach (var source in typeof(TInput).GetProperties())
        {
            var target = typeof(TEntity).GetProperty(source.Name);
            if (target?.CanWrite == true && target.PropertyType == source.PropertyType) target.SetValue(entity, source.GetValue(input));
        }
    }
}
public class ClientDefinition : CatalogDefinition<Client, ClientInput, ClientDto>
{
    public override Expression<Func<Client, ClientDto>> Projection => x => new(x.Id, x.Name, x.LegalName, x.ICE, x.IF, x.RC, x.Email, x.Phone, x.Country, x.Accounts.Select(a => a.Bank.Name).Distinct().ToArray(), "Actif", x.PhotoVersion);
    public override Task ValidateAsync(ClientInput x, AppDbContext db, CancellationToken ct) { Validation.Client(x); return Task.CompletedTask; }
}
public class AccountDefinition : CatalogDefinition<BankAccount, BankAccountInput, BankAccountDto>
{
    public override Expression<Func<BankAccount, BankAccountDto>> Projection => x => new(x.Id, x.ClientId, x.BankId, x.Bank.Name, x.AccountNumber, x.AccountName, x.Currency, x.Journal, x.AccountCode);
    public override async Task ValidateAsync(BankAccountInput x, AppDbContext db, CancellationToken ct)
    {
        Validation.Text(x.AccountNumber, "Numéro de compte", 80); Validation.Text(x.AccountName, "Intitulé"); Validation.Text(x.Journal, "Journal", 20); Validation.Text(x.AccountCode, "Compte comptable", 30);
        Validation.Require(x.Currency is not null && System.Text.RegularExpressions.Regex.IsMatch(x.Currency, "^[A-Z]{3}$"), "Devise : 3 lettres majuscules.");
        Validation.Require(await db.Clients.AnyAsync(c => c.Id == x.ClientId, ct) && await db.Banks.AnyAsync(b => b.Id == x.BankId, ct), "Client ou banque introuvable.");
    }
}
