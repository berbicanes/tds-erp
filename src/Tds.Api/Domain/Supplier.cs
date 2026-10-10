namespace Tds.Api.Domain;

/// <summary>
/// DATA_MODEL §4. No IsActive and never deleted: a supplier no longer used goes to Blokiran
/// with the reason in Notes (DOMAIN §7.2). Certificates (files) come in Phase 3.
/// </summary>
public class Supplier : Entity
{
    public const int NameMax = 150;
    public const int MaterialOrServiceMax = 300;
    public const int TextMax = 2000;

    public required string Name { get; set; }
    public string? MaterialOrService { get; set; }
    public SupplierApprovalStatus ApprovalStatus { get; set; } = SupplierApprovalStatus.UProcjeni;
    public DateOnly? InitialEvaluationDate { get; set; }
    public DateOnly? NextEvaluationDate { get; set; }
    public string? EvaluationCriteria { get; set; }
    public Guid? ResponsibleEmployeeId { get; set; }
    public string? Notes { get; set; }
}
