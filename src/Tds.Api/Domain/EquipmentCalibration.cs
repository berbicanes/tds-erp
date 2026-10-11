namespace Tds.Api.Domain;

/// <summary>
/// DATA_MODEL §9a. One calibration of a measuring instrument. Not edited: a wrong entry is voided (Admin) and
/// entered again. The instrument's last/next calibration and state are derived from the latest non-voided one.
/// </summary>
public class EquipmentCalibration : VoidableEntity
{
    public const int PerformedByMax = 150;
    public const int NoteMax = 1000;

    public Guid EquipmentId { get; init; }
    public DateOnly CalibrationDate { get; init; }
    public DateOnly? NextCalibrationDate { get; init; }
    public required string PerformedBy { get; init; }
    public CalibrationResult Result { get; init; }
    public Guid? CertificateFileId { get; init; }
    public string? Note { get; init; }

    /// <summary>
    /// Records a calibration. Without an explicit next date, the instrument's interval proposes one
    /// (CalibrationDate + interval); an explicit date wins.
    /// </summary>
    public static EquipmentCalibration Record(
        Equipment equipment, DateOnly calibrationDate, DateOnly? nextCalibrationDate, string performedBy,
        CalibrationResult result, Guid? certificateFileId, string? note, DateOnly today)
    {
        if (equipment.Kind != EquipmentKind.MjernoSredstvo)
            throw new DomainException($"{equipment.Code} nije mjerno sredstvo; kalibracija se bilježi samo za mjerna sredstva.");
        if (calibrationDate > today)
            throw new DomainException("Datum kalibracije ne može biti u budućnosti.");
        if (string.IsNullOrWhiteSpace(performedBy))
            throw new DomainException("Upiši ko je izvršio kalibraciju.");
        var next = nextCalibrationDate
            ?? (equipment.CalibrationIntervalMonths is > 0 ? calibrationDate.AddMonths(equipment.CalibrationIntervalMonths.Value) : null);
        if (next is { } n && n < calibrationDate)
            throw new DomainException("Sljedeća kalibracija ne može biti prije datuma kalibracije.");

        return new EquipmentCalibration
        {
            EquipmentId = equipment.Id,
            CalibrationDate = calibrationDate,
            NextCalibrationDate = next,
            PerformedBy = performedBy.Trim(),
            Result = result,
            CertificateFileId = certificateFileId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
    }
}

/// <summary>Latest non-voided calibration as seen by the rules.</summary>
public sealed record LastCalibration(DateOnly CalibrationDate, DateOnly? NextCalibrationDate, CalibrationResult Result);

/// <summary>DOMAIN §7.5 — calibration state of a measuring instrument.</summary>
public static class Calibration
{
    public static CalibrationStatusAtCheck Status(LastCalibration? last, DateOnly today)
    {
        if (last is null || last.NextCalibrationDate is null)
            return CalibrationStatusAtCheck.BezDatumaKalibracije;
        if (last.Result == CalibrationResult.Neuskladjeno || last.NextCalibrationDate < today)
            return CalibrationStatusAtCheck.VanKalibracije;
        return CalibrationStatusAtCheck.UKalibraciji;
    }

    /// <summary>Stored on a quality check at creation (never changed later).</summary>
    public static CalibrationStatusAtCheck AtCheck(bool hasInstrument, LastCalibration? last, DateOnly today) =>
        hasInstrument ? Status(last, today) : CalibrationStatusAtCheck.BezMjernogSredstva;

    /// <summary>A warning is shown for these states.</summary>
    public static bool NeedsWarning(CalibrationStatusAtCheck s) =>
        s is CalibrationStatusAtCheck.VanKalibracije or CalibrationStatusAtCheck.BezDatumaKalibracije;
}
