using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tds.Api.Domain;

namespace Tds.Api.Data.Configurations;

public class ControlledDocumentConfiguration : IEntityTypeConfiguration<ControlledDocument>
{
    public void Configure(EntityTypeBuilder<ControlledDocument> b)
    {
        b.ToTable("controlled_document");
        b.Property(x => x.Title).HasMaxLength(ControlledDocument.TitleMax);
        b.Property(x => x.Code).HasMaxLength(ControlledDocument.CodeMax);
        // DATA_MODEL "Version" column; the concurrency token stays on the xmin system column.
        b.Property(x => x.DocVersion).HasMaxLength(ControlledDocument.DocVersionMax).HasColumnName("version");
        b.Property(x => x.Version).HasColumnName("xmin");
        b.Property(x => x.ChangeDescription).HasMaxLength(ControlledDocument.ChangeDescriptionMax);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.OwnerEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FileAttachment>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Code, x.DocVersion }).IsUnique().HasFilter("NOT is_voided");
        // DOMAIN §7.4: at most one approved, non-voided version per Code — enforced by the database too.
        b.HasIndex(x => x.Code).IsUnique().HasFilter("status = 'Odobren' AND NOT is_voided").HasDatabaseName("ux_controlled_document_one_approved_per_code");
    }
}
