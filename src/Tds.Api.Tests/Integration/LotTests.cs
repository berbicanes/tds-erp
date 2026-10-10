using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Materials;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class LotTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Receiving_a_lot_creates_prijem_and_onhand_comes_from_movements()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var item = await TestData.Item(k);
        var supplier = await TestData.Supplier(k, status: "UslovnoOdobren");

        var r = await (await k.PostAsJsonAsync("/api/lots", new
        {
            lotNumber = " H-4471 ", materialItemId = item.Id, supplierId = supplier.Id, receivedDate = "2026-10-01",
            receivedQty = 12.5, dimensionsDescription = "3000×1500, 12,5 tabli", storageLocation = "Regal A2",
        }, Ct)).ReadAsync<WithWarnings<LotDto>>(HttpStatusCode.Created);

        Assert.Equal("H-4471", r.Data.LotNumber);
        Assert.Equal(12.5m, r.Data.OnHand);
        Assert.Equal(ReceiptDecision.NaKontroli, r.Data.ReceiptDecision);
        Assert.Equal(["Dobavljač Metalia d.o.o. nije potpuno odobren (Uslovno odobren)."], r.Warnings);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var m = await db.StockMovements.SingleAsync(x => x.MaterialLotId == r.Data.Id, Ct);
        Assert.Equal(StockMovementType.Prijem, m.Type);
        Assert.Equal(12.5m, m.Quantity);
        Assert.Equal(new DateOnly(2026, 10, 1), m.Date);
    }

    [Fact]
    public async Task Blocked_supplier_duplicate_lot_and_future_date_are_rejected()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var item = await TestData.Item(admin);
        var ok = await TestData.Supplier(admin);
        var blocked = await TestData.Supplier(admin, "Stari dobavljač", "Blokiran");
        await TestData.Lot(admin, item.Id, ok.Id, 5);

        var b = await admin.PostAsJsonAsync("/api/lots", new { lotNumber = "H-9", materialItemId = item.Id, supplierId = blocked.Id, receivedDate = "2026-10-01", receivedQty = 1 }, Ct);
        Assert.Equal("Dobavljač je blokiran.", (await b.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        var dup = await admin.PostAsJsonAsync("/api/lots", new { lotNumber = "H-4471", materialItemId = item.Id, supplierId = ok.Id, receivedDate = "2026-10-01", receivedQty = 1 }, Ct);
        Assert.Equal("Lot H-4471 već postoji.", (await dup.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("lotNumber"));

        var future = await admin.PostAsJsonAsync("/api/lots", new { lotNumber = "H-10", materialItemId = item.Id, supplierId = ok.Id, receivedDate = "2099-01-01", receivedQty = 1 }, Ct);
        Assert.Equal("Datum prijema ne može biti u budućnosti.", (await future.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        var zero = await admin.PostAsJsonAsync("/api/lots", new { lotNumber = "H-11", materialItemId = item.Id, supplierId = ok.Id, receivedDate = "2026-10-01", receivedQty = 0 }, Ct);
        Assert.Equal("Primljena količina mora biti veća od 0.", (await zero.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("receivedQty"));
    }

    [Fact]
    public async Task Accepting_without_certificate_warns_and_rejecting_writes_off_remaining_stock()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var item = await TestData.Item(admin);
        var supplier = await TestData.Supplier(admin);
        var inspector = await TestData.Employee(admin);
        var lot = await TestData.Lot(admin, item.Id, supplier.Id, 10);

        var accepted = await (await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/decision",
            new { decision = "Prihvacen", inspectedByEmployeeId = inspector.Id, version = lot.Version }, Ct)).ReadAsync<WithWarnings<LotDto>>();
        Assert.Equal(["Lot H-4471 je prihvaćen bez certifikata materijala."], accepted.Warnings);
        Assert.Equal("Amra Hodžić", accepted.Data.InspectedByName);

        var quarantine = await (await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/decision",
            new { decision = "Karantin", inspectedByEmployeeId = inspector.Id, version = accepted.Data.Version }, Ct)).ReadAsync<WithWarnings<LotDto>>();

        var rejected = await (await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/decision",
            new { decision = "Odbijen", inspectedByEmployeeId = inspector.Id, reason = "Certifikat ne odgovara heat broju", version = quarantine.Data.Version }, Ct))
            .ReadAsync<WithWarnings<LotDto>>();
        Assert.Equal(0m, rejected.Data.OnHand);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var otpis = await db.StockMovements.SingleAsync(m => m.MaterialLotId == lot.Id && m.Type == StockMovementType.Otpis, Ct);
        Assert.Equal(-10m, otpis.Quantity);
        Assert.Equal("Certifikat ne odgovara heat broju", otpis.Reason);

        var again = await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/decision",
            new { decision = "Prihvacen", inspectedByEmployeeId = inspector.Id, version = rejected.Data.Version }, Ct);
        Assert.Equal("Lot H-4471 je odbijen i odluka se više ne može mijenjati.", (await again.ProblemAsync(HttpStatusCode.Conflict)).Detail());
    }

    [Fact]
    public async Task Decision_without_inspector_or_rejection_without_reason_is_rejected_and_nothing_is_written()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var item = await TestData.Item(admin);
        var supplier = await TestData.Supplier(admin);
        var inspector = await TestData.Employee(admin);
        var lot = await TestData.Lot(admin, item.Id, supplier.Id, 10);

        var noInspector = await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/decision", new { decision = "Prihvacen", version = lot.Version }, Ct);
        Assert.Equal("Odaberi ko je kontrolisao lot.", (await noInspector.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        var noReason = await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/decision", new { decision = "Odbijen", inspectedByEmployeeId = inspector.Id, version = lot.Version }, Ct);
        Assert.Equal("Razlog odbijanja je obavezan.", (await noReason.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        var after = await (await admin.GetAsync($"/api/lots/{lot.Id}", Ct)).ReadAsync<LotDto>();
        Assert.Equal(ReceiptDecision.NaKontroli, after.ReceiptDecision);
        Assert.Equal(10m, after.OnHand);
    }

    [Fact]
    public async Task Update_changes_only_descriptive_fields_and_respects_version()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var item = await TestData.Item(admin);
        var supplier = await TestData.Supplier(admin);
        var lot = await TestData.Lot(admin, item.Id, supplier.Id, 10);

        var updated = await (await admin.PutAsJsonAsync($"/api/lots/{lot.Id}",
            new { lotNumber = "H-4471A", supplierId = supplier.Id, storageLocation = "Regal B1", version = lot.Version }, Ct)).ReadAsync<LotDto>();
        Assert.Equal("H-4471A", updated.LotNumber);
        Assert.Equal("Regal B1", updated.StorageLocation);
        Assert.Equal(10m, updated.ReceivedQty);

        var stale = await admin.PutAsJsonAsync($"/api/lots/{lot.Id}", new { lotNumber = "X", supplierId = supplier.Id, version = lot.Version }, Ct);
        Assert.Equal("concurrency", (await stale.ProblemAsync(HttpStatusCode.Conflict)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Admin_voids_a_fresh_lot_and_its_prijem_kontrolor_cannot()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var item = await TestData.Item(admin);
        var supplier = await TestData.Supplier(admin);
        var lot = await TestData.Lot(admin, item.Id, supplier.Id, 10);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);

        Assert.Equal(HttpStatusCode.Forbidden, (await k.PostAsJsonAsync($"/api/lots/{lot.Id}/void", new { reason = "Greška", version = lot.Version }, Ct)).StatusCode);

        var empty = await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/void", new { reason = "", version = lot.Version }, Ct);
        Assert.Equal("Razlog poništavanja je obavezan.", (await empty.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("reason"));

        var voided = await (await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/void", new { reason = "Dupli unos", version = lot.Version }, Ct)).ReadAsync<LotDto>();
        Assert.True(voided.IsVoided);
        Assert.Equal(0m, voided.OnHand);

        var list = await (await admin.GetAsync("/api/lots", Ct)).ReadAsync<PagedResult<LotDto>>();
        Assert.Empty(list.Items);
        var withVoided = await (await admin.GetAsync("/api/lots?includeVoided=true", Ct)).ReadAsync<PagedResult<LotDto>>();
        Assert.Single(withVoided.Items);
    }

    [Fact]
    public async Task Lot_with_other_movements_cannot_be_voided()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var item = await TestData.Item(admin);
        var supplier = await TestData.Supplier(admin);
        var inspector = await TestData.Employee(admin);
        var lot = await TestData.Lot(admin, item.Id, supplier.Id, 10);
        var rejected = await (await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/decision",
            new { decision = "Odbijen", inspectedByEmployeeId = inspector.Id, reason = "Oštećeno", version = lot.Version }, Ct)).ReadAsync<WithWarnings<LotDto>>();

        var r = await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/void", new { reason = "Greška", version = rejected.Data.Version }, Ct);
        Assert.Equal("Lot H-4471 ima pokrete zalihe osim prijema i ne može se poništiti.", (await r.ProblemAsync(HttpStatusCode.Conflict)).Detail());
    }

    [Fact]
    public async Task Item_received_date_and_quantity_cannot_be_changed_by_update()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var item = await TestData.Item(admin);
        var other = await TestData.Item(admin, "Lim inox", "1.4301", 2, "kg");
        var supplier = await TestData.Supplier(admin);
        var lot = await TestData.Lot(admin, item.Id, supplier.Id, 10, receivedDate: "2026-10-01");

        var r = await (await admin.PutAsJsonAsync($"/api/lots/{lot.Id}", new
        {
            lotNumber = "H-4471", supplierId = supplier.Id, storageLocation = "B2", version = lot.Version,
            materialItemId = other.Id, receivedDate = "2026-09-01", receivedQty = 99,   // not part of the contract → ignored
        }, Ct)).ReadAsync<LotDto>();

        Assert.Equal((item.Id, new DateOnly(2026, 10, 1), 10m, 10m), (r.MaterialItemId, r.ReceivedDate, r.ReceivedQty, r.OnHand));
        Assert.Equal("B2", r.StorageLocation);
    }
}
