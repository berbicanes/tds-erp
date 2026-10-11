namespace Tds.Api.Domain;

/// <summary>IZV "Nivo zalihe" (DATA_MODEL §5, DOMAIN §2.4). Derived, never stored.</summary>
public enum StockLevel { Nema, IspodMinimuma, Nisko, Ok, NijeDefinisano }

/// <summary>Pure stock formulas (DOMAIN §2.3, §2.4).</summary>
public static class StockLevels
{
    /// <summary>Effective reservation of one lot for one order: max(0, ReservedQty − issued from that lot for that order).</summary>
    public static decimal EffectiveReservation(decimal reservedQty, decimal issuedQty) => Math.Max(0m, reservedQty - issuedQty);

    /// <summary>
    /// Rules in the order written in DOMAIN §2.4: Available ≤ 0 → Nema; no minimum → Nije definisano;
    /// below minimum → Ispod minimuma; below 1.5 × minimum → Nisko; otherwise OK.
    /// </summary>
    public static StockLevel Level(decimal available, decimal? minimumStock)
    {
        if (available <= 0)
            return StockLevel.Nema;
        if (minimumStock is not { } min)
            return StockLevel.NijeDefinisano;
        if (available < min)
            return StockLevel.IspodMinimuma;
        if (available < 1.5m * min)
            return StockLevel.Nisko;
        return StockLevel.Ok;
    }

    /// <summary>
    /// DOMAIN §2.4 / §2.6: with lots of unknown stock the real level is only known when it cannot get better or does
    /// not depend on the amount (a lot without quantity can only add stock): OK and Nije definisano stay, the
    /// other levels are unknown (null, UI "—").
    /// </summary>
    public static StockLevel? Level(decimal available, decimal? minimumStock, int unknownLots)
    {
        var level = Level(available, minimumStock);
        return unknownLots == 0 || level is StockLevel.Ok or StockLevel.NijeDefinisano ? level : null;
    }
}
