using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tds.Api.Domain;

namespace Tds.Api.Data.Configurations;

public class FileAttachmentConfiguration : IEntityTypeConfiguration<FileAttachment>
{
    public void Configure(EntityTypeBuilder<FileAttachment> b)
    {
        b.ToTable("file_attachment");
        b.Property(x => x.OriginalName).HasMaxLength(255);
        b.Property(x => x.ContentType).HasMaxLength(100);
        b.Property(x => x.StoragePath).HasMaxLength(400);
        b.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength();
        b.HasIndex(x => x.StoragePath).IsUnique();
    }
}
