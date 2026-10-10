namespace Tds.Api.Domain;

/// <summary>
/// Base for every audited entity (DATA_MODEL: "Svi entiteti imaju i …").
/// Audit fields are filled by <c>AuditInterceptor</c>, never by endpoints.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>User id; null = system action (seed, import, migration).</summary>
    public Guid? CreatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>User id; null = system action.</summary>
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Optimistic concurrency token = Postgres system column <c>xmin</c> (no real column).
    /// Clients send it back on update; a mismatch is a 409 (CLAUDE.md "Istovremene izmjene").
    /// </summary>
    public uint Version { get; set; }
}
