namespace Tds.Api.Domain;

public abstract class VoidableEntity : Entity, IVoidable
{
    public const int VoidedReasonMax = 300;

    public bool IsVoided { get; private set; }
    public string? VoidedReason { get; private set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }

    public void Void(string reason)
    {
        if (IsVoided)
            throw new DomainException("Zapis je već poništen.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Razlog poništavanja je obavezan.");

        IsVoided = true;
        VoidedReason = reason.Trim();
    }
}
