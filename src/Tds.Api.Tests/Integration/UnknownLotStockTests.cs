using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Dashboard;
using Tds.Api.Features.Materials;
using Tds.Api.Features.Stock;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

/// <summary>PREGLED N2 / DOMAIN §2.6: a lot imported from Notion without quantity has unknown stock, never 0.</summary>
public class UnknownLotStockTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Unknown_lot_shows_dash_marks_the_item_incomplete_and_a_real_correction_makes_it_normal()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var inspector = await TestData.Employee(admin);
        var supplier = await TestData.Supplier(admin);
        var item = await TestData.Item(admin, minimumStock: 100);
        await TestData.AcceptedLot(admin, item.Id, supplier.Id, inspector.Id, 50, "H-1");

        // As the Notion import creates it: no received quantity, no movement, accepted.
        var imported = MaterialLot.ImportedFromNotion("AL-77", item.Id, supplier.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1));
        imported.Decide(ReceiptDecision.Prihvacen, inspector.Id, null, 0, new DateOnly(2026, 10, 1));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.MaterialLots.Add(imported);
            await db.SaveChangesAsync(Ct);
        }

        var lot = await (await admin.GetAsync($"/api/lots/{imported.Id}", Ct)).ReadAsync<LotDto>();
        Assert.Null(lot.OnHand);
        using (var stock = JsonDocument.Parse(await admin.GetStringAsync($"/api/stock/lots/{imported.Id}", Ct)))
        {
            Assert.Equal(JsonValueKind.Null, stock.RootElement.GetProperty("onHand").ValueKind);
            Assert.Equal(JsonValueKind.Null, stock.RootElement.GetProperty("available").ValueKind);
        }

        // Item stock only from the known lot, marked incomplete.
        var row = (await (await admin.GetAsync("/api/stock/items", Ct)).ReadAsync<PagedResult<ItemStockDto>>()).Items.Single();
        // Known lots alone would be "Ispod minimuma"; the real level is not known (DOMAIN §2.4): "—".
        Assert.Equal((50m, 1, (StockLevel?)null), (row.OnHand, row.UnknownLotCount, row.StockLevel));
        Assert.Empty((await (await admin.GetAsync("/api/stock/items?complete=true", Ct)).ReadAsync<PagedResult<ItemStockDto>>()).Items);

        // Not in the "below minimum" tile (stock unknown), but in the warnings.
        var dash = await (await admin.GetAsync("/api/dashboard", Ct)).ReadAsync<DashboardDto>();
        Assert.Equal(0, dash.Kpi.ItemsBelowMinimum);
        Assert.Contains(dash.Warnings, w => w.Kind == "LotBezKolicine" && w.EntityId == imported.Id && w.Severity == "amber");
        Assert.Equal(1, dash.Stock.Items.Single().UnknownLotCount);

        // No write-off and no negative correction before the real stock is known.
        var writeOff = await admin.PostAsJsonAsync($"/api/lots/{imported.Id}/movements", new { type = "Otpis", quantity = 1, date = "2026-10-01", reason = "Oštećenje" }, Ct);
        Assert.Equal("Stanje lota AL-77 nije poznato. Prvo upiši stvarno stanje korekcijom.", (await writeOff.ProblemAsync(HttpStatusCode.Conflict)).Detail());
        await (await admin.PostAsJsonAsync($"/api/lots/{imported.Id}/movements", new { type = "Korekcija", quantity = -1, date = "2026-10-01", reason = "Popis" }, Ct))
            .ProblemAsync(HttpStatusCode.Conflict);

        // A correction with the real stock (here: nothing left, 0) turns it into a normal lot.
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/lots/{imported.Id}/movements",
            new { type = "Korekcija", quantity = 0, date = "2026-10-01", reason = "Popis nakon importa" }, Ct)).StatusCode);
        Assert.Equal(0m, (await (await admin.GetAsync($"/api/lots/{imported.Id}", Ct)).ReadAsync<LotDto>()).OnHand);
        row = (await (await admin.GetAsync("/api/stock/items", Ct)).ReadAsync<PagedResult<ItemStockDto>>()).Items.Single();
        Assert.Equal((0, StockLevel.IspodMinimuma), (row.UnknownLotCount, row.StockLevel!.Value));
        dash = await (await admin.GetAsync("/api/dashboard", Ct)).ReadAsync<DashboardDto>();
        Assert.Equal(1, dash.Kpi.ItemsBelowMinimum);
        Assert.DoesNotContain(dash.Warnings, w => w.Kind == "LotBezKolicine");
    }

    [Fact]
    public async Task Incomplete_item_whose_known_lots_are_ok_stays_ok_and_is_not_in_the_tile()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var inspector = await TestData.Employee(admin);
        var supplier = await TestData.Supplier(admin);
        var item = await TestData.Item(admin, minimumStock: 10);
        await TestData.AcceptedLot(admin, item.Id, supplier.Id, inspector.Id, 50, "H-1");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.MaterialLots.Add(MaterialLot.ImportedFromNotion("AL-78", item.Id, supplier.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1)));
            await db.SaveChangesAsync(Ct);
        }

        var row = (await (await admin.GetAsync("/api/stock/items", Ct)).ReadAsync<PagedResult<ItemStockDto>>()).Items.Single();
        Assert.Equal((1, StockLevel.Ok), (row.UnknownLotCount, row.StockLevel!.Value));
        Assert.Equal(0, (await (await admin.GetAsync("/api/dashboard", Ct)).ReadAsync<DashboardDto>()).Kpi.ItemsBelowMinimum);
    }
}
