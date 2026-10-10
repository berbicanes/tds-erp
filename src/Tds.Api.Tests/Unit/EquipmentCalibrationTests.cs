using Tds.Api.Domain;

namespace Tds.Api.Tests.Unit;

public class EquipmentCalibrationTests
{
    private static Equipment Instrument() => new() { Code = "MS-001", Name = "Pomično mjerilo", Kind = EquipmentKind.MjernoSredstvo };

    [Fact]
    public void Next_calibration_is_derived_from_last_plus_interval()
    {
        var e = Instrument();
        e.SetCalibration(12, new DateOnly(2026, 3, 15), manualNext: new DateOnly(2030, 1, 1));
        Assert.Equal(new DateOnly(2027, 3, 15), e.NextCalibrationDate);
    }

    [Fact]
    public void Month_end_is_clamped()
    {
        var e = Instrument();
        e.SetCalibration(1, new DateOnly(2026, 1, 31), null);
        Assert.Equal(new DateOnly(2026, 2, 28), e.NextCalibrationDate);
    }

    [Fact]
    public void Without_interval_the_manual_date_is_kept()
    {
        var e = Instrument();
        e.SetCalibration(null, new DateOnly(2026, 3, 15), manualNext: new DateOnly(2026, 9, 1));
        Assert.Equal(new DateOnly(2026, 9, 1), e.NextCalibrationDate);
    }

    [Fact]
    public void Interval_without_last_date_uses_manual_date()
    {
        var e = Instrument();
        e.SetCalibration(12, null, manualNext: null);
        Assert.Null(e.NextCalibrationDate);
    }
}
