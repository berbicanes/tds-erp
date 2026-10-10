using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tds.Api.Domain;

namespace Tds.Api.Data.Configurations;

public class MaterialLotConfiguration : IEntityTypeConfiguration<MaterialLot>
{
    public void Configure(EntityTypeBuilder<MaterialLot> b)
    {
        b.ToTable("material_lot");
        b.Property(x => x.LotNumber).HasMaxLength(MaterialLot.LotNumberMax);
        b.HasIndex(x => x.LotNumber).IsUnique();
        b.Property(x => x.DimensionsDescription).HasMaxLength(MaterialLot.DimensionsMax);
        b.Property(x => x.StorageLocation).HasMaxLength(MaterialLot.StorageLocationMax);
        b.HasOne<MaterialItem>().WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.InspectedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FileAttachment>().WithMany().HasForeignKey(x => x.CertificateFileId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.MaterialItemId, x.ReceiptDecision });
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("stock_movement");
        b.Property(x => x.Reason).HasMaxLength(StockMovement.ReasonMax);
        b.HasOne<MaterialLot>().WithMany().HasForeignKey(x => x.MaterialLotId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.MaterialLotId);
        b.HasIndex(x => new { x.MaterialLotId, x.WorkOrderId, x.Type });
        b.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        // FK to production_entry is added with that table.
    }
}
