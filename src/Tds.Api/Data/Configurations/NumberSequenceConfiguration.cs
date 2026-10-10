using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tds.Api.Domain;

namespace Tds.Api.Data.Configurations;

public class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> b)
    {
        b.ToTable("number_sequence");
        b.HasKey(x => new { x.Kind, x.Period });
        b.Property(x => x.Kind).HasMaxLength(10);
        b.Property(x => x.Period).HasMaxLength(8);
    }
}
