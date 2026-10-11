namespace Tds.Api.Domain;

/// <summary>
/// M:N file links on QMS records (DOMAIN §7.7). Technical rows without audit fields; adding/removing is
/// written to the parent's AuditLog as one set field (see <see cref="IAuditedLink"/>). Removing a link
/// never deletes the file itself.
/// </summary>
public abstract class FileLink : IAuditedLink
{
    public Guid FileId { get; init; }
    protected abstract Guid Parent { get; }
    protected abstract string ParentTypeName { get; }
    protected abstract string FieldName { get; }

    Guid IAuditedLink.ParentId => Parent;
    string IAuditedLink.ParentType => ParentTypeName;
    string IAuditedLink.Field => FieldName;
    Guid IAuditedLink.ValueId => FileId;
}

public class QualityCheckEvidence : FileLink
{
    public Guid QualityCheckId { get; init; }
    protected override Guid Parent => QualityCheckId;
    protected override string ParentTypeName => nameof(QualityCheck);
    protected override string FieldName => "EvidenceFileIds";
}

public class NonConformityEvidence : FileLink
{
    public Guid NonConformityId { get; init; }
    protected override Guid Parent => NonConformityId;
    protected override string ParentTypeName => nameof(NonConformity);
    protected override string FieldName => "EvidenceFileIds";
}

public class SupplierCertificate : FileLink
{
    public Guid SupplierId { get; init; }
    protected override Guid Parent => SupplierId;
    protected override string ParentTypeName => nameof(Supplier);
    protected override string FieldName => "CertificateFileIds";
}
