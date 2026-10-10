using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tds.Api.Domain;

namespace Tds.Api.Data.Configurations;

public class MaterialItemConfiguration : IEntityTypeConfiguration<MaterialItem>
{
    public void Configure(EntityTypeBuilder<MaterialItem> b)
    {
        b.ToTable("material_item");
        b.Property(x => x.Name).HasMaxLength(MaterialItem.NameMax);
        b.Property(x => x.Grade).HasMaxLength(MaterialItem.GradeMax);
        b.Property(x => x.ThicknessMm).HasPrecision(6, 2);
        // "Lim crni" without grade/thickness is still unique (NULLS NOT DISTINCT, Postgres 15+).
        b.HasIndex(x => new { x.Name, x.Grade, x.ThicknessMm }).IsUnique().AreNullsDistinct(false);
    }
}
