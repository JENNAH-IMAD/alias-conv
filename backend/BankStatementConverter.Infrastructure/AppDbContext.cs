using BankStatementConverter.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Design;
namespace BankStatementConverter.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor? http = null) : DbContext(options)
{
    public DbSet<ExtractionProfile> ExtractionProfiles => Set<ExtractionProfile>();
    public DbSet<StatementActivity> StatementActivities => Set<StatementActivity>();
    public DbSet<ExtractionProfileActivity> ExtractionProfileActivities => Set<ExtractionProfileActivity>();
    public DbSet<ExportJob> ExportJobs => Set<ExportJob>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<WorkspaceUser> WorkspaceUsers => Set<WorkspaceUser>();
    public DbSet<WorkspaceSession> WorkspaceSessions => Set<WorkspaceSession>();
    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ExportTemplate> ExportTemplates => Set<ExportTemplate>();
    public DbSet<StatementImport> StatementImports => Set<StatementImport>();
    public DbSet<PendingFileDeletion> PendingFileDeletions => Set<PendingFileDeletion>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ExtractionProfileActivity>().HasIndex(x => new { x.ProfileId, x.CreatedAt });
        b.Entity<ExtractionProfileActivity>().Property(x => x.SnapshotJson).HasColumnType("jsonb").Metadata.SetMaxLength(null);
        b.Entity<StatementActivity>().HasIndex(x => new { x.StatementId, x.CreatedAt });
        b.Entity<StatementActivity>().HasIndex(x => new { x.CreatedAt, x.Id });
        b.Entity<StatementActivity>().Property(x => x.ChangesJson).HasColumnType("jsonb").Metadata.SetMaxLength(null);
        b.Entity<ExportJob>().HasIndex(x => new { x.StatementId, x.CreatedAt });
        b.Entity<ExportJob>().Property(x => x.TemplateSnapshotJson).HasColumnType("jsonb").Metadata.SetMaxLength(null);
        b.Entity<ExtractionProfile>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<ExtractionProfile>().HasIndex(x => new { x.BankCode, x.Currency });
        b.Entity<StatementImport>().HasIndex(x => new { x.BankAccountId, x.FiledAt });
        b.Entity<WorkspaceUser>().HasIndex(x => x.Email).IsUnique();
        b.Entity<WorkspaceUser>().Property(x => x.PermissionsJson).HasColumnType("jsonb");
        b.Entity<WorkspaceUser>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<WorkspaceSession>().HasIndex(x => x.ExpiresAt);
        b.Entity<StatementImport>().HasOne<Client>().WithMany().HasForeignKey(x => x.ClientId);
        b.Entity<StatementImport>().HasOne<BankAccount>().WithMany().HasForeignKey(x => x.BankAccountId);
        b.Entity<StatementImport>().HasIndex(x => new { x.ClientId, x.CreatedAt });
        b.Entity<Client>().HasIndex(x => x.ICE).IsUnique();
        b.Entity<Bank>().HasIndex(x => x.Code).IsUnique();
        b.Entity<BankAccount>().HasIndex(x => new { x.BankId, x.AccountNumber }).IsUnique();
        b.Entity<Document>().HasIndex(x => new { x.ClientId, x.CreatedAt });
        b.Entity<ExportTemplate>().HasMany(x => x.Fields).WithOne().HasForeignKey(x => x.ExportTemplateId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ExportTemplateField>().HasIndex(x => new { x.ExportTemplateId, x.Position }).IsUnique();
        b.Entity<StatementImport>().Property(x => x.NormalizedJson).HasColumnType("jsonb");
        b.Entity<StatementImport>().Property(x => x.ImportDiagnosticsJson).HasColumnType("jsonb");
        b.Entity<StatementImport>().Property(x => x.RawExtractionJson).HasColumnType("jsonb");
        b.Entity<StatementImport>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<StatementImport>().HasIndex(x => new { x.CreatedAt, x.Id });
        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(string)))
                property.SetMaxLength(2000);
            foreach (var fk in entity.GetForeignKeys().Where(fk => fk.DeclaringEntityType.ClrType != typeof(ExportTemplateField)))
                fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
        b.Entity<StatementImport>().Property(x => x.NormalizedJson).Metadata.SetMaxLength(null);
        b.Entity<StatementImport>().Property(x => x.ImportDiagnosticsJson).Metadata.SetMaxLength(null);
        b.Entity<StatementImport>().Property(x => x.RawExtractionJson).Metadata.SetMaxLength(null);
        b.Entity<StatementActivity>().Property(x => x.ChangesJson).Metadata.SetMaxLength(null);
        b.Entity<ExtractionProfileActivity>().Property(x => x.SnapshotJson).Metadata.SetMaxLength(null);
        b.Entity<ExportJob>().Property(x => x.TemplateSnapshotJson).Metadata.SetMaxLength(null);
        b.Entity<WorkspaceUser>().Property(x => x.PermissionsJson).Metadata.SetMaxLength(null);
        foreach (var name in new[] { "MappingJson", "SampleJson", "AliasesJson" })
        {
            b.Entity<ExtractionProfile>().Property<string>(name).HasColumnType("jsonb").Metadata.SetMaxLength(null);
        }
    }
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ChangeTracker.DetectChanges();
        var changes = ChangeTracker.Entries<StatementImport>().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        foreach (var entry in changes)
        {
            var item = entry.Entity;
            var action = entry.State == EntityState.Added ? "IMPORTED" : entry.State == EntityState.Deleted ? "DELETED" :
                entry.Property(x => x.BankAccountId).IsModified ? "FILED" :
                entry.Property(x => x.Status).IsModified && item.Status == "VALIDATED" ? "VALIDATED" :
                entry.Property(x => x.Status).IsModified && item.Status == "EXPORTED" ? "DOWNLOAD_COMPLETED" :
                entry.Property(x => x.SourceStorageKey).IsModified ? "SOURCE_SAVED" : "MODIFIED";
            var client = item.ClientId.HasValue ? await Clients.AsNoTracking().Where(x => x.Id == item.ClientId).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
            var account = item.BankAccountId.HasValue ? await BankAccounts.AsNoTracking().Where(x => x.Id == item.BankAccountId).Select(x => x.AccountName).FirstOrDefaultAsync(ct) : null;
            var fields = entry.Properties.Where(p => entry.State != EntityState.Modified || p.IsModified)
                .Where(p => p.Metadata.Name is not "RawExtractionJson" and not "ImportDiagnosticsJson" and not "SourceStorageKey")
                .ToDictionary(p => p.Metadata.Name, p => new { before = entry.State == EntityState.Added ? null : p.OriginalValue, after = entry.State == EntityState.Deleted ? null : p.CurrentValue });
            StatementActivities.Add(new()
            {
                StatementId = item.Id,
                ClientId = item.ClientId,
                BankAccountId = item.BankAccountId,
                ClientName = client ?? "",
                AccountName = account ?? "",
                BankName = item.BankName,
                FileName = item.SourceFileName ?? item.StatementNumber ?? item.BankName,
                Action = action,
                Version = item.Version,
                ChangesJson = System.Text.Json.JsonSerializer.Serialize(fields),
                ActorId = http?.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),
                ActorName = http?.HttpContext?.User.Identity?.Name ?? item.ModifiedByName ?? item.ImportedByName ?? WorkspaceIdentity.Name
            });
        }
        foreach (var e in ChangeTracker.Entries<Entity>().Where(x => x.State == EntityState.Modified))
            e.Entity.UpdatedAt = DateTime.UtcNow;
        return await base.SaveChangesAsync(ct);
    }

}
public class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? "Host=localhost;Database=convertbank").Options);
}
