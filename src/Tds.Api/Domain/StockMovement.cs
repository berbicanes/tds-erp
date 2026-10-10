namespace Tds.Api.Domain;

/// <summary>
/// DATA_MODEL §7. The only source of stock (golden rule 1): OnHand(lot) = Σ Quantity of non-voided movements.
/// Sign: Prijem +, Izdavanje −, Otpis −, Korekcija ±. Never updated; a mistake is voided or corrected by a new movement.
/// </summary>
public class StockMovement : VoidableEntity
{
    public const int ReasonMax = 300;

    public Guid MaterialLotId { get; init; }
    public StockMovementType Type { get; init; }
    public decimal Quantity { get; init; }
    public DateOnly Date { get; init; }
    public Guid? ProductionEntryId { get; init; }
    public Guid? WorkOrderId { get; init; }
    public string? Reason { get; init; }
}

/// <summary>Pure stock arithmetic (DOMAIN §2.1).</summary>
public static class Stock
{
    public static decimal OnHand(IEnumerable<StockMovement> movements) =>
        movements.Where(m => !m.IsVoided).Sum(m => m.Quantity);
}
