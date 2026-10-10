using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tds.Api.Domain;

namespace Tds.Api.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_log");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.EntityType).HasMaxLength(60);
        b.Property(x => x.Action).HasMaxLength(20);
        b.Property(x => x.ChangesJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => x.At);
    }
}
