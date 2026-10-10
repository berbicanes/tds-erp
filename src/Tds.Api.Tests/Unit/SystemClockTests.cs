using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;

namespace Tds.Api.Tests.Unit;

public class SystemClockTests
{
    [Fact]
    public void Today_is_Sarajevo_date_not_UTC_date_in_summer()
    {
        // 22:30 UTC on 12.10. = 00:30 on 13.10. in Sarajevo (CEST, UTC+2)
        var clock = new SystemClock(new FixedTimeProvider(new DateTimeOffset(2026, 10, 12, 22, 30, 0, TimeSpan.Zero)));

        Assert.Equal(new DateOnly(2026, 10, 13), clock.Today);
    }

    [Fact]
    public void Today_is_Sarajevo_date_not_UTC_date_in_winter()
    {
        // 23:30 UTC on 31.12. = 00:30 on 01.01. in Sarajevo (CET, UTC+1)
        var clock = new SystemClock(new FixedTimeProvider(new DateTimeOffset(2026, 12, 31, 23, 30, 0, TimeSpan.Zero)));

        Assert.Equal(new DateOnly(2027, 1, 1), clock.Today);
    }

    [Fact]
    public void Today_stays_on_same_date_before_local_midnight()
    {
        // 21:59 UTC on 12.10. = 23:59 on 12.10. in Sarajevo
        var clock = new SystemClock(new FixedTimeProvider(new DateTimeOffset(2026, 10, 12, 21, 59, 0, TimeSpan.Zero)));

        Assert.Equal(new DateOnly(2026, 10, 12), clock.Today);
    }

    [Fact]
    public void ToLocal_applies_Sarajevo_offset()
    {
        var clock = new SystemClock(TimeProvider.System);

        var local = clock.ToLocal(new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));

        Assert.Equal(TimeSpan.FromHours(2), local.Offset);
        Assert.Equal(12, local.Hour);
    }
}
