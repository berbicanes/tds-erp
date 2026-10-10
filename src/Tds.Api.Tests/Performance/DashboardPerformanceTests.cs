using System.Diagnostics;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Performance;

/// <summary>
/// ROADMAP Phase 2: GET /api/dashboard with 5.000 production entries. Explicit = never part of a normal
/// `dotnet test`; run manually with scripts/perf-dashboard.sh. Prints p50 / p95 / max, no pass/fail threshold.
/// The data lives only in this test's throw-away Testcontainers database (not in the dev or production seed).
/// </summary>
public class DashboardPerformanceTests(PostgresFixture postgres)
{
    private const int Entries = 5_000;
    private const int Warmup = 5;
    private const int Runs = 30;

    [Fact(Explicit = true)]
    public async Task Dashboard_with_5000_production_entries()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var seeded = await Seed(factory);

        for (var i = 0; i < Warmup; i++)
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/dashboard", Ct)).StatusCode);

        var times = new List<double>();
        for (var i = 0; i < Runs; i++)
        {
            var sw = Stopwatch.StartNew();
            var r = await admin.GetAsync("/api/dashboard", Ct);
            await r.Content.ReadAsByteArrayAsync(Ct);
            sw.Stop();
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            times.Add(sw.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        double P(double q) => times[(int)Math.Ceiling(q * times.Count) - 1];
        var report = $"""
            GET /api/dashboard — {Runs} runs after {Warmup} warm-up runs
            data: {seeded}
            p50 = {P(0.50):F1} ms, p95 = {P(0.95):F1} ms, max = {times[^1]:F1} ms
            """;
        TestContext.Current.TestOutputHelper?.WriteLine(report);
        Console.WriteLine(report);
        var outFile = Path.Combine(AppContext.BaseDirectory, "dashboard-perf.txt");
        await File.WriteAllTextAsync(outFile, report, Ct);
    }

    /// <summary>Realistic volume: ~1 year of a small workshop. Inserted directly (bulk), consistent with the stock rules.</summary>
    private static async Task<string> Seed(TdsApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = new SystemClock(TimeProvider.System).Today;
        var rnd = new Random(42);
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        var employees = Enumerable.Range(1, 10).Select(i => new Employee { FullName = $"Radnik {i:D2}", Position = "Operater" }).ToList();
        db.Employees.AddRange(employees);
        var machines = await db.Equipment.Where(e => e.Kind == EquipmentKind.Masina).ToListAsync(Ct);
        var suppliers = Enumerable.Range(1, 8).Select(i => new Supplier { Name = $"Dobavljač {i}", ApprovalStatus = SupplierApprovalStatus.Odobren, NextEvaluationDate = today.AddDays(rnd.Next(-10, 120)) }).ToList();
        db.Suppliers.AddRange(suppliers);
        string[] grades = ["S235", "S355", "1.4301", "AlMg3"];
        var items = Enumerable.Range(0, 40).Select(i => new MaterialItem
        {
            Name = i % 4 == 2 ? "Lim inox" : i % 4 == 3 ? "Lim aluminij" : "Lim crni", Grade = grades[i % 4], ThicknessMm = 1 + i / 4 * 1.5m,
            Unit = i % 4 == 2 ? Unit.kg : Unit.tabla, MinimumStock = i % 3 == 0 ? null : 5 + i % 10, NextStocktakeDate = today.AddDays(rnd.Next(-5, 90)),
        }).ToList();
        db.MaterialItems.AddRange(items);
        await db.SaveChangesAsync(Ct);

        // 150 lots, accepted (some on hold), each with its Prijem.
        var lots = new List<MaterialLot>();
        for (var i = 0; i < 150; i++)
        {
            var item = items[i % items.Count];
            var (lot, receipt) = MaterialLot.Receive($"H-{10000 + i}", item.Id, suppliers[i % suppliers.Count].Id, SupplierApprovalStatus.Odobren,
                today.AddDays(-rnd.Next(1, 360)), 200 + rnd.Next(0, 800), today);
            var decision = i % 15 == 0 ? ReceiptDecision.NaKontroli : i % 25 == 0 ? ReceiptDecision.Karantin : ReceiptDecision.Prihvacen;
            if (decision != ReceiptDecision.NaKontroli)
                lot.Decide(decision, employees[0].Id, null, receipt.Quantity, today);
            lots.Add(lot);
            db.MaterialLots.Add(lot);
            db.StockMovements.Add(receipt);
        }
        await db.SaveChangesAsync(Ct);
        var accepted = lots.Where(l => l.ReceiptDecision == ReceiptDecision.Prihvacen).ToList();

        // 300 orders over the year; ~45 still active.
        var statuses = new[] { WorkOrderStatus.Spreman, WorkOrderStatus.Laser, WorkOrderStatus.Presanje, WorkOrderStatus.Kontrola, WorkOrderStatus.Doraditi };
        var orders = Enumerable.Range(1, 300).Select(i => new WorkOrder
        {
            Number = $"RN-{today.Year}-{i:D4}", Customer = $"Kupac {i % 35}", ProductDescription = $"Pozicija {i}", OrderedQty = 50 + i % 400,
            DueDate = today.AddDays(i > 255 ? rnd.Next(-5, 40) : -rnd.Next(1, 360)),
            Status = i > 255 ? statuses[i % statuses.Length] : WorkOrderStatus.Isporuceno,
            Priority = i % 9 == 0 ? Priority.Hitno : Priority.Normalan,
            QualityApproval = i > 255 && i % 20 == 0 ? QualityApproval.Blokirano : QualityApproval.Prihvaceno,
        }).ToList();
        db.WorkOrders.AddRange(orders);
        await db.SaveChangesAsync(Ct);

        // Reservations and quality checks.
        foreach (var (wo, i) in orders.Select((w, i) => (w, i)))
        {
            var lot = accepted[i % accepted.Count];
            db.WorkOrderMaterials.Add(WorkOrderMaterial.Link(wo.Id, lot, 1 + i % 20));
            foreach (var phase in new[] { InspectionPhase.LaserskoRezanje, InspectionPhase.HidraulicnoPresanje })
                db.QualityChecks.Add(new QualityCheck
                {
                    Number = $"KK-{today.Year}-{i * 2 + (int)phase:D5}", WorkOrderId = wo.Id, Phase = phase, Characteristic = "Mjera",
                    Result = InspectionResult.Prihvaceno, InspectedByEmployeeId = employees[0].Id, InspectionDate = today.AddDays(-rnd.Next(0, 300)),
                });
        }
        await db.SaveChangesAsync(Ct);

        // 5.000 production entries over ~250 working days, ~40% with consumption (Izdavanje).
        var perDay = new Dictionary<DateOnly, int>();
        var issued = 0;
        for (var i = 0; i < Entries; i++)
        {
            var date = today.AddDays(-rnd.Next(0, 365));
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                date = date.AddDays(-2);
            perDay[date] = perDay.GetValueOrDefault(date) + 1;
            var wo = orders[rnd.Next(orders.Count)];
            var lot = accepted[rnd.Next(accepted.Count)];
            var consumes = i % 5 < 2;
            var e = new ProductionEntry
            {
                Number = $"DP-{date:yyyyMMdd}-{perDay[date]:D2}", Date = date, WorkOrderId = wo.Id, OperatorEmployeeId = employees[i % employees.Count].Id,
                MaterialLotId = consumes ? lot.Id : null, LaserCutQty = consumes ? 20 + i % 60 : 0, PressedQty = 10 + i % 40, GoodQty = i % 3 == 0 ? 5 + i % 20 : 0,
                ScrapQty = i % 7 == 0 ? 1 : 0, LaborHours = 1 + i % 8, MaterialConsumed = consumes ? 0.5m + i % 3 : null,
            };
            e.Equipment.Add(new ProductionEntryEquipment { ProductionEntryId = e.Id, EquipmentId = machines[i % machines.Count].Id });
            db.ProductionEntries.Add(e);
            if (consumes)
            {
                db.StockMovements.Add(ProductionEntryRules.Issue(e, lot.Id, e.MaterialConsumed!.Value));
                issued++;
            }
            if (i % 500 == 499)
            {
                db.ChangeTracker.DetectChanges();
                await db.SaveChangesAsync(Ct);
                db.ChangeTracker.Clear();
            }
        }
        db.ChangeTracker.DetectChanges();
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();

        for (var i = 0; i < 20; i++)
            db.Notes.Add(new Note { Text = $"Bilješka {i}" });
        await db.SaveChangesAsync(Ct);

        var counts = $"{await db.ProductionEntries.CountAsync(Ct)} dnevnih zapisa, {await db.StockMovements.CountAsync(Ct)} pokreta zalihe ({issued} izdavanja), " +
                     $"{orders.Count} naloga ({orders.Count(o => !WorkOrder.FinalStatuses.Contains(o.Status))} aktivnih), {lots.Count} lotova, {items.Count} artikala, " +
                     $"{await db.QualityChecks.CountAsync(Ct)} kontrola, {await db.AuditLogs.CountAsync(Ct)} redova AuditLog-a";
        return counts;
    }
}
