using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tds.Api.Domain;

namespace Tds.Api.Data.Configurations;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> b)
    {
        b.ToTable("equipment");
        b.Property(x => x.Code).HasMaxLength(Equipment.CodeMax);
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(Equipment.NameMax);
        b.Property(x => x.Manufacturer).HasMaxLength(Equipment.ManufacturerMax);
        b.Property(x => x.SerialNumber).HasMaxLength(Equipment.SerialNumberMax);
        b.HasOne<FileAttachment>().WithMany().HasForeignKey(x => x.CalibrationCertificateFileId).OnDelete(DeleteBehavior.Restrict);
    }
}
