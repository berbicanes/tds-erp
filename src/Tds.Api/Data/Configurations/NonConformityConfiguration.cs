using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tds.Api.Domain;

namespace Tds.Api.Data.Configurations;

public class NonConformityConfiguration : IEntityTypeConfiguration<NonConformity>
{
    public void Configure(EntityTypeBuilder<NonConformity> b)
    {
        b.ToTable("non_conformity");
        b.Property(x => x.Number).HasMaxLength(NonConformity.NumberMax);
        b.HasIndex(x => x.Number).IsUnique();
        b.Property(x => x.Description).HasMaxLength(NonConformity.TextMax);
        b.Property(x => x.Containment).HasMaxLength(NonConformity.TextMax);
        b.Property(x => x.RootCause).HasMaxLength(NonConformity.TextMax);
        b.Property(x => x.CorrectiveAction).HasMaxLength(NonConformity.TextMax);
        b.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MaterialLot>().WithMany().HasForeignKey(x => x.MaterialLotId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<QualityCheck>().WithMany().HasForeignKey(x => x.QualityCheckId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ActionOwnerEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.WorkOrderId);
        // DOMAIN §7.1a: at most one non-voided NC per quality check.
        b.HasIndex(x => x.QualityCheckId).IsUnique().HasFilter("quality_check_id IS NOT NULL AND NOT is_voided");
        b.HasMany(x => x.Evidence).WithOne().HasForeignKey(x => x.NonConformityId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class NonConformityEvidenceConfiguration : IEntityTypeConfiguration<NonConformityEvidence>
{
    public void Configure(EntityTypeBuilder<NonConformityEvidence> b)
    {
        b.ToTable("non_conformity_evidence");
        b.HasKey(x => new { x.NonConformityId, x.FileId });
        b.HasOne<FileAttachment>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupplierCertificateConfiguration : IEntityTypeConfiguration<SupplierCertificate>
{
    public void Configure(EntityTypeBuilder<SupplierCertificate> b)
    {
        b.ToTable("supplier_certificate");
        b.HasKey(x => new { x.SupplierId, x.FileId });
        b.HasOne<Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<FileAttachment>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
    }
}
