using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Equipment;
using Tds.Api.Features.Materials;
using Tds.Api.Features.ProductionEntries;
using Tds.Api.Features.Stock;
using Tds.Api.Features.WorkOrders;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

/// <summary>
/// ROADMAP Phase 1, last item: lot → order → reservation → production entry with consumption → stock is exact.
/// Stock is only ever Σ stock_movement (golden rule 1); this test checks every intermediate state.
/// </summary>
public class StockFlowEndToEndTests(PostgresFixture postgres)
{
    private static readonly string Today = new SystemClock(TimeProvider.System).Today.ToString("yyyy-MM-dd");

    [Fact]
    public async Task Lot_order_reservation_production_entry_with_consumption_gives_exact_stock()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var kontrolor = await factory.ClientAsAsync(Roles.Kontrolor);
        var operater = await factory.ClientAsAsync(Roles.Operater);

        async Task<(decimal OnHand, decimal Reserved, decimal Available)> LotStock(Guid lotId)
        {
            var s = await (await admin.GetAsync($"/api/stock/lots/{lotId}", Ct)).ReadAsync<Dictionary<string, decimal>>();
            return (s["onHand"], s["reserved"], s["available"]);
        }
        async Task<ItemStockDto> ItemStock(Guid itemId) =>
            (await (await admin.GetAsync("/api/stock/items", Ct)).ReadAsync<PagedResult<ItemStockDto>>()).Items.Single(i => i.MaterialItemId == itemId);

        // 1. Lot: 20 tabli S235 3 mm received and accepted.
        var inspector = await TestData.Employee(admin, "Amra Hodžić");
        var operatorEmp = await TestData.Employee(admin, "Marko Marić");
        var item = await TestData.Item(admin, "Lim crni", "S235", 3, "tabla", minimumStock: 4);
        var supplier = await TestData.Supplier(admin, "Salzgitter Mannesmann Stahlhandel");
        var lot = await TestData.AcceptedLot(kontrolor, item.Id, supplier.Id, inspector.Id, 20m, "H-4471");
        Assert.Equal((20m, 0m, 20m), await LotStock(lot.Id));

        // 2. Order for Metalbau AG, released into production.
        var wo = await TestData.WorkOrder(kontrolor, "Metalbau AG", qty: 200);
        foreach (var s in new[] { "Spreman", "Laser" })
            wo = await (await kontrolor.PostAsJsonAsync($"/api/work-orders/{wo.Id}/status", new { to = s, version = wo.Version }, Ct)).ReadAsync<WorkOrderDto>();

        // 3. Reserve 12 tabli of the lot for the order.
        var link = (await (await kontrolor.PostAsJsonAsync($"/api/work-orders/{wo.Id}/materials", new { materialLotId = lot.Id, reservedQty = 12m }, Ct))
            .ReadAsync<WithWarnings<WorkOrderMaterialDto>>(HttpStatusCode.Created)).Data;
        Assert.Equal((20m, 12m, 8m), await LotStock(lot.Id));
        var afterReservation = await ItemStock(item.Id);
        Assert.Equal((20m, 12m, 8m, StockLevel.Ok), (afterReservation.OnHand, afterReservation.Reserved, afterReservation.Available, afterReservation.StockLevel));

        // 4. Operator records laser cutting with 5 tabli consumed.
        var m1 = (await admin.GetFromJsonAsync<List<EquipmentOption>>("/api/equipment/options?kind=Masina", JsonOptions, Ct))!.Single(m => m.Code == "M-01");
        object Entry(decimal consumed, uint version = 0) => new
        {
            workOrderId = wo.Id, date = Today, operatorEmployeeId = operatorEmp.Id, materialLotId = lot.Id, equipmentIds = new[] { m1.Id },
            laserCutQty = 180, pressedQty = 0, goodQty = 0, scrapQty = 3, laborHours = 7.5m, materialConsumed = consumed, version,
        };
        var entry = (await (await operater.PostAsJsonAsync("/api/production-entries", Entry(5m), Ct))
            .ReadAsync<WithWarnings<ProductionEntryDto>>(HttpStatusCode.Created)).Data;

        // OnHand 20 − 5 = 15; effective reservation max(0, 12 − 5) = 7; Available 15 − 7 = 8.
        Assert.Equal((15m, 7m, 8m), await LotStock(lot.Id));
        var mat = Assert.Single(await (await admin.GetAsync($"/api/work-orders/{wo.Id}/materials", Ct)).ReadAsync<List<WorkOrderMaterialDto>>());
        Assert.Equal((12m, 5m, 7m), (mat.ReservedQty, mat.ConsumedQty, mat.EffectiveReservation));

        // 5. Operator corrects the consumption to 14 (more than the reservation).
        entry = (await (await operater.PutAsJsonAsync($"/api/production-entries/{entry.Id}", Entry(14m, entry.Version), Ct))
            .ReadAsync<WithWarnings<ProductionEntryDto>>()).Data;
        // OnHand 20 − 14 = 6; effective reservation max(0, 12 − 14) = 0; Available 6.
        Assert.Equal((6m, 0m, 6m), await LotStock(lot.Id));
        var afterEdit = await ItemStock(item.Id);
        Assert.Equal((6m, 0m, 6m, StockLevel.Ok), (afterEdit.OnHand, afterEdit.Reserved, afterEdit.Available, afterEdit.StockLevel));

        // 6. A manual write-off of 1 damaged sheet: 5 < 1,5 × 4 → Nisko.
        await kontrolor.PostAsJsonAsync($"/api/lots/{lot.Id}/movements", new { type = "Otpis", quantity = 1m, date = Today, reason = "Oštećena tabla" }, Ct);
        Assert.Equal((5m, 0m, 5m), await LotStock(lot.Id));
        Assert.Equal(StockLevel.Nisko, (await ItemStock(item.Id)).StockLevel);

        // 7. The entry is voided: its issue is voided, consumption returns, the reservation is effective again.
        await kontrolor.PostAsJsonAsync($"/api/production-entries/{entry.Id}/void", new { reason = "Unesen na pogrešan nalog", version = entry.Version }, Ct);
        Assert.Equal((19m, 12m, 7m), await LotStock(lot.Id));

        // 8. Cancelling the order releases the reservation.
        wo = await (await admin.GetAsync($"/api/work-orders/{wo.Id}", Ct)).ReadAsync<WorkOrderDto>();
        await kontrolor.PostAsJsonAsync($"/api/work-orders/{wo.Id}/status", new { to = "Otkazano", version = wo.Version }, Ct);
        Assert.Equal((19m, 0m, 19m), await LotStock(lot.Id));

        // Golden rule 1: the lot's stock equals the sum of its non-voided movements, and nothing else stores it.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var movements = await db.StockMovements.Where(m => m.MaterialLotId == lot.Id).OrderBy(m => m.CreatedAt).ToListAsync(Ct);
        Assert.Equal(
            [(StockMovementType.Prijem, 20m, false), (StockMovementType.Izdavanje, -5m, true), (StockMovementType.Izdavanje, -14m, true), (StockMovementType.Otpis, -1m, false)],
            movements.Select(m => (m.Type, m.Quantity, m.IsVoided)));
        Assert.Equal(19m, movements.Where(m => !m.IsVoided).Sum(m => m.Quantity));
        Assert.Equal(20m, (await db.MaterialLots.SingleAsync(l => l.Id == lot.Id, Ct)).ReceivedQty);
        Assert.Equal(link.Id, (await db.WorkOrderMaterials.SingleAsync(Ct)).Id);
    }
}
