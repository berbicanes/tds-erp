using System.Net.Http.Json;
using Tds.Api.Domain;
using Tds.Api.Features.Stock;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class ItemStockTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Item_stock_counts_only_accepted_lots_and_shows_on_hold_separately()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var supplier = await TestData.Supplier(admin);
        var inspector = await TestData.Employee(admin);
        var s235 = await TestData.Item(admin, minimumStock: 10);
        var inox = await TestData.Item(admin, "Lim inox", "1.4301", 2, "kg");
        await TestData.Item(admin, "Lim aluminij", "AlMg3", 4, "tabla", minimumStock: 5);

        await TestData.AcceptedLot(admin, s235.Id, supplier.Id, inspector.Id, 12, "H-1");     // accepted 12
        await TestData.Lot(admin, s235.Id, supplier.Id, 30, "H-2");                            // na kontroli 30
        var quarantined = await TestData.Lot(admin, s235.Id, supplier.Id, 5, "H-3");
        await admin.PostAsJsonAsync($"/api/lots/{quarantined.Id}/decision",
            new { decision = "Karantin", inspectedByEmployeeId = inspector.Id, version = quarantined.Version }, Ct);
        var rejected = await TestData.Lot(admin, s235.Id, supplier.Id, 7, "H-4");
        await admin.PostAsJsonAsync($"/api/lots/{rejected.Id}/decision",
            new { decision = "Odbijen", inspectedByEmployeeId = inspector.Id, reason = "Oštećeno", version = rejected.Version }, Ct);
        await TestData.AcceptedLot(admin, inox.Id, supplier.Id, inspector.Id, 250.5m, "H-5");

        var page = await (await admin.GetAsync("/api/stock/items", Ct)).ReadAsync<PagedResult<ItemStockDto>>();
        var byName = page.Items.ToDictionary(x => x.Name);

        var crni = byName["Lim crni"];
        Assert.Equal(12m, crni.OnHand);
        Assert.Equal(0m, crni.Reserved);
        Assert.Equal(12m, crni.Available);
        Assert.Equal(35m, crni.OnHoldQty);                 // 30 na kontroli + 5 karantin; odbijen = 0
        Assert.Equal(StockLevel.Nisko, crni.StockLevel);     // 12 < 1.5 × 10

        Assert.Equal(StockLevel.NijeDefinisano, byName["Lim inox"].StockLevel);
        Assert.Equal(StockLevel.Nema, byName["Lim aluminij"].StockLevel);

        // Attention first: Nema, then Nisko, then the rest.
        Assert.Equal(["Lim aluminij", "Lim crni", "Lim inox"], page.Items.Select(x => x.Name));

        var nisko = await (await admin.GetAsync("/api/stock/items?stockLevel=Nisko&stockLevel=IspodMinimuma", Ct)).ReadAsync<PagedResult<ItemStockDto>>();
        Assert.Equal("Lim crni", Assert.Single(nisko.Items).Name);
    }
}
