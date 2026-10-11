using Tds.Api.Domain;

namespace Tds.Api.Tests.UnitTests;

/// <summary>DOMAIN §7.5 and DATA_MODEL §9a.</summary>
public class CalibrationTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);
    private static Equipment Instrument(int? interval = 12) =>
        new() { Code = "MS-003", Name = "Mikrometar", Kind = EquipmentKind.MjernoSredstvo, CalibrationIntervalMonths = interval };

    [Theory]
    [InlineData(null, null, null, CalibrationStatusAtCheck.BezDatumaKalibracije)]          // never calibrated
    [InlineData("2026-01-10", null, CalibrationResult.Uskladjeno, CalibrationStatusAtCheck.BezDatumaKalibracije)]
    [InlineData("2026-01-10", "2027-01-10", CalibrationResult.Uskladjeno, CalibrationStatusAtCheck.UKalibraciji)]
    [InlineData("2025-10-01", "2026-10-12", CalibrationResult.Uskladjeno, CalibrationStatusAtCheck.UKalibraciji)] // due today = still valid
    [InlineData("2025-10-01", "2026-10-11", CalibrationResult.Uskladjeno, CalibrationStatusAtCheck.VanKalibracije)]
    [InlineData("2026-10-01", "2027-10-01", CalibrationResult.Neuskladjeno, CalibrationStatusAtCheck.VanKalibracije)] // result wins over date
    public void Status_from_latest_calibration(string? date, string? next, CalibrationResult? result, CalibrationStatusAtCheck expected)
    {
        LastCalibration? last = date is null ? null : new(DateOnly.Parse(date), next is null ? null : DateOnly.Parse(next), result!.Value);
        Assert.Equal(expected, Calibration.Status(last, Today));
    }

    [Fact]
    public void Check_without_instrument_is_BezMjernogSredstva() =>
        Assert.Equal(CalibrationStatusAtCheck.BezMjernogSredstva, Calibration.AtCheck(false, null, Today));

    [Theory]
    [InlineData(CalibrationStatusAtCheck.VanKalibracije, true)]
    [InlineData(CalibrationStatusAtCheck.BezDatumaKalibracije, true)]
    [InlineData(CalibrationStatusAtCheck.UKalibraciji, false)]
    [InlineData(CalibrationStatusAtCheck.BezMjernogSredstva, false)]
    public void Warning_only_for_out_of_calibration_and_no_date(CalibrationStatusAtCheck s, bool warn) =>
        Assert.Equal(warn, Calibration.NeedsWarning(s));

    [Fact]
    public void Interval_proposes_next_date_explicit_date_wins()
    {
        Assert.Equal(new DateOnly(2027, 3, 15), EquipmentCalibration.Record(Instrument(12), new(2026, 3, 15), null, "Lab", CalibrationResult.Uskladjeno, null, null, Today).NextCalibrationDate);
        Assert.Equal(new DateOnly(2026, 9, 1), EquipmentCalibration.Record(Instrument(12), new(2026, 3, 15), new(2026, 9, 1), "Lab", CalibrationResult.Uskladjeno, null, null, Today).NextCalibrationDate);
        Assert.Null(EquipmentCalibration.Record(Instrument(null), new(2026, 3, 15), null, "Lab", CalibrationResult.Uskladjeno, null, null, Today).NextCalibrationDate);
    }

    [Fact]
    public void Rejections()
    {
        var machine = new Equipment { Code = "M-01", Name = "Laser", Kind = EquipmentKind.Masina };
        Assert.Equal("M-01 nije mjerno sredstvo; kalibracija se bilježi samo za mjerna sredstva.",
            Assert.Throws<DomainException>(() => EquipmentCalibration.Record(machine, Today, null, "Lab", CalibrationResult.Uskladjeno, null, null, Today)).Message);
        Assert.Equal("Datum kalibracije ne može biti u budućnosti.",
            Assert.Throws<DomainException>(() => EquipmentCalibration.Record(Instrument(), Today.AddDays(1), null, "Lab", CalibrationResult.Uskladjeno, null, null, Today)).Message);
        Assert.Equal("Upiši ko je izvršio kalibraciju.",
            Assert.Throws<DomainException>(() => EquipmentCalibration.Record(Instrument(), Today, null, " ", CalibrationResult.Uskladjeno, null, null, Today)).Message);
        Assert.Equal("Sljedeća kalibracija ne može biti prije datuma kalibracije.",
            Assert.Throws<DomainException>(() => EquipmentCalibration.Record(Instrument(), Today, Today.AddDays(-1), "Lab", CalibrationResult.Uskladjeno, null, null, Today)).Message);
    }
}
