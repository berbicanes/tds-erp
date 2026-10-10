namespace Tds.Api.Domain;

/// <summary>DATA_MODEL §1. Hall worker; may or may not have a login (UserId).</summary>
public class Employee : Entity
{
    public const int FullNameMax = 120;
    public const int PositionMax = 80;

    public required string FullName { get; set; }
    public string? Position { get; set; }

    /// <summary>Inactive employees are not offered in pickers but stay in history.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Login account; null = no login.</summary>
    public Guid? UserId { get; set; }
}
