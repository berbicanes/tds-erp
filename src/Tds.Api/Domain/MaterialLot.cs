namespace Tds.Api.Domain;

/// <summary>DATA_MODEL §6. Incoming lot (heat number). Stock is never stored here — see <see cref="StockMovement"/>.</summary>
public class MaterialLot : VoidableEntity
{
    public const int LotNumberMax = 60;
    public const int DimensionsMax = 200;
    public const int StorageLocationMax = 60;

    public required string LotNumber { get; set; }
    public Guid MaterialItemId { get; init; }
    public Guid SupplierId { get; set; }
    public DateOnly ReceivedDate { get; init; }
    public string? DimensionsDescription { get; set; }

    /// <summary>Immutable after creation; a receiving mistake is fixed with a Korekcija (DOMAIN §2.2).</summary>
    public decimal ReceivedQty { get; init; }

    public ReceiptDecision ReceiptDecision { get; private set; } = ReceiptDecision.NaKontroli;
    public Guid? InspectedByEmployeeId { get; private set; }
    public string? StorageLocation { get; set; }
    public Guid? CertificateFileId { get; set; }

    /// <summary>DOMAIN §2.5a.</summary>
    public static readonly IReadOnlyDictionary<ReceiptDecision, ReceiptDecision[]> AllowedDecisions =
        new Dictionary<ReceiptDecision, ReceiptDecision[]>
        {
            [ReceiptDecision.NaKontroli] = [ReceiptDecision.Prihvacen, ReceiptDecision.Karantin, ReceiptDecision.Odbijen],
            [ReceiptDecision.Karantin] = [ReceiptDecision.Prihvacen, ReceiptDecision.Odbijen],
            [ReceiptDecision.Prihvacen] = [ReceiptDecision.Karantin],
            [ReceiptDecision.Odbijen] = [],
        };

    /// <summary>
    /// Receives a lot (DOMAIN §2.2, §7.2): creates the lot and its Prijem movement.
    /// A blocked supplier is rejected; the decision starts as NaKontroli.
    /// </summary>
    public static (MaterialLot Lot, StockMovement Receipt) Receive(
        string lotNumber, Guid materialItemId, Guid supplierId, SupplierApprovalStatus supplierStatus,
        DateOnly receivedDate, decimal receivedQty, DateOnly today)
    {
        if (supplierStatus == SupplierApprovalStatus.Blokiran)
            throw new DomainException("Dobavljač je blokiran.");
        if (receivedQty <= 0)
            throw new DomainException("Primljena količina mora biti veća od 0.");
        if (receivedDate > today)
            throw new DomainException("Datum prijema ne može biti u budućnosti.");

        var lot = new MaterialLot
        {
            LotNumber = lotNumber.Trim(),
            MaterialItemId = materialItemId,
            SupplierId = supplierId,
            ReceivedDate = receivedDate,
            ReceivedQty = receivedQty,
        };
        var receipt = new StockMovement
        {
            MaterialLotId = lot.Id,
            Type = StockMovementType.Prijem,
            Quantity = receivedQty,
            Date = receivedDate,
        };
        return (lot, receipt);
    }

    /// <summary>
    /// Changes the receipt decision (DOMAIN §2.5a). Moving to Odbijen writes off the whole remaining stock:
    /// returns that Otpis movement (to be saved in the same transaction), otherwise null.
    /// </summary>
    public StockMovement? Decide(ReceiptDecision to, Guid? inspectedByEmployeeId, string? reason, decimal onHand, DateOnly today)
    {
        if (IsVoided)
            throw new DomainException($"Lot {LotNumber} je poništen.");
        if (to == ReceiptDecision)
            throw new DomainException($"Lot {LotNumber} već ima tu odluku.");
        if (!AllowedDecisions[ReceiptDecision].Contains(to))
            throw new DomainException(ReceiptDecision == ReceiptDecision.Odbijen
                ? $"Lot {LotNumber} je odbijen i odluka se više ne može mijenjati."
                : $"Odluka prijema se ne može promijeniti iz \"{DisplayNames.Of(ReceiptDecision)}\" u \"{DisplayNames.Of(to)}\".");
        if (to != ReceiptDecision.NaKontroli && inspectedByEmployeeId is null)
            throw new DomainException("Odaberi ko je kontrolisao lot.");
        if (to == ReceiptDecision.Odbijen && string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Razlog odbijanja je obavezan.");

        ReceiptDecision = to;
        InspectedByEmployeeId = inspectedByEmployeeId;

        if (to != ReceiptDecision.Odbijen || onHand <= 0)
            return null;
        return new StockMovement
        {
            MaterialLotId = Id,
            Type = StockMovementType.Otpis,
            Quantity = -onHand,
            Date = today,
            Reason = reason!.Trim(),
        };
    }

    /// <summary>Non-blocking warnings shown after receiving or deciding (DOMAIN §7.2).</summary>
    public IReadOnlyList<string> Warnings(string supplierName, SupplierApprovalStatus supplierStatus)
    {
        var warnings = new List<string>();
        if (supplierStatus is SupplierApprovalStatus.UProcjeni or SupplierApprovalStatus.UslovnoOdobren)
            warnings.Add($"Dobavljač {supplierName} nije potpuno odobren ({DisplayNames.Of(supplierStatus)}).");
        if (ReceiptDecision == ReceiptDecision.Prihvacen && CertificateFileId is null)
            warnings.Add($"Lot {LotNumber} je prihvaćen bez certifikata materijala.");
        return warnings;
    }
}
