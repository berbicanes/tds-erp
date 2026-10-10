using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tds.Api.Domain;

namespace Tds.Api.Data.Configurations;

public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> b)
    {
        b.ToTable("note");
        b.Property(x => x.Text).HasMaxLength(Note.TextMax);
        b.HasIndex(x => new { x.IsDone, x.DoneAt });
    }
}
