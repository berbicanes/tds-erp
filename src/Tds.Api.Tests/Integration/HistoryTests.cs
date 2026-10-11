using System.Net;
using System.Net.Http.Json;
using Tds.Api.Domain;
using Tds.Api.Features.Equipment;
using Tds.Api.Features.History;
using Tds.Api.Features.NonConformities;
using Tds.Api.Features.WorkOrders;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

/// <summary>UI §6: unified, readable history (names instead of ids, reasons as their own line).</summary>
public class HistoryTests(PostgresFixture postgres)
{
    private static readonly string Today = new SystemClock(TimeProvider.System).Today.ToString("yyyy-MM-dd");

    [Fact]
    public async Task Order_history_contains_its_checks_ncs_entries_and_lot_links_with_names_and_reasons()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var inspector = await TestData.Employee(admin, "Amra Hodžić");
        var lot = await TestData.Lot(admin, (await TestData.Item(admin)).Id, (await TestData.Supplier(admin)).Id, 10);
        var wo = await TestData.WorkOrder(admin);
        await admin.PostAsJsonAsync($"/api/work-orders/{wo.Id}/materials", new { materialLotId = lot.Id, reservedQty = 2 }, Ct);

        // Override with a reason, then a failed check, its NC, and a voided check.
        wo = await (await admin.PostAsJsonAsync($"/api/work-orders/{wo.Id}/status", new { to = "Spreman", version = wo.Version }, Ct)).ReadAsync<WorkOrderDto>();
        wo = await (await admin.PostAsJsonAsync($"/api/work-orders/{wo.Id}/status", new { to = "Laser", version = wo.Version }, Ct)).ReadAsync<WorkOrderDto>();
        await admin.PostAsJsonAsync($"/api/work-orders/{wo.Id}/status", new { to = "Presanje", reason = "Samo plazma, laser se ne koristi", version = wo.Version }, Ct);
        var failed = await TestData.Check(admin, wo.Id, inspector.Id, "LaserskoRezanje", "Neuskladjeno", Today);
        var prefill = await (await admin.GetAsync($"/api/non-conformities/prefill?qualityCheckId={failed.Id}", Ct)).ReadAsync<NcPrefillDto>();
        await admin.PostAsJsonAsync("/api/non-conformities", new { workOrderId = wo.Id, qualityCheckId = failed.Id, source = "Laser", description = prefill.Description, productDecision = "CekaOdluku", version = 0 }, Ct);
        var wrong = await TestData.Check(admin, wo.Id, inspector.Id, "ZavrsnaKontrola", "Prihvaceno", Today);
        await admin.PostAsJsonAsync($"/api/quality-checks/{wrong.Id}/void", new { reason = "Unesena na pogrešan nalog", version = wrong.Version }, Ct);

        var h = await (await admin.GetAsync($"/api/history/work-order/{wo.Id}", Ct)).ReadAsync<List<HistoryEntryDto>>();

        Assert.Equal(h.OrderByDescending(e => e.At).Select(e => e.Id), h.Select(e => e.Id));
        Assert.Contains(h, e => e.RecordLabel == $"Nalog {wo.Number}" && e.Reason == "Preskočen uslov \"Prvi komad sa lasera nije potvrđen.\": Samo plazma, laser se ne koristi"
                                && e.Changes.Any(c => c.Field == "Status" && c.Old!.GetValue<string>() == "Laser" && c.New!.GetValue<string>() == "Presanje"));
        var voided = Assert.Single(h, e => e.Action == AuditAction.Void);
        Assert.Equal(($"Kontrola {wrong.Number}", "Unesena na pogrešan nalog"), (voided.RecordLabel, voided.Reason));
        Assert.DoesNotContain(voided.Changes, c => c.Field is "IsVoided" or "VoidedReason");

        var checkCreated = Assert.Single(h, e => e.Action == AuditAction.Create && e.RecordLabel == $"Kontrola {failed.Number}");
        Assert.Equal("Amra Hodžić", checkCreated.Changes.Single(c => c.Field == "InspectedByEmployeeId").New!.GetValue<string>());
        Assert.Equal("BezMjernogSredstva", checkCreated.Changes.Single(c => c.Field == "CalibrationStatusAtCheck").New!.GetValue<string>());
        Assert.Contains(h, e => e.RecordLabel.StartsWith("NC NC-") && e.Changes.Any(c => c.Field == "QualityCheckId" && c.New!.GetValue<string>() == failed.Number));
        Assert.Contains(h, e => e.RecordLabel == "Lot H-4471 na nalogu" && e.Changes.Any(c => c.Field == "MaterialLotId" && c.New!.GetValue<string>() == "H-4471"));
        Assert.All(h, e => Assert.Equal("admin", e.UserName));
        Assert.DoesNotContain(h.SelectMany(e => e.Changes), c => c.New?.ToJsonString().Contains(inspector.Id.ToString()) == true);
    }

    [Fact]
    public async Task Link_sets_show_names_and_unknown_scope_is_404()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var op = await factory.ClientAsAsync(Roles.Operater);
        var nc = await (await admin.PostAsJsonAsync("/api/non-conformities", new { source = "Audit", description = "Interni audit, nalaz 3", productDecision = "CekaOdluku", version = 0 }, Ct))
            .ReadAsync<NonConformityDto>(HttpStatusCode.Created);

        var h = await (await op.GetAsync($"/api/history/non-conformity/{nc.Id}", Ct)).ReadAsync<List<HistoryEntryDto>>();
        var created = Assert.Single(h);
        Assert.Equal(("Audit", "CekaOdluku", "Otvorena"),
            (created.Changes.Single(c => c.Field == "Source").New!.GetValue<string>(), created.Changes.Single(c => c.Field == "ProductDecision").New!.GetValue<string>(),
             created.Changes.Single(c => c.Field == "Status").New!.GetValue<string>()));
        Assert.DoesNotContain(created.Changes, c => c.Field == "IsVoided");

        Assert.Equal(HttpStatusCode.NotFound, (await op.GetAsync($"/api/history/nesto/{nc.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await op.GetAsync($"/api/history/lot/{nc.Id}", Ct)).StatusCode);
    }
}
