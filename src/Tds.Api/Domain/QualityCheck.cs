namespace Tds.Api.Domain;

/// <summary>
/// DATA_MODEL §10. Not edited: a wrong check is voided (Admin) and entered again (DOMAIN §7.0).
/// Phase 1: checks on work orders only; UlazniMaterijal, evidence files and measuring equipment come in Phase 3.
/// </summary>
public class QualityCheck : VoidableEntity
{
    public const int NumberMax = 20;
    public const int TextMax = 300;

    public required string Number { get; init; }
    public Guid? WorkOrderId { get; init; }
    public Guid? MaterialLotId { get; init; }
    public InspectionPhase Phase { get; init; }
    public required string Characteristic { get; init; }
    public string? AcceptanceCriterion { get; init; }
    public string? MeasuredResult { get; init; }
    public int? SampleSize { get; init; }
    public InspectionResult Result { get; init; }
    public Guid InspectedByEmployeeId { get; init; }
    public DateOnly InspectionDate { get; init; }
    public Guid? MeasuringEquipmentId { get; init; }

    /// <summary>Phases that can be recorded on a work order in Phase 1.</summary>
    public static readonly InspectionPhase[] WorkOrderPhases =
        [InspectionPhase.LaserskoRezanje, InspectionPhase.HidraulicnoPresanje, InspectionPhase.ZavrsnaKontrola];
}
