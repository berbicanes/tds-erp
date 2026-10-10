namespace Tds.Api.Domain;

public enum AuditAction
{
    Create,
    Update,
    Void,
}

/// <summary>
/// DATA_MODEL §15. Exception to the base entity: has an Id but no audit fields and is not audited itself.
/// </summary>
public class AuditLog
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string EntityType { get; init; }
    public required Guid EntityId { get; init; }
    public required AuditAction Action { get; init; }

    /// <summary><c>{"Field": [old, new]}</c></summary>
    public required string ChangesJson { get; init; }

    /// <summary>null = system action.</summary>
    public Guid? UserId { get; init; }

    public required DateTimeOffset At { get; init; }
}
