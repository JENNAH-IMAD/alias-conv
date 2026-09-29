using BankStatementConverter.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BankStatementConverter.Infrastructure;
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PendingFileDeletion> PendingFileDeletions => Set<PendingFileDeletion>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<BankStatementTemplate> StatementTemplates => Set<BankStatementTemplate>();
    public DbSet<ExportTemplate> ExportTemplates => Set<ExportTemplate>();
    public DbSet<ConversionHistory> History => Set<ConversionHistory>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().Property(x => x.Permissions).HasColumnName("AccessPermissions");
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<User>().HasIndex(x => x.Username).IsUnique();
        b.Entity<Client>().HasIndex(x => x.ICE).IsUnique();
        b.Entity<Bank>().HasIndex(x => x.Code).IsUnique();
        b.Entity<BankAccount>().HasIndex(x => new { x.BankId, x.AccountNumber }).IsUnique();
        b.Entity<ExportTemplate>().HasMany(x => x.Fields).WithOne().HasForeignKey(x => x.ExportTemplateId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ExportTemplateField>().HasIndex(x => new { x.ExportTemplateId, x.Position }).IsUnique();
        b.Entity<ConversionHistory>().HasIndex(x => new { x.UserId, x.CreatedAt });
        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(string))) property.SetMaxLength(2000);
            foreach (var fk in entity.GetForeignKeys().Where(f => f.DeclaringEntityType.ClrType != typeof(ExportTemplateField))) fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var e in ChangeTracker.Entries<Entity>().Where(x => x.State == EntityState.Modified)) e.Entity.UpdatedAt = DateTime.UtcNow;
        return base.SaveChangesAsync(ct);
    }
}
public class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? "Host=localhost;Database=convertbank").Options);
}
