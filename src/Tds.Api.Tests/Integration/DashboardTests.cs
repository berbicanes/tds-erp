using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Dashboard;
using Tds.Api.Features.Equipment;
using Tds.Api.Features.WorkOrders;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class DashboardTests(PostgresFixture postgres)
{
    private static readonly SystemClock Clock = new(TimeProvider.System);
    private static string Day(int offset = 0) => Clock.Today.AddDays(offset).ToString("yyyy-MM-dd");

    [Fact]
    public async Task Empty_database_gives_zeros_and_empty_sections()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var op = await factory.ClientAsAsync(Roles.Operater);

        var d = await (await op.GetAsync("/api/dashboard", Ct)).ReadAsync<DashboardDto>();

        Assert.Equal(new DashboardKpi(0, 0, 0, 0, 0, 0, 0), d.Kpi);
        Assert.Empty(d.TodayEntries);
        Assert.Equal(14, d.Chart.Count);
        Assert.Empty(d.Warnings);
        Assert.Empty(d.OrdersByDue.Items);
        Assert.Empty(d.Stock.Items);
        Assert.Equal(Clock.Today, d.Today);
    }

    [Fact]
    public async Task Sections_show_the_right_numbers()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var inspector = await TestData.Employee(admin, "Amra Hodžić");
        var op = await TestData.Employee(admin, "Edin Begić");
        var supplier = await TestData.Supplier(admin);
        var m3 = (await admin.GetFromJsonAsync<List<EquipmentOption>>("/api/equipment/options?kind=Masina", JsonOptions, Ct))!.Single(m => m.Code == "M-03");

        // Stock: one item below minimum, one without minimum and empty (must NOT count in the KPI), one inactive empty.
        var below = await TestData.Item(admin, "Lim crni", "S235", 3, minimumStock: 10);
        await TestData.AcceptedLot(admin, below.Id, supplier.Id, inspector.Id, 4, "H-1");
        await TestData.Item(admin, "Lim crni", "S235", 20);                               // no minimum, 0 → "Nema", not in KPI
        var inactive = await TestData.Item(admin, "Lim stari", "S235", 1, minimumStock: 5);
        await admin.PutAsJsonAsync($"/api/material-items/{inactive.Id}", new { name = inactive.Name, grade = inactive.Grade, thicknessMm = 1, unit = "tabla", minimumStock = 5, isActive = false, version = inactive.Version }, Ct);
        var quarantined = await TestData.Lot(admin, below.Id, supplier.Id, 2, "Q-7");
        await admin.PostAsJsonAsync($"/api/lots/{quarantined.Id}/decision", new { decision = "Karantin", inspectedByEmployeeId = inspector.Id, version = quarantined.Version }, Ct);
        await TestData.Lot(admin, below.Id, supplier.Id, 5, "N-9", receivedDate: Day(-1));  // na kontroli

        // Orders: overdue, due in 3 days + urgent, later, blocked, finished.
        var late = await TestData.WorkOrder(admin, "Kasni AG", dueDate: Day(-2));
        var soon = await TestData.WorkOrder(admin, "Uskoro GmbH", dueDate: Day(3), priority: "Hitno");
        await TestData.WorkOrder(admin, "Kasnije d.o.o.", dueDate: Day(40));
        var blocked = await TestData.WorkOrder(admin, "Blokirani AG", dueDate: Day(20));
        await admin.PostAsJsonAsync($"/api/work-orders/{blocked.Id}/quality-approval", new { qualityApproval = "Blokirano", version = blocked.Version }, Ct);
        var done = await TestData.WorkOrder(admin, "Gotovo AG", dueDate: Day(-10));
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().WorkOrders.Where(w => w.Id == done.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(w => w.Status, WorkOrderStatus.Isporuceno), Ct);

        // Production today on the overdue order (after final inspection so good pieces are allowed).
        var wo = late;
        foreach (var s in new[] { "Spreman", "Laser" })
            wo = await (await admin.PostAsJsonAsync($"/api/work-orders/{wo.Id}/status", new { to = s, version = wo.Version }, Ct)).ReadAsync<WorkOrderDto>();
        await TestData.Check(admin, wo.Id, inspector.Id, "ZavrsnaKontrola", date: Day());
        object Entry(int good, int pressed) => new { workOrderId = wo.Id, date = Day(), operatorEmployeeId = op.Id, equipmentIds = new[] { m3.Id }, pressedQty = pressed, goodQty = good, laborHours = 2.5m };
        await admin.PostAsJsonAsync("/api/production-entries", Entry(12, 20), Ct);
        await admin.PostAsJsonAsync("/api/production-entries", Entry(8, 0), Ct);

        // Deadlines: calibration overdue, supplier evaluation in 10 days, stocktake in 40 days (not shown).
        await admin.PostAsJsonAsync("/api/equipment", new { code = "MS-001", name = "Pomično mjerilo", kind = "MjernoSredstvo", nextCalibrationDate = Day(-1), isActive = true }, Ct);
        await admin.PutAsJsonAsync($"/api/suppliers/{supplier.Id}", new { name = supplier.Name, approvalStatus = "Odobren", nextEvaluationDate = Day(10), version = supplier.Version }, Ct);
        await admin.PostAsJsonAsync("/api/notes", new { text = "Hladnjak ispuhati" }, Ct);

        var d = await (await admin.GetAsync("/api/dashboard", Ct)).ReadAsync<DashboardDto>();

        Assert.Equal(new DashboardKpi(ActiveOrders: 4, OverdueOrders: 1, DueIn7Days: 2, UrgentOrders: 1, GoodToday: 20, EntriesToday: 2, ItemsBelowMinimum: 1), d.Kpi);
        Assert.Equal(2, d.TodayEntries.Count);
        Assert.Equal(new ProductionTotalsDto(0, 20, 20, 0, 5m), d.TodayTotals);
        Assert.Equal(20, d.Chart[^1].GoodQty);

        Assert.Equal(["Kasni AG", "Uskoro GmbH", "Blokirani AG", "Kasnije d.o.o."], d.OrdersByDue.Items.Select(o => o.Customer));
        Assert.Equal(4, d.OrdersByDue.Total);

        // Stock: attention first; inactive item not shown.
        Assert.Equal([StockLevel.Nema, StockLevel.IspodMinimuma], d.Stock.Items.Select(s => s.StockLevel));
        Assert.Equal(2, d.Stock.Total);

        // Warnings: red first (overdue calibration, quarantine, blocked), then amber (lot on inspection, supplier evaluation), by date.
        // Within a colour: earliest date first (quarantined lot received 2026-10-01 < calibration due yesterday < order due in 20 days).
        Assert.Equal(
            [("red", "LotKarantin"), ("red", "Calibration"), ("red", "OrderBlocked"), ("amber", "LotNaKontroli"), ("amber", "SupplierEvaluation")],
            d.Warnings.Select(w => (w.Severity, w.Kind)).ToList());
        Assert.Equal(d.Warnings.Where(w => w.Severity == "red").Select(w => w.Date).Order(), d.Warnings.Where(w => w.Severity == "red").Select(w => w.Date));
        Assert.Equal("Lim crni", d.Warnings.Single(w => w.Kind == "LotKarantin").ItemName);

        Assert.Equal("Hladnjak ispuhati", Assert.Single(d.Notes).Text);

        // The tile link shows exactly what the tile counts.
        var tileList = await (await admin.GetAsync("/api/stock/items?stockLevel=IspodMinimuma&stockLevel=Nema&hasMinimum=true", Ct))
            .ReadAsync<PagedResult<Tds.Api.Features.Stock.ItemStockDto>>();
        Assert.Equal(d.Kpi.ItemsBelowMinimum, tileList.Total);
    }

    [Fact]
    public async Task One_query_per_section()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        await admin.GetAsync("/api/dashboard", Ct); // warm up (security stamp check, query compilation)
        string dbName;
        using (var scope = factory.Services.CreateScope())
            dbName = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection().Database;

        var commands = new List<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "Npgsql",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            // Other tests run in parallel against their own databases: count only this test's database.
            ActivityStopped = a =>
            {
                if (Equals(a.GetTagItem("db.namespace") ?? a.GetTagItem("db.name"), dbName))
                    lock (commands) commands.Add(a.DisplayName + " " + (a.GetTagItem("db.query.text") ?? a.GetTagItem("db.statement")));
            },
        };
        ActivitySource.AddActivityListener(listener);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/dashboard", Ct)).StatusCode);

        var data = commands.Where(c => !c.Contains("asp_net_", StringComparison.Ordinal)).ToList();
        Assert.True(data.Count == 7, $"Expected 7 queries (one per section), got {data.Count}:\n{string.Join("\n---\n", data)}");
    }
}
