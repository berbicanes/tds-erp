using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Stock;
using Tds.Api.Features.WorkOrders;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class WorkOrderMaterialIntegrationTests(PostgresFixture postgres)
{
    private sealed record Setup(HttpClient Admin, Guid ItemId, Guid LotId, Guid InspectorId, WorkOrderDto Wo);

    private static async Task<Setup> Arrange(TdsApiFactory factory, decimal lotQty = 10)
    {
        var admin = await factory.AdminAsync();
        var item = await TestData.Item(admin, minimumStock: 2);
        var inspector = await TestData.Employee(admin);
        var lot = await TestData.AcceptedLot(admin, item.Id, (await TestData.Supplier(admin)).Id, inspector.Id, lotQty);
        return new Setup(admin, item.Id, lot.Id, inspector.Id, await TestData.WorkOrder(admin));
    }

    private static async Task<ItemStockDto> ItemStock(HttpClient c, Guid itemId) =>
        (await (await c.GetAsync("/api/stock/items", Ct)).ReadAsync<PagedResult<ItemStockDto>>()).Items.Single(i => i.MaterialItemId == itemId);

    [Fact]
    public async Task Reservation_lowers_available_and_over_reservation_only_warns()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var s = await Arrange(factory);

        var linked = await (await s.Admin.PostAsJsonAsync($"/api/work-orders/{s.Wo.Id}/materials", new { materialLotId = s.LotId, reservedQty = 4 }, Ct))
            .ReadAsync<WithWarnings<WorkOrderMaterialDto>>(HttpStatusCode.Created);
        Assert.Empty(linked.Warnings);
        Assert.Equal(4m, linked.Data.EffectiveReservation);
        Assert.Equal(6m, linked.Data.LotAvailable);

        var stock = await ItemStock(s.Admin, s.ItemId);
        Assert.Equal((10m, 4m, 6m), (stock.OnHand, stock.Reserved, stock.Available));

        // Second order reserves more than is left: allowed, with a warning; Available goes negative.
        var wo2 = await TestData.WorkOrder(s.Admin, "Stahlbau Keller GmbH");
        var over = await (await s.Admin.PostAsJsonAsync($"/api/work-orders/{wo2.Id}/materials", new { materialLotId = s.LotId, reservedQty = 8 }, Ct))
            .ReadAsync<WithWarnings<WorkOrderMaterialDto>>(HttpStatusCode.Created);
        Assert.Equal(["Rezervacija je veća od raspoloživog na lotu H-4471 (raspoloživo 6 tabla)."], over.Warnings);
        Assert.Equal(-2m, (await ItemStock(s.Admin, s.ItemId)).Available);
        Assert.Equal(StockLevel.Nema, (await ItemStock(s.Admin, s.ItemId)).StockLevel);
    }

    [Fact]
    public async Task Reservation_change_duplicate_link_and_read_only_order()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var s = await Arrange(factory);
        var link = (await (await s.Admin.PostAsJsonAsync($"/api/work-orders/{s.Wo.Id}/materials", new { materialLotId = s.LotId, reservedQty = 4 }, Ct))
            .ReadAsync<WithWarnings<WorkOrderMaterialDto>>(HttpStatusCode.Created)).Data;

        var dup = await s.Admin.PostAsJsonAsync($"/api/work-orders/{s.Wo.Id}/materials", new { materialLotId = s.LotId, reservedQty = 1 }, Ct);
        Assert.Equal("Lot H-4471 je već vezan za ovaj nalog. Promijeni rezervaciju u listi.", (await dup.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("materialLotId"));

        var zero = await (await s.Admin.PutAsJsonAsync($"/api/work-orders/{s.Wo.Id}/materials/{link.Id}", new { reservedQty = 0, version = link.Version }, Ct))
            .ReadAsync<WithWarnings<WorkOrderMaterialDto>>();
        Assert.Equal(0m, zero.Data.ReservedQty);
        Assert.Single(await (await s.Admin.GetAsync($"/api/work-orders/{s.Wo.Id}/materials", Ct)).ReadAsync<List<WorkOrderMaterialDto>>());

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await s.Admin.DeleteAsync($"/api/work-orders/{s.Wo.Id}/materials/{link.Id}", Ct)).StatusCode);

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().WorkOrders.Where(w => w.Id == s.Wo.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(w => w.Status, WorkOrderStatus.Otkazano), Ct);
        var closed = await s.Admin.PutAsJsonAsync($"/api/work-orders/{s.Wo.Id}/materials/{link.Id}", new { reservedQty = 2, version = zero.Data.Version }, Ct);
        await closed.ProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reservations_of_finished_orders_do_not_count()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var s = await Arrange(factory);
        await s.Admin.PostAsJsonAsync($"/api/work-orders/{s.Wo.Id}/materials", new { materialLotId = s.LotId, reservedQty = 4 }, Ct);
        Assert.Equal(4m, (await ItemStock(s.Admin, s.ItemId)).Reserved);

        foreach (var status in new[] { WorkOrderStatus.Zavrseno, WorkOrderStatus.Isporuceno, WorkOrderStatus.Otkazano })
        {
            using (var scope = factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().WorkOrders.Where(w => w.Id == s.Wo.Id)
                    .ExecuteUpdateAsync(x => x.SetProperty(w => w.Status, status), Ct);
            Assert.Equal(0m, (await ItemStock(s.Admin, s.ItemId)).Reserved);
        }
    }

    [Fact]
    public async Task Quarantined_lot_cannot_be_reserved_and_linked_lot_cannot_be_voided()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var s = await Arrange(factory);
        var other = await TestData.Lot(s.Admin, s.ItemId, (await TestData.Supplier(s.Admin, "Drugi")).Id, 5, "H-2");
        await s.Admin.PostAsJsonAsync($"/api/lots/{other.Id}/decision", new { decision = "Karantin", inspectedByEmployeeId = s.InspectorId, version = other.Version }, Ct);

        var q = await s.Admin.PostAsJsonAsync($"/api/work-orders/{s.Wo.Id}/materials", new { materialLotId = other.Id, reservedQty = 1 }, Ct);
        Assert.Equal("Lot H-2 ima odluku Karantin i ne može se rezervisati.", (await q.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        var fresh = await TestData.Lot(s.Admin, s.ItemId, (await TestData.Supplier(s.Admin, "Treći")).Id, 3, "H-3");
        await s.Admin.PostAsJsonAsync($"/api/work-orders/{s.Wo.Id}/materials", new { materialLotId = fresh.Id, reservedQty = 0 }, Ct);
        var voidLinked = await s.Admin.PostAsJsonAsync($"/api/lots/{fresh.Id}/void", new { reason = "Greška", version = fresh.Version }, Ct);
        Assert.Equal("Lot H-3 je vezan za nalog i ne može se poništiti.", (await voidLinked.ProblemAsync(HttpStatusCode.Conflict)).Detail());
    }
}
