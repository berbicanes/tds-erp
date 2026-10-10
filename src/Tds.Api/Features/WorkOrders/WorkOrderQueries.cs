using Tds.Api.Data;
using Tds.Api.Domain;

namespace Tds.Api.Features.WorkOrders;

/// <summary>SQL-side row; derived values are added in memory (DOMAIN §3).</summary>
public sealed record WorkOrderRow
{
    public required Guid Id { get; init; }
    public required string Number { get; init; }
    public required string Customer { get; init; }
    public required string ProductDescription { get; init; }
    public required int OrderedQty { get; init; }
    public required DateOnly DueDate { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public required WorkOrderStatus Status { get; init; }
    public required Priority Priority { get; init; }
    public required QualityApproval QualityApproval { get; init; }
    public string? DrawingNumber { get; init; }
    public string? DrawingRevision { get; init; }
    public Guid? DrawingFileId { get; init; }
    public string? DrawingFileName { get; init; }
    public Guid? ResponsibleEmployeeId { get; init; }
    public string? ResponsibleEmployeeName { get; init; }
    public string? Notes { get; init; }
    public required int CompletedQty { get; init; }
    public required bool LaserFirstPieceApproved { get; init; }
    public required bool PressFirstPieceApproved { get; init; }
    public required bool FinalInspectionPassed { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required uint Version { get; init; }
}

public sealed record WorkOrderDto
{
    public required Guid Id { get; init; }
    public required string Number { get; init; }
    public required string Customer { get; init; }
    public required string ProductDescription { get; init; }
    public required int OrderedQty { get; init; }
    public required DateOnly DueDate { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public required WorkOrderStatus Status { get; init; }
    public required Priority Priority { get; init; }
    public required QualityApproval QualityApproval { get; init; }
    public string? DrawingNumber { get; init; }
    public string? DrawingRevision { get; init; }
    public Guid? DrawingFileId { get; init; }
    public string? DrawingFileName { get; init; }
    public Guid? ResponsibleEmployeeId { get; init; }
    public string? ResponsibleEmployeeName { get; init; }
    public string? Notes { get; init; }
    // Derived (DOMAIN §3) — never accepted from the client.
    public required int CompletedQty { get; init; }
    public required bool LaserFirstPieceApproved { get; init; }
    public required bool PressFirstPieceApproved { get; init; }
    public required bool FinalInspectionPassed { get; init; }
    public required bool ReadyForShipment { get; init; }
    public required int ProgressPercent { get; init; }
    public required bool IsOverdue { get; init; }
    public required uint Version { get; init; }

    public static WorkOrderDto From(WorkOrderRow r, DateOnly today)
    {
        var d = WorkOrderDerived.Compute(new WorkOrderFacts(r.OrderedQty, r.DueDate, r.Status, r.QualityApproval,
            r.CompletedQty, r.LaserFirstPieceApproved, r.PressFirstPieceApproved, r.FinalInspectionPassed), today);
        return new WorkOrderDto
        {
            Id = r.Id, Number = r.Number, Customer = r.Customer, ProductDescription = r.ProductDescription,
            OrderedQty = r.OrderedQty, DueDate = r.DueDate, ReleaseDate = r.ReleaseDate, Status = r.Status,
            Priority = r.Priority, QualityApproval = r.QualityApproval, DrawingNumber = r.DrawingNumber,
            DrawingRevision = r.DrawingRevision, DrawingFileId = r.DrawingFileId, DrawingFileName = r.DrawingFileName,
            ResponsibleEmployeeId = r.ResponsibleEmployeeId, ResponsibleEmployeeName = r.ResponsibleEmployeeName, Notes = r.Notes,
            CompletedQty = r.CompletedQty, LaserFirstPieceApproved = r.LaserFirstPieceApproved,
            PressFirstPieceApproved = r.PressFirstPieceApproved, FinalInspectionPassed = r.FinalInspectionPassed,
            ReadyForShipment = d.ReadyForShipment, ProgressPercent = d.ProgressPercent, IsOverdue = d.IsOverdue, Version = r.Version,
        };
    }
}

public static class WorkOrderQueries
{
    /// <summary>
    /// Every derived input comes from source records: CompletedQty from production entries,
    /// first-piece / final flags from quality checks (DOMAIN §3).
    /// </summary>
    public static IQueryable<WorkOrderRow> Rows(AppDbContext db) =>
        from w in db.WorkOrders
        join f in db.FileAttachments on w.DrawingFileId equals f.Id into fs
        from f in fs.DefaultIfEmpty()
        join e in db.Employees on w.ResponsibleEmployeeId equals e.Id into es
        from e in es.DefaultIfEmpty()
        select new WorkOrderRow
        {
            Id = w.Id, Number = w.Number, Customer = w.Customer, ProductDescription = w.ProductDescription,
            OrderedQty = w.OrderedQty, DueDate = w.DueDate, ReleaseDate = w.ReleaseDate, Status = w.Status,
            Priority = w.Priority, QualityApproval = w.QualityApproval, DrawingNumber = w.DrawingNumber,
            DrawingRevision = w.DrawingRevision, DrawingFileId = w.DrawingFileId, DrawingFileName = f.OriginalName,
            ResponsibleEmployeeId = w.ResponsibleEmployeeId, ResponsibleEmployeeName = e.FullName, Notes = w.Notes,
            CompletedQty = 0,
            LaserFirstPieceApproved = db.QualityChecks.Any(q => q.WorkOrderId == w.Id && !q.IsVoided
                && q.Phase == InspectionPhase.LaserskoRezanje && q.Result == InspectionResult.Prihvaceno),
            PressFirstPieceApproved = db.QualityChecks.Any(q => q.WorkOrderId == w.Id && !q.IsVoided
                && q.Phase == InspectionPhase.HidraulicnoPresanje && q.Result == InspectionResult.Prihvaceno),
            FinalInspectionPassed = db.QualityChecks.Any(q => q.WorkOrderId == w.Id && !q.IsVoided
                && q.Phase == InspectionPhase.ZavrsnaKontrola && q.Result == InspectionResult.Prihvaceno),
            CreatedAt = w.CreatedAt, Version = w.Version,
        };
}
