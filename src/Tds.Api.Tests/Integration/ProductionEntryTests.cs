using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Equipment;
using Tds.Api.Features.Materials;
using Tds.Api.Features.ProductionEntries;
using Tds.Api.Features.WorkOrders;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class ProductionEntryTests(PostgresFixture postgres)
{
    private static readonly SystemClock Clock = new(TimeProvider.System);
    private static string Day(int offset = 0) => Clock.Today.AddDays(offset).ToString("yyyy-MM-dd");

    private sealed record World(TdsApiFactory Factory, HttpClient Admin, HttpClient Kontrolor, WorkOrderDto Wo, LotDto Lot, Guid OperatorId, Guid InspectorId, Guid M1, Guid M2);

    private static async Task<World> Arrange(TdsApiFactory factory, string toStatus = "Laser")
    {
        var admin = await factory.AdminAsync();
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var inspector = await TestData.Employee(admin, "Amra Hodžić");
        var op = await TestData.Employee(admin, "Marko Marić");
        var item = await TestData.Item(admin);
        var lot = await TestData.AcceptedLot(admin, item.Id, (await TestData.Supplier(admin)).Id, inspector.Id, 10);
        var wo = await TestData.WorkOrder(k, qty: 50);
        foreach (var s in toStatus == "Laser" ? new[] { "Spreman", "Laser" } : new[] { "Spreman" })
            wo = await (await k.PostAsJsonAsync($"/api/work-orders/{wo.Id}/status", new { to = s, version = wo.Version }, Ct)).ReadAsync<WorkOrderDto>();
        var machines = await admin.GetFromJsonAsync<List<EquipmentOption>>("/api/equipment/options?kind=Masina", JsonOptions, Ct);
        return new World(factory, admin, k, wo, lot, op.Id, inspector.Id, machines![0].Id, machines[2].Id);
    }

    private static object Body(World w, int laser = 0, int pressed = 0, int good = 0, int scrap = 0, decimal? consumed = null,
        Guid? lot = null, string? date = null, Guid[]? machines = null, uint version = 0) => new
    {
        workOrderId = w.Wo.Id, date = date ?? Day(), operatorEmployeeId = w.OperatorId, materialLotId = lot,
        equipmentIds = machines ?? [w.M1], laserCutQty = laser, pressedQty = pressed, goodQty = good, scrapQty = scrap,
        laborHours = 7.5m, materialConsumed = consumed, note = "Smjena 1", version,
    };

    private static async Task<decimal> OnHand(HttpClient c, Guid lotId) => (await (await c.GetAsync($"/api/lots/{lotId}", Ct)).ReadAsync<LotDto>()).OnHand;

    [Fact]
    public async Task Entry_with_consumption_issues_stock_links_lot_and_gets_a_daily_number()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var w = await Arrange(factory);
        var op = await factory.ClientAsAsync(Roles.Operater);

        var r = await (await op.PostAsJsonAsync("/api/production-entries", Body(w, laser: 40, scrap: 2, consumed: 2.5m, lot: w.Lot.Id, machines: [w.M1, w.M2]), Ct))
            .ReadAsync<WithWarnings<ProductionEntryDto>>(HttpStatusCode.Created);

        Assert.Equal($"DP-{Clock.Today:yyyyMMdd}-01", r.Data.Number);
        Assert.Equal(["M-01", "M-03"], r.Data.EquipmentCodes);
        Assert.Equal("operater", r.Data.CreatedByName);
        Assert.Equal(7.5m, await OnHand(w.Admin, w.Lot.Id));

        // DOMAIN §5.2: the lot was not linked → link with 0 reservation.
        var links = await (await w.Admin.GetAsync($"/api/work-orders/{w.Wo.Id}/materials", Ct)).ReadAsync<List<WorkOrderMaterialDto>>();
        var link = Assert.Single(links);
        Assert.Equal((0m, 2.5m), (link.ReservedQty, link.ConsumedQty));

        var second = await (await op.PostAsJsonAsync("/api/production-entries", Body(w, laser: 10, lot: w.Lot.Id), Ct)).ReadAsync<WithWarnings<ProductionEntryDto>>(HttpStatusCode.Created);
        Assert.Equal($"DP-{Clock.Today:yyyyMMdd}-02", second.Data.Number);
    }

    [Fact]
    public async Task Changing_consumption_voids_old_issue_and_creates_new_one()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var w = await Arrange(factory);
        var e = (await (await w.Kontrolor.PostAsJsonAsync("/api/production-entries", Body(w, laser: 40, consumed: 2.5m, lot: w.Lot.Id), Ct))
            .ReadAsync<WithWarnings<ProductionEntryDto>>(HttpStatusCode.Created)).Data;

        var edited = (await (await w.Kontrolor.PutAsJsonAsync($"/api/production-entries/{e.Id}", Body(w, laser: 40, consumed: 9.5m, lot: w.Lot.Id, version: e.Version), Ct))
            .ReadAsync<WithWarnings<ProductionEntryDto>>()).Data;
        Assert.Equal(9.5m, edited.MaterialConsumed);
        Assert.Equal(0.5m, await OnHand(w.Admin, w.Lot.Id)); // edit may use what it already consumed

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var moves = await db.StockMovements.Where(m => m.ProductionEntryId == e.Id).OrderBy(m => m.CreatedAt).ToListAsync(Ct);
        Assert.Equal(2, moves.Count);
        Assert.True(moves[0].IsVoided);
        Assert.Equal(-2.5m, moves[0].Quantity);
        Assert.False(moves[1].IsVoided);
        Assert.Equal(-9.5m, moves[1].Quantity);

        // Editing only the note does not touch stock.
        var noteOnly = await w.Kontrolor.PutAsJsonAsync($"/api/production-entries/{e.Id}", Body(w, laser: 41, consumed: 9.5m, lot: w.Lot.Id, version: edited.Version), Ct);
        Assert.Equal(HttpStatusCode.OK, noteOnly.StatusCode);
        Assert.Equal(2, await db.StockMovements.CountAsync(m => m.ProductionEntryId == e.Id, Ct));
    }

    [Fact]
    public async Task Voiding_entry_voids_its_issue_and_restores_stock_and_completed_qty()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var w = await Arrange(factory);
        var e = (await (await w.Kontrolor.PostAsJsonAsync("/api/production-entries", Body(w, laser: 10, consumed: 4m, lot: w.Lot.Id), Ct))
            .ReadAsync<WithWarnings<ProductionEntryDto>>(HttpStatusCode.Created)).Data;
        Assert.Equal(6m, await OnHand(w.Admin, w.Lot.Id));

        var voided = await (await w.Kontrolor.PostAsJsonAsync($"/api/production-entries/{e.Id}/void", new { reason = "Pogrešan nalog", version = e.Version }, Ct)).ReadAsync<ProductionEntryDto>();
        Assert.True(voided.IsVoided);
        Assert.Equal(10m, await OnHand(w.Admin, w.Lot.Id));

        var edit = await w.Kontrolor.PutAsJsonAsync($"/api/production-entries/{e.Id}", Body(w, laser: 1, version: voided.Version), Ct);
        await edit.ProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Good_pieces_need_final_inspection_and_count_into_completed_qty()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var w = await Arrange(factory);

        var early = await w.Kontrolor.PostAsJsonAsync("/api/production-entries", Body(w, good: 5), Ct);
        Assert.Equal("Gotovi ispravni komadi se upisuju tek nakon prihvaćene završne kontrole.", (await early.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        await TestData.Check(w.Kontrolor, w.Wo.Id, w.InspectorId, "ZavrsnaKontrola");
        await (await w.Kontrolor.PostAsJsonAsync("/api/production-entries", Body(w, good: 30), Ct)).ReadAsync<WithWarnings<ProductionEntryDto>>(HttpStatusCode.Created);
        var over = await (await w.Kontrolor.PostAsJsonAsync("/api/production-entries", Body(w, good: 25), Ct)).ReadAsync<WithWarnings<ProductionEntryDto>>(HttpStatusCode.Created);
        Assert.Equal(["Ukupno gotovih ispravnih komada (55) je veće od naručene količine (50)."], over.Warnings);

        var wo = await (await w.Admin.GetAsync($"/api/work-orders/{w.Wo.Id}", Ct)).ReadAsync<WorkOrderDto>();
        Assert.Equal(55, wo.CompletedQty);
        Assert.Equal(100, wo.ProgressPercent);

        var totals = await (await w.Admin.GetAsync($"/api/work-orders/{w.Wo.Id}/production-entries", Ct)).ReadAsync<WorkOrderProduction>();
        Assert.Equal((55, 2, 15m), (totals.Totals.GoodQty, totals.Totals.Entries, totals.Totals.LaborHours));
    }

    [Fact]
    public async Task Rules_reject_wrong_status_lot_and_dates()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var w = await Arrange(factory);

        var noLot = await w.Kontrolor.PostAsJsonAsync("/api/production-entries", Body(w, laser: 5), Ct);
        Assert.Equal("Odaberi lot materijala — obavezan je kad se reže ili troši materijal.", (await noLot.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        var lot2 = await TestData.Lot(w.Admin, w.Lot.MaterialItemId, w.Lot.SupplierId, 5, "H-NK");
        var notAccepted = await w.Kontrolor.PostAsJsonAsync("/api/production-entries", Body(w, laser: 5, lot: lot2.Id), Ct);
        Assert.Equal("Lot H-NK nije prihvaćen (Na kontroli) i ne smije se koristiti.", (await notAccepted.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        var tooMuch = await w.Kontrolor.PostAsJsonAsync("/api/production-entries", Body(w, laser: 5, consumed: 11, lot: w.Lot.Id), Ct);
        Assert.Equal("Na lotu ima samo 10 tabla.", (await tooMuch.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        var old = await w.Kontrolor.PostAsJsonAsync("/api/production-entries", Body(w, pressed: 5, date: Day(-8)), Ct);
        Assert.Equal("Datum ne smije biti stariji od 7 dana. Starije zapise unosi Admin.", (await old.ProblemAsync(HttpStatusCode.Conflict)).Detail());
        Assert.Equal(HttpStatusCode.Created, (await w.Admin.PostAsJsonAsync("/api/production-entries", Body(w, pressed: 5, date: Day(-8)), Ct)).StatusCode);

        var noMachine = await w.Kontrolor.PostAsJsonAsync("/api/production-entries", Body(w, pressed: 5, machines: []), Ct);
        Assert.Equal("Odaberi barem jednu mašinu.", (await noMachine.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        var wrongOrder = await TestData.WorkOrder(w.Kontrolor, "Novi");
        var zaprimljen = await w.Kontrolor.PostAsJsonAsync("/api/production-entries", new
        {
            workOrderId = wrongOrder.Id, date = Day(), operatorEmployeeId = w.OperatorId, equipmentIds = new[] { w.M1 }, pressedQty = 5, laborHours = 1,
        }, Ct);
        Assert.Equal($"Na nalogu {wrongOrder.Number} sa statusom Zaprimljen se ne može evidentirati proizvodnja.", (await zaprimljen.ProblemAsync(HttpStatusCode.Conflict)).Detail());
    }

    [Fact]
    public async Task Operater_changes_only_own_entries_and_kontrolor_any()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var w = await Arrange(factory);
        var op1 = await factory.ClientAsAsync(Roles.Operater, "op1");
        var op2 = await factory.ClientAsAsync(Roles.Operater, "op2");
        var mine = (await (await op1.PostAsJsonAsync("/api/production-entries", Body(w, pressed: 5), Ct)).ReadAsync<WithWarnings<ProductionEntryDto>>(HttpStatusCode.Created)).Data;

        var other = await op2.PutAsJsonAsync($"/api/production-entries/{mine.Id}", Body(w, pressed: 6, version: mine.Version), Ct);
        Assert.Equal("Svoj zapis možeš mijenjati ili poništiti samo isti dan. Za starije zapise javi se kontroloru.", (await other.ProblemAsync(HttpStatusCode.Forbidden)).Detail());
        Assert.Equal(HttpStatusCode.Forbidden, (await op2.PostAsJsonAsync($"/api/production-entries/{mine.Id}/void", new { reason = "X", version = mine.Version }, Ct)).StatusCode);

        var own = await (await op1.PutAsJsonAsync($"/api/production-entries/{mine.Id}", Body(w, pressed: 6, version: mine.Version), Ct)).ReadAsync<WithWarnings<ProductionEntryDto>>();
        var byKontrolor = await w.Kontrolor.PutAsJsonAsync($"/api/production-entries/{mine.Id}", Body(w, pressed: 7, version: own.Data.Version), Ct);
        Assert.Equal(HttpStatusCode.OK, byKontrolor.StatusCode);

        // Author from yesterday cannot change any more.
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProductionEntries.Where(p => p.Id == mine.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.CreatedAt, DateTimeOffset.UtcNow.AddDays(-1)), Ct);
        var current = await (await op1.GetAsync($"/api/production-entries/{mine.Id}", Ct)).ReadAsync<ProductionEntryDto>();
        Assert.Equal(HttpStatusCode.Forbidden, (await op1.PutAsJsonAsync($"/api/production-entries/{mine.Id}", Body(w, pressed: 8, version: current.Version), Ct)).StatusCode);
    }

    [Fact]
    public async Task Machine_changes_are_audited_on_the_entry_and_order_cannot_change()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var w = await Arrange(factory);
        var e = (await (await w.Kontrolor.PostAsJsonAsync("/api/production-entries", Body(w, pressed: 5, machines: [w.M1]), Ct)).ReadAsync<WithWarnings<ProductionEntryDto>>(HttpStatusCode.Created)).Data;
        await w.Kontrolor.PutAsJsonAsync($"/api/production-entries/{e.Id}", Body(w, pressed: 5, machines: [w.M2], version: e.Version), Ct);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = await db.AuditLogs.Where(a => a.EntityId == e.Id).OrderBy(a => a.At).ToListAsync(Ct);
        var create = JsonDocument.Parse(logs[0].ChangesJson).RootElement.GetProperty("EquipmentIds");
        Assert.Equal(w.M1.ToString(), create[1][0].GetString());
        var change = JsonDocument.Parse(logs.Last().ChangesJson).RootElement.GetProperty("EquipmentIds");
        Assert.Equal(w.M1.ToString(), change[0][0].GetString());
        Assert.Equal(w.M2.ToString(), change[1][0].GetString());

        var other = await TestData.WorkOrder(w.Kontrolor, "Drugi");
        var current = await (await w.Kontrolor.GetAsync($"/api/production-entries/{e.Id}", Ct)).ReadAsync<ProductionEntryDto>();
        var move = await w.Kontrolor.PutAsJsonAsync($"/api/production-entries/{e.Id}", new
        {
            workOrderId = other.Id, date = Day(), operatorEmployeeId = w.OperatorId, equipmentIds = new[] { w.M1 }, pressedQty = 5, laborHours = 1, version = current.Version,
        }, Ct);
        Assert.Equal("Nalog se na postojećem zapisu ne mijenja. Poništi zapis i unesi novi.", (await move.ProblemAsync(HttpStatusCode.Conflict)).Detail());
    }
}
