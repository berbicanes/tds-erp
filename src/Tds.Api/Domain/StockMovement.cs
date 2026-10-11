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

/// <summary>Pure stock arithmetic and manual movements (DOMAIN §2.1, §2.2).</summary>
public static class Stock
{
    public static decimal OnHand(IEnumerable<StockMovement> movements) =>
        movements.Where(m => !m.IsVoided).Sum(m => m.Quantity);

    /// <summary>
    /// DOMAIN §2.6: stock is unknown only for a lot imported from Notion (no received quantity) that has no
    /// non-voided movement yet. Every lot received in the application has a known stock.
    /// </summary>
    public static bool IsKnown(decimal? receivedQty, bool hasMovements) => receivedQty is not null || hasMovements;

    public static string UnknownMessage(string lotNumber) =>
        $"Stanje lota {lotNumber} nije poznato. Prvo upiši stvarno stanje korekcijom.";

    /// <summary>
    /// Korekcija: ± quantity, reason required, result never below 0, date not in the future. On a lot with unknown
    /// stock (§2.6) it records the real stock, which may be 0.
    /// </summary>
    public static StockMovement Correction(MaterialLot lot, decimal quantity, string? reason, DateOnly date, decimal onHand, DateOnly today, bool stockKnown = true)
    {
        CheckManual(lot, reason, date, today);
        if (quantity == 0 && stockKnown)
            throw new DomainException("Korekcija ne može biti 0.");
        if (onHand + quantity < 0)
            throw new DomainException($"Korekcija bi spustila stanje lota {lot.LotNumber} ispod 0 (na stanju je {Format(onHand)}).");
        return New(lot, StockMovementType.Korekcija, quantity, date, reason!);
    }

    /// <summary>Otpis: quantity &gt; 0 and ≤ OnHand, reason required, date not in the future.</summary>
    public static StockMovement WriteOff(MaterialLot lot, decimal quantity, string? reason, DateOnly date, decimal onHand, DateOnly today, bool stockKnown = true)
    {
        CheckManual(lot, reason, date, today);
        if (!stockKnown)
            throw new DomainException(UnknownMessage(lot.LotNumber));
        if (quantity <= 0)
            throw new DomainException("Količina otpisa mora biti veća od 0.");
        if (quantity > onHand)
            throw new DomainException($"Na lotu {lot.LotNumber} ima samo {Format(onHand)}.");
        return New(lot, StockMovementType.Otpis, -quantity, date, reason!);
    }

    private static void CheckManual(MaterialLot lot, string? reason, DateOnly date, DateOnly today)
    {
        if (lot.IsVoided)
            throw new DomainException($"Lot {lot.LotNumber} je poništen.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Razlog je obavezan.");
        if (date > today)
            throw new DomainException("Datum pokreta ne može biti u budućnosti.");
    }

    private static StockMovement New(MaterialLot lot, StockMovementType type, decimal qty, DateOnly date, string reason) =>
        new() { MaterialLotId = lot.Id, Type = type, Quantity = qty, Date = date, Reason = reason.Trim() };

    /// <summary>"1.234,5" — Bosnian number format for messages.</summary>
    public static string Format(decimal value) =>
        value.ToString("#,##0.###", System.Globalization.CultureInfo.GetCultureInfo("bs-Latn-BA"));
}
