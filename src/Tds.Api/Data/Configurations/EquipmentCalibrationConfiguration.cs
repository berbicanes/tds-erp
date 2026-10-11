using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tds.Api.Domain;

namespace Tds.Api.Data.Configurations;

public class EquipmentCalibrationConfiguration : IEntityTypeConfiguration<EquipmentCalibration>
{
    public void Configure(EntityTypeBuilder<EquipmentCalibration> b)
    {
        b.ToTable("equipment_calibration");
        b.Property(x => x.PerformedBy).HasMaxLength(EquipmentCalibration.PerformedByMax);
        b.Property(x => x.Note).HasMaxLength(EquipmentCalibration.NoteMax);
        b.HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FileAttachment>().WithMany().HasForeignKey(x => x.CertificateFileId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EquipmentId, x.CalibrationDate });
    }
}
