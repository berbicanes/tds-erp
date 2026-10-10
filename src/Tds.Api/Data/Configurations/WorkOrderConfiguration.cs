using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tds.Api.Domain;

namespace Tds.Api.Data.Configurations;

public class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> b)
    {
        b.ToTable("work_order");
        b.Property(x => x.Number).HasMaxLength(WorkOrder.NumberMax);
        b.HasIndex(x => x.Number).IsUnique();
        b.Property(x => x.Customer).HasMaxLength(WorkOrder.CustomerMax);
        b.Property(x => x.ProductDescription).HasMaxLength(WorkOrder.ProductDescriptionMax);
        b.Property(x => x.DrawingNumber).HasMaxLength(WorkOrder.DrawingNumberMax);
        b.Property(x => x.DrawingRevision).HasMaxLength(WorkOrder.DrawingRevisionMax);
        b.Property(x => x.Notes).HasMaxLength(WorkOrder.NotesMax);
        b.HasOne<FileAttachment>().WithMany().HasForeignKey(x => x.DrawingFileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ResponsibleEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Status, x.DueDate });
    }
}
