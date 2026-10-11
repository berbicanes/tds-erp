using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Stock;

/// <summary>Stock per lot, computed only from stock_movement (golden rule 1).</summary>
/// <param name="StockKnown">False for an imported lot without received quantity and movements (DOMAIN §2.6).</param>
public sealed record LotStock(Guid LotId, Guid MaterialItemId, ReceiptDecision Decision, decimal OnHand, decimal Reserved, bool StockKnown)
{
    public decimal Available => OnHand - Reserved;

    /// <summary>What the UI shows: null ("—") when the stock is unknown, never 0.</summary>
    public decimal? KnownOnHand => StockKnown ? OnHand : null;
    public decimal? KnownAvailable => StockKnown ? Available : null;
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
    /// <summary>Lots (not voided, not Odbijen) with unknown stock (DOMAIN §2.6); they are left out of every sum above.</summary>
    public required int UnknownLotCount { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>
/// DOMAIN §2.1–2.4 in one SQL statement per question. The only stock arithmetic in the system:
/// OnHand = Σ non-voided movements; effective reservation = max(0, reserved − issued for that order),
/// only for orders not in Zavrseno/Isporuceno/Otkazano. The level itself comes from <see cref="StockLevels"/>.
/// </summary>
public static class StockQueries
{
    private const string LotCte = """
        lot_onhand AS (
            SELECT l.id, l.material_item_id, l.receipt_decision,
                   COALESCE(SUM(m.quantity) FILTER (WHERE NOT m.is_voided), 0) AS on_hand,
                   l.received_qty IS NOT NULL OR COUNT(m.id) FILTER (WHERE NOT m.is_voided) > 0 AS stock_known
            FROM material_lot l
            LEFT JOIN stock_movement m ON m.material_lot_id = l.id
            WHERE NOT l.is_voided
            GROUP BY l.id),
        issued AS (
            SELECT material_lot_id, work_order_id, -SUM(quantity) AS qty
            FROM stock_movement
            WHERE type = 'Izdavanje' AND NOT is_voided
            GROUP BY material_lot_id, work_order_id),
        lot_reserved AS (
            SELECT wm.material_lot_id, SUM(GREATEST(0, wm.reserved_qty - COALESCE(i.qty, 0))) AS reserved
            FROM work_order_material wm
            JOIN work_order w ON w.id = wm.work_order_id AND w.status NOT IN ('Zavrseno', 'Isporuceno', 'Otkazano')
            LEFT JOIN issued i ON i.material_lot_id = wm.material_lot_id AND i.work_order_id = wm.work_order_id
            GROUP BY wm.material_lot_id)
        """;

    public static async Task<List<LotStock>> LotStocksAsync(AppDbContext db, Guid? materialItemId, CancellationToken ct, Guid? lotId = null) =>
        await Sql.QueryAsync(db, $"""
            WITH {LotCte}
            SELECT lo.id, lo.material_item_id, lo.receipt_decision, lo.on_hand, COALESCE(lr.reserved, 0) AS reserved, lo.stock_known
            FROM lot_onhand lo
            LEFT JOIN lot_reserved lr ON lr.material_lot_id = lo.id
            WHERE (@item::uuid IS NULL OR lo.material_item_id = @item) AND (@lot::uuid IS NULL OR lo.id = @lot)
            """,
            r => new LotStock(r.Get<Guid>("id"), r.Get<Guid>("material_item_id"), Enum.Parse<ReceiptDecision>(r.Get<string>("receipt_decision")),
                r.Get<decimal>("on_hand"), r.Get<decimal>("reserved"), r.Get<bool>("stock_known")),
            ct,
            new NpgsqlParameter("item", (object?)materialItemId ?? DBNull.Value) { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Uuid },
            new NpgsqlParameter("lot", (object?)lotId ?? DBNull.Value) { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Uuid });

    /// <summary>Per item (DOMAIN §2.4): OnHand/Reserved/Available/level over Prihvacen lots; OnHold separately. One query.</summary>
    public static async Task<List<ItemStockDto>> ItemStocksAsync(AppDbContext db, CancellationToken ct) =>
        await Sql.QueryAsync(db, $"""
            WITH {LotCte}
            SELECT mi.id, mi.name, mi.grade, mi.thickness_mm, mi.unit, mi.minimum_stock, mi.is_active,
                   COALESCE(SUM(lo.on_hand) FILTER (WHERE lo.stock_known AND lo.receipt_decision = 'Prihvacen'), 0) AS on_hand,
                   COALESCE(SUM(lr.reserved) FILTER (WHERE lo.stock_known AND lo.receipt_decision = 'Prihvacen'), 0) AS reserved,
                   COALESCE(SUM(lo.on_hand) FILTER (WHERE lo.stock_known AND lo.receipt_decision IN ('NaKontroli', 'Karantin')), 0) AS on_hold,
                   COUNT(lo.id) FILTER (WHERE NOT lo.stock_known AND lo.receipt_decision <> 'Odbijen') AS unknown_lots
            FROM material_item mi
            LEFT JOIN lot_onhand lo ON lo.material_item_id = mi.id
            LEFT JOIN lot_reserved lr ON lr.material_lot_id = lo.id
            GROUP BY mi.id
            """,
            r =>
            {
                var onHand = r.Get<decimal>("on_hand");
                var reserved = r.Get<decimal>("reserved");
                var minimum = r.Nullable<decimal>("minimum_stock");
                return new ItemStockDto
                {
                    MaterialItemId = r.Get<Guid>("id"), Name = r.Get<string>("name"), Grade = r.NullableString("grade"),
                    ThicknessMm = r.Nullable<decimal>("thickness_mm"), Unit = Enum.Parse<Unit>(r.Get<string>("unit")),
                    MinimumStock = minimum, OnHand = onHand, Reserved = reserved, Available = onHand - reserved,
                    StockLevel = StockLevels.Level(onHand - reserved, minimum), OnHoldQty = r.Get<decimal>("on_hold"),
                    UnknownLotCount = (int)r.Get<long>("unknown_lots"),
                    IsActive = r.Get<bool>("is_active"),
                };
            }, ct);

    /// <summary>Attention first (Nema, Ispod minimuma, Nisko, Nije definisano), then OK; by name within (UI §3).</summary>
    public static IEnumerable<ItemStockDto> AttentionFirst(IEnumerable<ItemStockDto> rows) =>
        rows.OrderBy(r => r.StockLevel == StockLevel.Ok ? 1 : 0).ThenBy(r => r.StockLevel).ThenBy(r => r.Name).ThenBy(r => r.Grade).ThenBy(r => r.ThicknessMm);
}

/// <summary>Reservation usage per link (detail screens). Same formula as the SQL above.</summary>
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
}
