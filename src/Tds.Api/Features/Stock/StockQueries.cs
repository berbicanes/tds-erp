using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;

namespace Tds.Api.Features.Stock;

/// <summary>Stock per lot, computed only from stock_movement (golden rule 1).</summary>
public sealed record LotStock(Guid LotId, Guid MaterialItemId, ReceiptDecision Decision, decimal OnHand, decimal Reserved)
{
    public decimal Available => OnHand - Reserved;
}

public sealed record ItemStockDto
{
    public required Guid MaterialItemId { get; init; }
    public required string Name { get; init; }
    public string? Grade { get; init; }
    public decimal? ThicknessMm { get; init; }
    public required Unit Unit { get; init; }
    public decimal? MinimumStock { get; init; }
    /// <summary>Over Prihvacen lots only.</summary>
    public required decimal OnHand { get; init; }
    public required decimal Reserved { get; init; }
    public required decimal Available { get; init; }
    public required StockLevel StockLevel { get; init; }
    /// <summary>"Na kontroli / karantin": OnHand of NaKontroli + Karantin lots; not part of Available.</summary>
    public required decimal OnHoldQty { get; init; }
    public required bool IsActive { get; init; }
}

public static class StockQueries
{
    /// <summary>OnHand, effective reservations and Available for every non-voided lot (optionally one item).</summary>
    public static async Task<List<LotStock>> LotStocksAsync(AppDbContext db, Guid? materialItemId, CancellationToken ct, Guid? lotId = null)
    {
        var lots = db.MaterialLots.Where(l => !l.IsVoided
            && (materialItemId == null || l.MaterialItemId == materialItemId)
            && (lotId == null || l.Id == lotId));
        var rows = await lots.Select(l => new
        {
            l.Id,
            l.MaterialItemId,
            l.ReceiptDecision,
            OnHand = db.StockMovements.Where(m => m.MaterialLotId == l.Id && !m.IsVoided).Sum(m => (decimal?)m.Quantity) ?? 0m,
        }).ToListAsync(ct);

        var reservations = await Reservations.EffectiveByLotAsync(db, rows.Select(r => r.Id).ToList(), ct);
        return rows.Select(r => new LotStock(r.Id, r.MaterialItemId, r.ReceiptDecision, r.OnHand, reservations.GetValueOrDefault(r.Id))).ToList();
    }

    /// <summary>Per item (DOMAIN §2.4): OnHand/Reserved/Available/level over Prihvacen lots; OnHold separately.</summary>
    public static async Task<List<ItemStockDto>> ItemStocksAsync(AppDbContext db, CancellationToken ct)
    {
        var items = await db.MaterialItems.ToListAsync(ct);
        var lots = await LotStocksAsync(db, null, ct);
        return items.Select(i =>
        {
            var accepted = lots.Where(l => l.MaterialItemId == i.Id && l.Decision == ReceiptDecision.Prihvacen).ToList();
            var onHand = accepted.Sum(l => l.OnHand);
            var reserved = accepted.Sum(l => l.Reserved);
            var available = onHand - reserved;
            return new ItemStockDto
            {
                MaterialItemId = i.Id, Name = i.Name, Grade = i.Grade, ThicknessMm = i.ThicknessMm, Unit = i.Unit,
                MinimumStock = i.MinimumStock, OnHand = onHand, Reserved = reserved, Available = available,
                StockLevel = StockLevels.Level(available, i.MinimumStock),
                OnHoldQty = lots.Where(l => l.MaterialItemId == i.Id && l.Decision is ReceiptDecision.NaKontroli or ReceiptDecision.Karantin).Sum(l => l.OnHand),
                IsActive = i.IsActive,
            };
        }).ToList();
    }
}

/// <summary>Reservations from WorkOrderMaterial (DOMAIN §2.3).</summary>
public static class Reservations
{
    /// <summary>One link with what was already issued from that lot for that order.</summary>
    public sealed record LinkUsage
    {
        public required Guid LinkId { get; init; }
        public required Guid WorkOrderId { get; init; }
        public required Guid MaterialLotId { get; init; }
        public required decimal ReservedQty { get; init; }
        public required decimal IssuedQty { get; init; }
        public required bool OrderIsActive { get; init; }

        /// <summary>max(0, ReservedQty − issued); 0 for finished orders (Zavrseno, Isporuceno, Otkazano).</summary>
        public decimal Effective => OrderIsActive ? StockLevels.EffectiveReservation(ReservedQty, IssuedQty) : 0m;
    }

    public static IQueryable<LinkUsage> Usage(AppDbContext db) =>
        from l in db.WorkOrderMaterials
        join w in db.WorkOrders on l.WorkOrderId equals w.Id
        select new LinkUsage
        {
            LinkId = l.Id, WorkOrderId = l.WorkOrderId, MaterialLotId = l.MaterialLotId, ReservedQty = l.ReservedQty,
            IssuedQty = -(db.StockMovements.Where(m => m.MaterialLotId == l.MaterialLotId && m.WorkOrderId == l.WorkOrderId
                && m.Type == StockMovementType.Izdavanje && !m.IsVoided).Sum(m => (decimal?)m.Quantity) ?? 0m),
            OrderIsActive = !WorkOrder.FinalStatuses.Contains(w.Status),
        };

    public static async Task<Dictionary<Guid, decimal>> EffectiveByLotAsync(AppDbContext db, IReadOnlyCollection<Guid> lotIds, CancellationToken ct)
    {
        var links = await Usage(db).Where(u => lotIds.Contains(u.MaterialLotId)).ToListAsync(ct);
        return links.GroupBy(l => l.MaterialLotId).ToDictionary(g => g.Key, g => g.Sum(l => l.Effective));
    }
}
