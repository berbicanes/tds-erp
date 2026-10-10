using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tds.Api.Domain;

namespace Tds.Api.Data.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable("employee");
        b.Property(x => x.FullName).HasMaxLength(Employee.FullNameMax);
        b.Property(x => x.Position).HasMaxLength(Employee.PositionMax);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        // One login belongs to at most one employee.
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("user_id IS NOT NULL");
        b.HasIndex(x => x.FullName);
    }
}
