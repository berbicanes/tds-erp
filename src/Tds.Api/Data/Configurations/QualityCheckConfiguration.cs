using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tds.Api.Domain;

namespace Tds.Api.Data.Configurations;

public class QualityCheckConfiguration : IEntityTypeConfiguration<QualityCheck>
{
    public void Configure(EntityTypeBuilder<QualityCheck> b)
    {
        b.ToTable("quality_check");
        b.Property(x => x.Number).HasMaxLength(QualityCheck.NumberMax);
        b.HasIndex(x => x.Number).IsUnique();
        b.Property(x => x.Characteristic).HasMaxLength(QualityCheck.TextMax);
        b.Property(x => x.AcceptanceCriterion).HasMaxLength(QualityCheck.TextMax);
        b.Property(x => x.MeasuredResult).HasMaxLength(QualityCheck.TextMax);
        b.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MaterialLot>().WithMany().HasForeignKey(x => x.MaterialLotId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.InspectedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Equipment>().WithMany().HasForeignKey(x => x.MeasuringEquipmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.WorkOrderId, x.Phase, x.Result });
        b.HasIndex(x => x.MaterialLotId);
        b.HasMany(x => x.Evidence).WithOne().HasForeignKey(x => x.QualityCheckId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class QualityCheckEvidenceConfiguration : IEntityTypeConfiguration<QualityCheckEvidence>
{
    public void Configure(EntityTypeBuilder<QualityCheckEvidence> b)
    {
        b.ToTable("quality_check_evidence");
        b.HasKey(x => new { x.QualityCheckId, x.FileId });
        b.HasOne<FileAttachment>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
    }
}
