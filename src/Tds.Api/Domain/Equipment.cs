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
    public int? CalibrationIntervalMonths { get; set; }
    public DateOnly? LastCalibrationDate { get; set; }

    /// <summary>Derived from Last + interval when both are set; otherwise entered manually (DATA_MODEL §9).</summary>
    public DateOnly? NextCalibrationDate { get; set; }

    public Guid? CalibrationCertificateFileId { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Applies the calibration rule; <paramref name="manualNext"/> is used only without an interval.</summary>
    public void SetCalibration(int? intervalMonths, DateOnly? last, DateOnly? manualNext)
    {
        CalibrationIntervalMonths = intervalMonths;
        LastCalibrationDate = last;
        NextCalibrationDate = intervalMonths is > 0 && last is { } l ? l.AddMonths(intervalMonths.Value) : manualNext;
    }

    /// <summary>Seed from tdsproducts.com (DATA_MODEL §9, to be confirmed with the client).</summary>
    public static readonly (string Code, string Name)[] SeedMachines =
    [
        ("M-01", "CNC fiber laser 6 kW"),
        ("M-02", "CNC plazma 85 A"),
        ("M-03", "Hidraulična 4-stubna presa 350 t"),
        ("M-04", "Hidraulična presa 25 t (Durmark)"),
    ];
}
