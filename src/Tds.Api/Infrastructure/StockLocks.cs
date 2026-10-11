using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;

namespace Tds.Api.Infrastructure;

public static class StockLocks
{
    /// <summary>
    /// Locks a lot row until the current transaction ends, so two writers cannot both read the same OnHand
    /// and write movements that together break a rule (e.g. two write-offs below 0).
    /// </summary>
    public static async Task LockLotAsync(this AppDbContext db, Guid lotId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Lot locks need a transaction.");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM material_lot WHERE id = {lotId} FOR UPDATE", ct);
    }

    /// <summary>DOMAIN §2.6: false only for an imported lot without received quantity and without movements.</summary>
    public static async Task<bool> StockKnownAsync(this AppDbContext db, Guid lotId, CancellationToken ct) =>
        await db.MaterialLots.Where(l => l.Id == lotId).Select(l => l.ReceivedQty).SingleOrDefaultAsync(ct) is not null
        || await db.StockMovements.AnyAsync(m => m.MaterialLotId == lotId && !m.IsVoided, ct);

    /// <summary>OnHand of a lot straight from the movements (DOMAIN §2.1).</summary>
    public static Task<decimal> OnHandAsync(this AppDbContext db, Guid lotId, CancellationToken ct) =>
        db.StockMovements.Where(m => m.MaterialLotId == lotId && !m.IsVoided).SumAsync(m => m.Quantity, ct);
}
