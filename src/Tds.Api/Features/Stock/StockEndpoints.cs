using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Stock;

public static class StockEndpoints
{
    public static void MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/stock").RequireAuthorization();
        g.MapGet("/items", Items);
        g.MapGet("/lots/{id:guid}", Lot);
    }

    /// <summary>
    /// Items with derived stock. Default order: levels that need attention first (Nema, Ispod minimuma, Nisko),
    /// then the rest by name (UI §3 "Zalihe"). Computed in memory: the workshop has hundreds of lots, not millions.
    /// </summary>
    private static async Task<PagedResult<ItemStockDto>> Items(
        AppDbContext db, [AsParameters] ListQuery list, StockLevel[]? stockLevel, bool? includeInactive, bool? hasMinimum, bool? complete, CancellationToken ct)
    {
        IEnumerable<ItemStockDto> rows = await StockQueries.ItemStocksAsync(db, ct);
        if (includeInactive != true)
            rows = rows.Where(r => r.IsActive);
        if (list.Search is { } s)
            rows = rows.Where(r => r.Name.Contains(s, StringComparison.OrdinalIgnoreCase) || (r.Grade?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        if (stockLevel is { Length: > 0 })
            rows = rows.Where(r => r.StockLevel is { } level && stockLevel.Contains(level));
        // Dashboard tile "Materijal ispod minimuma" counts only items with a minimum (UI §3); its link uses this.
        if (hasMinimum is { } hm)
            rows = rows.Where(r => (r.MinimumStock is not null) == hm);
        // complete=true: only items whose stock is fully known (DOMAIN §2.6), as counted by the dashboard tile.
        if (complete is { } c)
            rows = rows.Where(r => (r.UnknownLotCount == 0) == c);

        var sorted = (list.Sort?.TrimStart('-'), list.Sort?.StartsWith('-') == true) switch
        {
            ("name", var d) => d ? rows.OrderByDescending(r => r.Name) : rows.OrderBy(r => r.Name),
            ("available", var d) => d ? rows.OrderByDescending(r => r.Available) : rows.OrderBy(r => r.Available),
            ("onHand", var d) => d ? rows.OrderByDescending(r => r.OnHand) : rows.OrderBy(r => r.OnHand),
            _ => StockQueries.AttentionFirst(rows),
        };
        var all = sorted.ToList();
        var page = all.Skip((list.SafePage - 1) * list.SafePageSize).Take(list.SafePageSize).ToList();
        return new PagedResult<ItemStockDto>(page, all.Count, list.SafePage, list.SafePageSize);
    }

    private static async Task<IResult> Lot(Guid id, AppDbContext db, CancellationToken ct)
    {
        var lot = (await StockQueries.LotStocksAsync(db, null, ct, lotId: id)).SingleOrDefault();
        return lot is null ? ApiResults.NotFound("Lot ne postoji ili je poništen.") : TypedResults.Ok(new { OnHand = lot.KnownOnHand, lot.Reserved, Available = lot.KnownAvailable });
    }
}
