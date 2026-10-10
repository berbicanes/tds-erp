namespace Tds.Api.Infrastructure;

/// <summary>
/// Per-request extra audit info. An endpoint sets <see cref="Reason"/> (gate override, reopening a
/// cancelled order) and AuditInterceptor writes it into ChangesJson as "reason" (DOMAIN §4).
/// </summary>
public sealed class AuditContext
{
    public string? Reason { get; set; }
}
