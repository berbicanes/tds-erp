using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Notes;
using Tds.Api.Features.Stock;
using Tds.Api.Features.WorkOrders;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Dashboard;

public sealed record DashboardKpi(
    int ActiveOrders, int OverdueOrders, int DueIn7Days, int UrgentOrders, int GoodToday, int EntriesToday, int ItemsBelowMinimum);

public sealed record TodayEntry(
    Guid Id, string Number, string OperatorName, Guid WorkOrderId, string WorkOrderNumber, string Customer,
    int LaserCutQty, int PressedQty, int GoodQty, int ScrapQty, decimal LaborHours);

public sealed record ProductionTotalsDto(int LaserCutQty, int PressedQty, int GoodQty, int ScrapQty, decimal LaborHours);

public sealed record ChartDay(DateOnly Date, int LaserCutQty, int PressedQty, int GoodQty, int ScrapQty);

/// <summary>
/// Kind tells the UI the text and the link target; Severity = "red" | "amber" (UI §3).
/// Raw values only (item name/grade/thickness, dates): all display formatting stays in web/src/lib/format.ts.
/// </summary>
public sealed record DashboardWarning(
    string Kind, string Severity, Guid EntityId, string Title, string? Detail,
    string? ItemName, string? ItemGrade, decimal? ItemThicknessMm, DateOnly? Date);

public sealed record Section<T>(IReadOnlyList<T> Items, int Total);

public sealed record DashboardDto(
    DateTimeOffset GeneratedAt, DateOnly Today, DashboardKpi Kpi,
    IReadOnlyList<TodayEntry> TodayEntries, ProductionTotalsDto TodayTotals, IReadOnlyList<ChartDay> Chart,
    IReadOnlyList<DashboardWarning> Warnings, Section<WorkOrderDto> OrdersByDue, Section<ItemStockDto> Stock,
    IReadOnlyList<NoteDto> Notes);

/// <summary>UI §3: one call, one DTO, one query per section.</summary>
public static class DashboardEndpoints
{
    public const int OrdersShown = 15;
    public const int StockShown = 10;
    public const int ChartDays = 14;
    public const int DeadlineDays = 30;

    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/dashboard", Get).RequireAuthorization();

    private static async Task<DashboardDto> Get(AppDbContext db, IClock clock, CancellationToken ct)
    {
        var today = clock.Today;
        var now = clock.UtcNow;

        var counts = await KpiCounts(db, today, ct);                    // 1
        var todayEntries = await TodayEntries(db, today, ct);          // 2
        var chart = await Chart(db, today, ct);                        // 3
        var warnings = await Warnings(db, today, ct);                  // 4
        var orders = await OrdersByDue(db, today, ct);                 // 5
        var stock = await StockQueries.ItemStocksAsync(db, ct);        // 6
        var notes = await NoteEndpoints.VisibleAsync(db, now, ct);     // 7

        var activeStock = stock.Where(s => s.IsActive).ToList();
        var kpi = counts with
        {
            // UI §3: only items WITH a minimum; an unused item without a minimum never raises this alarm.
            ItemsBelowMinimum = activeStock.Count(s => s.MinimumStock is not null && s.StockLevel is StockLevel.IspodMinimuma or StockLevel.Nema),
        };
        var totals = new ProductionTotalsDto(todayEntries.Sum(e => e.LaserCutQty), todayEntries.Sum(e => e.PressedQty),
            todayEntries.Sum(e => e.GoodQty), todayEntries.Sum(e => e.ScrapQty), todayEntries.Sum(e => e.LaborHours));

        return new DashboardDto(now, today, kpi, todayEntries, totals, chart, warnings,
            new Section<WorkOrderDto>(orders, counts.ActiveOrders),
            new Section<ItemStockDto>(StockQueries.AttentionFirst(activeStock).Take(StockShown).ToList(), activeStock.Count),
            notes);
    }

    private static async Task<DashboardKpi> KpiCounts(AppDbContext db, DateOnly today, CancellationToken ct) =>
        (await Sql.QueryAsync(db, """
            SELECT
              COUNT(*) FILTER (WHERE active) AS active,
              COUNT(*) FILTER (WHERE active AND due_date < @today) AS overdue,
              COUNT(*) FILTER (WHERE active AND due_date <= @in7) AS due7,
              COUNT(*) FILTER (WHERE active AND priority = 'Hitno') AS urgent,
              (SELECT COALESCE(SUM(good_qty), 0) FROM production_entry WHERE date = @today AND NOT is_voided) AS good_today,
              (SELECT COUNT(*) FROM production_entry WHERE date = @today AND NOT is_voided) AS entries_today
            FROM (SELECT status NOT IN ('Zavrseno', 'Isporuceno', 'Otkazano') AS active, due_date, priority FROM work_order) w
            """,
            r => new DashboardKpi((int)r.Get<long>("active"), (int)r.Get<long>("overdue"), (int)r.Get<long>("due7"),
                (int)r.Get<long>("urgent"), (int)r.Get<long>("good_today"), (int)r.Get<long>("entries_today"), 0),
            ct, new NpgsqlParameter("today", today), new NpgsqlParameter("in7", today.AddDays(7)))).Single();

    private static Task<List<TodayEntry>> TodayEntries(AppDbContext db, DateOnly today, CancellationToken ct) =>
        (from e in db.ProductionEntries
         where e.Date == today && !e.IsVoided
         join o in db.Employees on e.OperatorEmployeeId equals o.Id
         join w in db.WorkOrders on e.WorkOrderId equals w.Id
         orderby e.Number
         select new TodayEntry(e.Id, e.Number, o.FullName, w.Id, w.Number, w.Customer,
             e.LaserCutQty, e.PressedQty, e.GoodQty, e.ScrapQty, e.LaborHours)).ToListAsync(ct);

    /// <summary>
    /// Last 14 working days (Mon–Fri; a weekend day only when it has entries). Days without entries are 0 —
    /// only past days and today are listed, never future ones (UI §3).
    /// </summary>
    private static async Task<List<ChartDay>> Chart(AppDbContext db, DateOnly today, CancellationToken ct)
    {
        var from = today.AddDays(-ChartDays * 2);
        var sums = await db.ProductionEntries
            .Where(e => !e.IsVoided && e.Date >= from && e.Date <= today)
            .GroupBy(e => e.Date)
            .Select(g => new ChartDay(g.Key, g.Sum(e => e.LaserCutQty), g.Sum(e => e.PressedQty), g.Sum(e => e.GoodQty), g.Sum(e => e.ScrapQty)))
            .ToDictionaryAsync(d => d.Date, ct);
        return ChartDaysFor(today, sums);
    }

    /// <summary>Pure day selection (unit tested).</summary>
    public static List<ChartDay> ChartDaysFor(DateOnly today, IReadOnlyDictionary<DateOnly, ChartDay> sums)
    {
        var days = new List<ChartDay>();
        for (var d = today; days.Count < ChartDays && d > today.AddDays(-ChartDays * 2); d = d.AddDays(-1))
        {
            var weekend = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            if (sums.TryGetValue(d, out var day))
                days.Add(day);
            else if (!weekend)
                days.Add(new ChartDay(d, 0, 0, 0, 0));
        }
        days.Reverse();
        return days;
    }

    /// <summary>
    /// One UNION query over everything that needs attention (UI §3, DOMAIN §7.6). Phase 2 sources: lots NaKontroli/
    /// Karantin, Blokirano orders, supplier evaluation, calibration state from the latest calibration (DOMAIN §7.5:
    /// missing date, Neuskladjeno or due ≤ 30 days / past), NC deadlines (not Zatvorena, without a deadline = no warning),
    /// stocktake (≤ 30 days or past).
    /// Order: red first (past deadlines, out of calibration, Blokirano, Karantin), then amber; earliest date first.
    /// </summary>
    private static async Task<List<DashboardWarning>> Warnings(AppDbContext db, DateOnly today, CancellationToken ct)
    {
        var rows = await Sql.QueryAsync(db, """
            SELECT 'LotKarantin' AS kind, l.id, l.lot_number AS title, NULL AS detail,
                   i.name AS item_name, i.grade AS item_grade, i.thickness_mm AS item_thickness,
                   l.received_date AS date, true AS red
            FROM material_lot l JOIN material_item i ON i.id = l.material_item_id
            WHERE NOT l.is_voided AND l.receipt_decision = 'Karantin'
            UNION ALL
            SELECT 'LotNaKontroli', l.id, l.lot_number, NULL, i.name, i.grade, i.thickness_mm, l.received_date, false
            FROM material_lot l JOIN material_item i ON i.id = l.material_item_id
            WHERE NOT l.is_voided AND l.receipt_decision = 'NaKontroli'
            UNION ALL
            SELECT 'OrderBlocked', w.id, w.number, w.customer, NULL, NULL, NULL, w.due_date, true
            FROM work_order w
            WHERE w.quality_approval = 'Blokirano' AND w.status NOT IN ('Zavrseno', 'Isporuceno', 'Otkazano')
            UNION ALL
            SELECT 'SupplierEvaluation', s.id, s.name, NULL, NULL, NULL, NULL, s.next_evaluation_date, s.next_evaluation_date < @today
            FROM supplier s
            WHERE s.approval_status <> 'Blokiran' AND s.next_evaluation_date <= @limit
            UNION ALL
            SELECT CASE WHEN lc.id IS NULL OR lc.next_calibration_date IS NULL THEN 'CalibrationMissing'
                        WHEN lc.result = 'Neuskladjeno' THEN 'CalibrationFailed'
                        ELSE 'Calibration' END,
                   e.id, e.code, e.name, NULL, NULL, NULL, lc.next_calibration_date,
                   lc.id IS NULL OR lc.next_calibration_date IS NULL OR lc.result = 'Neuskladjeno' OR lc.next_calibration_date < @today
            FROM equipment e
            LEFT JOIN LATERAL (
                SELECT c.id, c.next_calibration_date, c.result
                FROM equipment_calibration c
                WHERE c.equipment_id = e.id AND NOT c.is_voided
                ORDER BY c.calibration_date DESC, c.created_at DESC
                LIMIT 1) lc ON true
            WHERE e.is_active AND e.kind = 'MjernoSredstvo'
              AND (lc.id IS NULL OR lc.next_calibration_date IS NULL OR lc.result = 'Neuskladjeno' OR lc.next_calibration_date <= @limit)
            UNION ALL
            SELECT 'NcDue', n.id, n.number, left(n.description, 120), NULL, NULL, NULL, n.due_date, n.due_date < @today
            FROM non_conformity n
            WHERE NOT n.is_voided AND n.status <> 'Zatvorena' AND n.due_date <= @limit
            UNION ALL
            SELECT 'Stocktake', i.id, i.name, NULL, i.name, i.grade, i.thickness_mm, i.next_stocktake_date, i.next_stocktake_date < @today
            FROM material_item i
            WHERE i.is_active AND i.next_stocktake_date <= @limit
            """,
            r => new DashboardWarning(r.Get<string>("kind"), r.Get<bool>("red") ? "red" : "amber", r.Get<Guid>("id"),
                r.Get<string>("title"), r.NullableString("detail"), r.NullableString("item_name"), r.NullableString("item_grade"),
                r.Nullable<decimal>("item_thickness"), r.Nullable<DateOnly>("date")),
            ct, new NpgsqlParameter("today", today), new NpgsqlParameter("limit", today.AddDays(DeadlineDays)));
        return rows.OrderBy(w => w.Severity == "red" ? 0 : 1).ThenBy(w => w.Date).ThenBy(w => w.Title).ToList();
    }

    private static async Task<List<WorkOrderDto>> OrdersByDue(AppDbContext db, DateOnly today, CancellationToken ct) =>
        (await WorkOrderQueries.Rows(db)
            .Where(w => !WorkOrder.FinalStatuses.Contains(w.Status))
            .OrderBy(w => w.DueDate).ThenBy(w => w.Number)
            .Take(OrdersShown)
            .ToListAsync(ct))
        .Select(r => WorkOrderDto.From(r, today)).ToList();
}
