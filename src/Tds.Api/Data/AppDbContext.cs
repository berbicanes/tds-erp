using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Domain;

namespace Tds.Api.Data;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>For derived contexts (tests).</summary>
    protected AppDbContext(DbContextOptions options) : base(options) { }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<FileAttachment> FileAttachments => Set<FileAttachment>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<MaterialItem> MaterialItems => Set<MaterialItem>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // CLAUDE.md: enums as strings, quantities decimal(12,3).
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<decimal>().HavePrecision(12, 3);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Identity sets PascalCase table names explicitly; keep everything snake_case.
        modelBuilder.Entity<AppUser>().ToTable("asp_net_users");
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("asp_net_roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("asp_net_user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("asp_net_user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("asp_net_user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("asp_net_user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("asp_net_role_claims");

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).Property(nameof(Entity.Id)).ValueGeneratedNever();
            modelBuilder.Entity(entityType.ClrType).Property(nameof(Entity.Version)).IsRowVersion();
            if (typeof(IVoidable).IsAssignableFrom(entityType.ClrType))
                modelBuilder.Entity(entityType.ClrType).Property(nameof(IVoidable.VoidedReason))
                    .HasMaxLength(VoidableEntity.VoidedReasonMaxLength);
        }
    }
}
