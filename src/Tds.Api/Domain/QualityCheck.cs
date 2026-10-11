namespace Tds.Api.Domain;

/// <summary>
/// DATA_MODEL §10. Not edited: a wrong check is voided (Admin) and entered again (DOMAIN §7.0); only evidence
/// files can be added. UlazniMaterijal checks belong to a lot, all other phases to a work order.
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

    /// <summary>State of the instrument at the moment of the check (DOMAIN §7.5). Set by the server, never changed.</summary>
    public CalibrationStatusAtCheck CalibrationStatusAtCheck { get; init; }

    public List<QualityCheckEvidence> Evidence { get; init; } = [];

    /// <summary>Phases recorded on a work order; UlazniMaterijal is recorded on a lot.</summary>
    public static readonly InspectionPhase[] WorkOrderPhases =
        [InspectionPhase.LaserskoRezanje, InspectionPhase.HidraulicnoPresanje, InspectionPhase.ZavrsnaKontrola];

    /// <summary>DATA_MODEL §10: which record a check belongs to.</summary>
    public static void CheckOwner(InspectionPhase phase, Guid? workOrderId, Guid? materialLotId)
    {
        if (phase == InspectionPhase.UlazniMaterijal)
        {
            if (materialLotId is null)
                throw new DomainException("Ulazna kontrola se veže za lot — odaberi lot.");
            if (workOrderId is not null)
                throw new DomainException("Ulazna kontrola se veže za lot, ne za nalog.");
        }
        else if (workOrderId is null)
        {
            throw new DomainException($"Kontrola faze {DisplayNames.Of(phase)} se veže za nalog — odaberi nalog.");
        }
    }

    /// <summary>NC source proposed for a failed check (DOMAIN §7.1a).</summary>
    public static NcSource SourceFor(InspectionPhase phase) => phase switch
    {
        InspectionPhase.UlazniMaterijal => NcSource.UlazniMaterijal,
        InspectionPhase.LaserskoRezanje => NcSource.Laser,
        InspectionPhase.HidraulicnoPresanje => NcSource.Presanje,
        _ => NcSource.ZavrsnaKontrola,
    };
}
