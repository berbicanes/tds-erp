namespace Tds.Api.Domain;

/// <summary>
/// DATA_MODEL §8. Lot ↔ order link with a reservation. Never removed (traceability, DOMAIN §2.3);
/// the reservation can go down to 0.
/// </summary>
public class WorkOrderMaterial : Entity
{
    public Guid WorkOrderId { get; init; }
    public Guid MaterialLotId { get; init; }
    public decimal ReservedQty { get; private set; }

    /// <summary>Lots that may receive a new or larger reservation (DOMAIN §2.3).</summary>
    public static bool CanReserveFrom(ReceiptDecision d) => d is ReceiptDecision.NaKontroli or ReceiptDecision.Prihvacen;

    public static WorkOrderMaterial Link(Guid workOrderId, MaterialLot lot, decimal reservedQty)
    {
        var link = new WorkOrderMaterial { WorkOrderId = workOrderId, MaterialLotId = lot.Id };
        link.SetReservation(lot, reservedQty);
        return link;
    }

    /// <summary>Automatic link with 0 reservation when a production entry uses an unlinked lot (DOMAIN §5.2).</summary>
    public static WorkOrderMaterial TraceOnly(Guid workOrderId, Guid lotId) => new() { WorkOrderId = workOrderId, MaterialLotId = lotId };

    public void SetReservation(MaterialLot lot, decimal reservedQty)
    {
        if (lot.Id != MaterialLotId)
            throw new InvalidOperationException("Wrong lot for this link.");
        if (reservedQty < 0)
            throw new DomainException("Rezervacija ne može biti negativna.");
        if (lot.IsVoided)
            throw new DomainException($"Lot {lot.LotNumber} je poništen.");
        if (reservedQty > ReservedQty && !CanReserveFrom(lot.ReceiptDecision))
            throw new DomainException($"Lot {lot.LotNumber} ima odluku {DisplayNames.Of(lot.ReceiptDecision)} i ne može se rezervisati.");
        ReservedQty = reservedQty;
    }
}
