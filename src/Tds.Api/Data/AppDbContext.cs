using Microsoft.EntityFrameworkCore;
using Tds.Api.Domain;

namespace Tds.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>For derived contexts (tests).</summary>
    protected AppDbContext(DbContextOptions options) : base(options) { }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<FileAttachment> FileAttachments => Set<FileAttachment>();
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

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).Property(nameof(Entity.Id)).ValueGeneratedNever();
            if (typeof(IVoidable).IsAssignableFrom(entityType.ClrType))
                modelBuilder.Entity(entityType.ClrType).Property(nameof(IVoidable.VoidedReason))
                    .HasMaxLength(VoidableEntity.VoidedReasonMaxLength);
        }
    }
}
