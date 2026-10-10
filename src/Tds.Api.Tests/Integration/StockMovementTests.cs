using System.Net;
using System.Net.Http.Json;
using Tds.Api.Domain;
using Tds.Api.Features.Materials;
using Tds.Api.Features.Stock;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class StockMovementTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Corrections_and_writeoffs_change_onhand_only_through_movements()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var item = await TestData.Item(admin);
        var supplier = await TestData.Supplier(admin);
        var lot = await TestData.Lot(admin, item.Id, supplier.Id, 10);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);

        Assert.Equal(HttpStatusCode.Created, (await k.PostAsJsonAsync($"/api/lots/{lot.Id}/movements",
            new { type = "Korekcija", quantity = -1.5, date = "2026-10-05", reason = "Popis: fali tabla" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await k.PostAsJsonAsync($"/api/lots/{lot.Id}/movements",
            new { type = "Otpis", quantity = 2, date = "2026-10-06", reason = "Oštećene table" }, Ct)).StatusCode);

        var after = await (await k.GetAsync($"/api/lots/{lot.Id}", Ct)).ReadAsync<LotDto>();
        Assert.Equal(6.5m, after.OnHand);
        Assert.Equal(10m, after.ReceivedQty);

        var movements = await (await k.GetAsync($"/api/lots/{lot.Id}/movements", Ct)).ReadAsync<List<StockMovementDto>>();
        Assert.Equal([StockMovementType.Otpis, StockMovementType.Korekcija, StockMovementType.Prijem], movements.Select(m => m.Type));
        Assert.Equal([-2m, -1.5m, 10m], movements.Select(m => m.Quantity));
        Assert.Equal("kontrolor", movements[0].CreatedByName);
        Assert.Equal("admin", movements[2].CreatedByName);
    }

    [Fact]
    public async Task Rule_violations_are_rejected_and_nothing_is_written()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var item = await TestData.Item(admin);
        var supplier = await TestData.Supplier(admin);
        var lot = await TestData.Lot(admin, item.Id, supplier.Id, 10);

        var tooMuch = await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/movements", new { type = "Otpis", quantity = 11, date = "2026-10-05", reason = "X" }, Ct);
        Assert.Equal("Na lotu H-4471 ima samo 10.", (await tooMuch.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        var belowZero = await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/movements", new { type = "Korekcija", quantity = -10.5, date = "2026-10-05", reason = "X" }, Ct);
        await belowZero.ProblemAsync(HttpStatusCode.Conflict);

        var noReason = await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/movements", new { type = "Korekcija", quantity = 1, date = "2026-10-05" }, Ct);
        Assert.Equal("Razlog je obavezan.", (await noReason.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("reason"));

        var prijem = await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/movements", new { type = "Prijem", quantity = 5, date = "2026-10-05", reason = "X" }, Ct);
        Assert.Equal("Ručno se unose samo korekcija i otpis.", (await prijem.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("type"));

        Assert.Equal(10m, (await (await admin.GetAsync($"/api/lots/{lot.Id}", Ct)).ReadAsync<LotDto>()).OnHand);
    }

    [Fact]
    public async Task Concurrent_writeoffs_cannot_take_more_than_onhand()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var item = await TestData.Item(admin);
        var supplier = await TestData.Supplier(admin);
        var lot = await TestData.Lot(admin, item.Id, supplier.Id, 10);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            admin.PostAsJsonAsync($"/api/lots/{lot.Id}/movements", new { type = "Otpis", quantity = 3, date = "2026-10-05", reason = "Paralelno" }, Ct)));

        Assert.Equal(3, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1m, (await (await admin.GetAsync($"/api/lots/{lot.Id}", Ct)).ReadAsync<LotDto>()).OnHand);
    }

    [Fact]
    public async Task Operater_can_see_movements_but_not_add()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var lot = await TestData.Lot(admin, (await TestData.Item(admin)).Id, (await TestData.Supplier(admin)).Id, 10);
        var op = await factory.ClientAsAsync(Roles.Operater);

        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync($"/api/lots/{lot.Id}/movements", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync($"/api/lots/{lot.Id}/movements",
            new { type = "Korekcija", quantity = 1, date = "2026-10-05", reason = "X" }, Ct)).StatusCode);
    }
}
