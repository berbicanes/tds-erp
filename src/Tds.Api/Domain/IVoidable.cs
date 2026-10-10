namespace Tds.Api.Domain;

/// <summary>
/// QMS records (🔒 in DATA_MODEL) are never deleted, only voided with a reason (golden rule 4).
/// <see cref="VoidedAt"/> and <see cref="VoidedBy"/> are set by <c>AuditInterceptor</c>.
/// </summary>
public interface IVoidable
{
    bool IsVoided { get; }
    string? VoidedReason { get; }
    DateTimeOffset? VoidedAt { get; set; }
    Guid? VoidedBy { get; set; }
}
