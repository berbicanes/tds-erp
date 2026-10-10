using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tds.Api.Domain;

namespace Tds.Api.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.ToTable("supplier");
        b.Property(x => x.Name).HasMaxLength(Supplier.NameMax);
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.MaterialOrService).HasMaxLength(Supplier.MaterialOrServiceMax);
        b.Property(x => x.EvaluationCriteria).HasMaxLength(Supplier.TextMax);
        b.Property(x => x.Notes).HasMaxLength(Supplier.TextMax);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ResponsibleEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
