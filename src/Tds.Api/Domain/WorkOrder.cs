namespace Tds.Api.Domain;

/// <summary>DATA_MODEL §2. Status changes only through <see cref="WorkOrderTransitions"/>; derived values in <see cref="WorkOrderDerived"/>.</summary>
public class WorkOrder : Entity
{
    public const int NumberMax = 20;
    public const int CustomerMax = 150;
    public const int ProductDescriptionMax = 300;
    public const int DrawingNumberMax = 60;
    public const int DrawingRevisionMax = 20;
    public const int NotesMax = 2000;

    public required string Number { get; init; }
    public required string Customer { get; set; }
    public required string ProductDescription { get; set; }
    public int OrderedQty { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Zaprimljen;
    public Priority Priority { get; set; } = Priority.Normalan;
    public QualityApproval QualityApproval { get; set; } = QualityApproval.Ceka;
    public string? DrawingNumber { get; set; }
    public string? DrawingRevision { get; set; }
    public Guid? DrawingFileId { get; set; }
    public Guid? ResponsibleEmployeeId { get; set; }
    public string? Notes { get; set; }

    public static readonly WorkOrderStatus[] FinalStatuses = [WorkOrderStatus.Zavrseno, WorkOrderStatus.Isporuceno, WorkOrderStatus.Otkazano];

    /// <summary>DOMAIN §4: an order in Isporuceno or Otkazano is read-only except Notes.</summary>
    public bool IsReadOnly => Status is WorkOrderStatus.Isporuceno or WorkOrderStatus.Otkazano;
}

/// <summary>Source facts for the derived values of one order.</summary>
public sealed record WorkOrderFacts(
    int OrderedQty, DateOnly DueDate, WorkOrderStatus Status, QualityApproval QualityApproval,
    int CompletedQty, bool LaserFirstPieceApproved, bool PressFirstPieceApproved, bool FinalInspectionPassed);

/// <summary>DOMAIN §3 — computed, never stored or accepted from a client.</summary>
public sealed record WorkOrderDerived(bool ReadyForShipment, int ProgressPercent, bool IsOverdue)
{
    public static WorkOrderDerived Compute(WorkOrderFacts f, DateOnly today) => new(
        ReadyForShipment: f.FinalInspectionPassed && f.QualityApproval == QualityApproval.Prihvaceno && f.CompletedQty >= f.OrderedQty,
        ProgressPercent: f.OrderedQty <= 0 ? 0 : (int)Math.Min(100, Math.Floor(f.CompletedQty * 100m / f.OrderedQty)),
        IsOverdue: f.DueDate < today && !WorkOrder.FinalStatuses.Contains(f.Status));
}
