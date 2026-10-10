using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tds.Api.Domain;

namespace Tds.Api.Data.Configurations;

public class ProductionEntryConfiguration : IEntityTypeConfiguration<ProductionEntry>
{
    public void Configure(EntityTypeBuilder<ProductionEntry> b)
    {
        b.ToTable("production_entry");
        b.Property(x => x.Number).HasMaxLength(ProductionEntry.NumberMax);
        b.HasIndex(x => x.Number).IsUnique();
        b.Property(x => x.LaborHours).HasPrecision(5, 2);
        b.Property(x => x.Note).HasMaxLength(ProductionEntry.NoteMax);
        b.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.OperatorEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MaterialLot>().WithMany().HasForeignKey(x => x.MaterialLotId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Equipment).WithOne().HasForeignKey(x => x.ProductionEntryId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.Date);
        b.HasIndex(x => x.WorkOrderId);
    }
}

public class ProductionEntryEquipmentConfiguration : IEntityTypeConfiguration<ProductionEntryEquipment>
{
    public void Configure(EntityTypeBuilder<ProductionEntryEquipment> b)
    {
        b.ToTable("production_entry_equipment");
        b.HasKey(x => new { x.ProductionEntryId, x.EquipmentId });
        b.HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
