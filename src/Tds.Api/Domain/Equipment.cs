namespace Tds.Api.Domain;

/// <summary>DATA_MODEL §9. Machines (M-01…) and measuring instruments (MS-001…).</summary>
public class Equipment : Entity
{
    public const int CodeMax = 20;
    public const int NameMax = 120;
    public const int ManufacturerMax = 80;
    public const int SerialNumberMax = 60;

    public required string Code { get; set; }
    public required string Name { get; set; }
    public EquipmentKind Kind { get; set; }
    public string? Manufacturer { get; set; }
    public string? SerialNumber { get; set; }
    /// <summary>Proposes the next date of a new calibration (DATA_MODEL §9a).</summary>
    public int? CalibrationIntervalMonths { get; set; }

    // Last/next calibration and the calibration state are derived from EquipmentCalibration (DATA_MODEL §9).

    public bool IsActive { get; set; } = true;

    /// <summary>Seed from tdsproducts.com (DATA_MODEL §9, to be confirmed with the client).</summary>
    public static readonly (string Code, string Name)[] SeedMachines =
    [
        ("M-01", "CNC fiber laser 6 kW"),
        ("M-02", "CNC plazma 85 A"),
        ("M-03", "Hidraulična 4-stubna presa 350 t"),
        ("M-04", "Hidraulična presa 25 t (Durmark)"),
    ];
}
