namespace Tds.Api.Infrastructure;

/// <summary>
/// The only source of "now" in business logic. "Today" is always Europe/Sarajevo
/// (CLAUDE.md: never DateTime.Now in logic).
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>Current business date in Europe/Sarajevo.</summary>
    DateOnly Today { get; }

    /// <summary>Converts an instant to Europe/Sarajevo local time.</summary>
    DateTimeOffset ToLocal(DateTimeOffset instant);
}

public sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    public static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(ToLocal(UtcNow).DateTime);

    public DateTimeOffset ToLocal(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, BusinessTimeZone);
}
