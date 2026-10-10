using Tds.Api.Features.Dashboard;

namespace Tds.Api.Tests.Unit;

/// <summary>UI §3 chart days: last 14 working days, weekends only with entries, empty past days are 0.</summary>
public class DashboardChartTests
{
    private static readonly DateOnly Wednesday = new(2026, 10, 14);

    [Fact]
    public void Fourteen_weekdays_ending_today_with_zero_for_empty_days()
    {
        var days = DashboardEndpoints.ChartDaysFor(Wednesday, new Dictionary<DateOnly, ChartDay>());

        Assert.Equal(14, days.Count);
        Assert.Equal(Wednesday, days[^1].Date);
        Assert.DoesNotContain(days, d => d.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        Assert.All(days, d => Assert.Equal(0, d.GoodQty + d.LaserCutQty + d.PressedQty + d.ScrapQty));
        Assert.Equal(new DateOnly(2026, 9, 25), days[0].Date); // 14 weekdays back incl. today
        Assert.Equal(days.Select(d => d.Date).Order(), days.Select(d => d.Date)); // oldest first
    }

    [Fact]
    public void Weekend_day_with_entries_is_shown_and_counts_as_one_of_the_14()
    {
        var saturday = new DateOnly(2026, 10, 10);
        var days = DashboardEndpoints.ChartDaysFor(Wednesday, new Dictionary<DateOnly, ChartDay>
        {
            [saturday] = new(saturday, 10, 0, 0, 1),
            [Wednesday] = new(Wednesday, 5, 3, 2, 0),
        });

        Assert.Equal(14, days.Count);
        Assert.Contains(days, d => d.Date == saturday && d.LaserCutQty == 10);
        Assert.DoesNotContain(days, d => d.Date == saturday.AddDays(1)); // Sunday without entries
        Assert.Equal(2, days[^1].GoodQty);
    }

    [Fact]
    public void Future_days_are_never_listed()
    {
        var future = Wednesday.AddDays(1);
        var days = DashboardEndpoints.ChartDaysFor(Wednesday, new Dictionary<DateOnly, ChartDay> { [future] = new(future, 1, 1, 1, 1) });
        Assert.DoesNotContain(days, d => d.Date > Wednesday);
    }
}
