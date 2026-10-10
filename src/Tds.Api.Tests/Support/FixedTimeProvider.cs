namespace Tds.Api.Tests.Support;

public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => Now;
}
