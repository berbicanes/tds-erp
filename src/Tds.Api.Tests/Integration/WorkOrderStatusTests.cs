using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.WorkOrders;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

/// <summary>Status changes through the API with gates computed from real quality checks (no temporary flags).</summary>
public class WorkOrderStatusTests(PostgresFixture postgres)
{
    private static async Task<WorkOrderDto> Move(HttpClient c, WorkOrderDto wo, string to, string? reason = null) =>
        await (await c.PostAsJsonAsync($"/api/work-orders/{wo.Id}/status", new { to, reason, version = wo.Version }, Ct)).ReadAsync<WorkOrderDto>();

    [Fact]
    public async Task Full_path_to_delivery_uses_real_quality_checks()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var inspector = await TestData.Employee(admin);
        var wo = await TestData.WorkOrder(k, qty: 2);

        wo = await Move(k, wo, "Spreman");
        wo = await Move(k, wo, "Laser");

        var blocked = await k.PostAsJsonAsync($"/api/work-orders/{wo.Id}/status", new { to = "Presanje", version = wo.Version }, Ct);
        Assert.Equal("Prvi komad sa lasera nije potvrđen.", (await blocked.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        await TestData.Check(k, wo.Id, inspector.Id, "LaserskoRezanje");
        wo = await (await k.GetAsync($"/api/work-orders/{wo.Id}", Ct)).ReadAsync<WorkOrderDto>();
        wo = await Move(k, wo, "Presanje");

        await TestData.Check(k, wo.Id, inspector.Id, "HidraulicnoPresanje");
        wo = await (await k.GetAsync($"/api/work-orders/{wo.Id}", Ct)).ReadAsync<WorkOrderDto>();
        wo = await Move(k, wo, "Kontrola");

        var noFinal = await k.PostAsJsonAsync($"/api/work-orders/{wo.Id}/status", new { to = "Zavrseno", version = wo.Version }, Ct);
        Assert.Equal("Završna kontrola nije prihvaćena.", (await noFinal.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        await TestData.Check(k, wo.Id, inspector.Id, "ZavrsnaKontrola");
        wo = await (await k.PostAsJsonAsync($"/api/work-orders/{wo.Id}/quality-approval", new { qualityApproval = "Prihvaceno", version = (await (await k.GetAsync($"/api/work-orders/{wo.Id}", Ct)).ReadAsync<WorkOrderDto>()).Version }, Ct)).ReadAsync<WorkOrderDto>();
        wo = await Move(k, wo, "Zavrseno");
        Assert.Equal(WorkOrderStatus.Zavrseno, wo.Status);

        // No good pieces recorded yet → not ready for shipment (production entries come next).
        var notReady = await k.PostAsJsonAsync($"/api/work-orders/{wo.Id}/status", new { to = "Isporuceno", version = wo.Version }, Ct);
        Assert.StartsWith("Nalog nije spreman za isporuku", (await notReady.ProblemAsync(HttpStatusCode.Conflict)).Detail());
    }

    [Fact]
    public async Task Transition_options_show_only_table_targets_with_gate_reasons()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var wo = await TestData.WorkOrder(k);
        wo = await Move(k, wo, "Spreman");
        wo = await Move(k, wo, "Laser");

        var options = await (await k.GetAsync($"/api/work-orders/{wo.Id}/transitions", Ct)).ReadAsync<List<TransitionCheck>>();
        Assert.Equal([WorkOrderStatus.Presanje, WorkOrderStatus.Kontrola, WorkOrderStatus.Doraditi, WorkOrderStatus.Otkazano], options.Select(o => o.To));
        var presanje = options.Single(o => o.To == WorkOrderStatus.Presanje);
        Assert.False(presanje.Allowed);
        Assert.False(presanje.CanOverride);           // Kontrolor
        Assert.Equal("Prvi komad sa lasera nije potvrđen.", presanje.Reason);

        var admin = await factory.AdminAsync();
        var adminOptions = await (await admin.GetAsync($"/api/work-orders/{wo.Id}/transitions", Ct)).ReadAsync<List<TransitionCheck>>();
        Assert.True(adminOptions.Single(o => o.To == WorkOrderStatus.Presanje).CanOverride);
    }

    [Fact]
    public async Task Admin_override_and_reopen_write_the_reason_into_audit_log()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var wo = await TestData.WorkOrder(admin);
        wo = await Move(admin, wo, "Spreman");
        wo = await Move(admin, wo, "Laser");

        var noReason = await admin.PostAsJsonAsync($"/api/work-orders/{wo.Id}/status", new { to = "Presanje", version = wo.Version }, Ct);
        Assert.Equal("Prvi komad sa lasera nije potvrđen. Za preskakanje uslova upiši razlog.", (await noReason.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        wo = await Move(admin, wo, "Presanje", "Samo plazma i savijanje, laser se ne koristi");
        wo = await Move(admin, wo, "Otkazano");
        wo = await Move(admin, wo, "Zaprimljen", "Kupac ponovo potvrdio narudžbu");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reasons = (await db.AuditLogs.Where(a => a.EntityId == wo.Id && a.Action == AuditAction.Update).OrderBy(a => a.At).ToListAsync(Ct))
            .Select(a => JsonDocument.Parse(a.ChangesJson).RootElement)
            .Where(c => c.TryGetProperty("reason", out _))
            .Select(c => c.GetProperty("reason")[1].GetString())
            .ToList();
        Assert.Equal(
            ["Preskočen uslov \"Prvi komad sa lasera nije potvrđen.\": Samo plazma i savijanje, laser se ne koristi", "Kupac ponovo potvrdio narudžbu"],
            reasons);

        var statusChanges = (await db.AuditLogs.Where(a => a.EntityId == wo.Id).ToListAsync(Ct))
            .Count(a => JsonDocument.Parse(a.ChangesJson).RootElement.TryGetProperty("Status", out _));
        Assert.Equal(6, statusChanges); // Create + 5 changes
    }

    [Fact]
    public async Task Blokirano_blocks_forward_and_operater_cannot_change_status()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var wo = await TestData.WorkOrder(admin);
        wo = await (await admin.PostAsJsonAsync($"/api/work-orders/{wo.Id}/quality-approval", new { qualityApproval = "Blokirano", version = wo.Version }, Ct)).ReadAsync<WorkOrderDto>();

        var forward = await admin.PostAsJsonAsync($"/api/work-orders/{wo.Id}/status", new { to = "Spreman", reason = "pokušaj", version = wo.Version }, Ct);
        Assert.Equal("Kvalitet je blokiran. Dozvoljen je samo prelaz u Doraditi ili Otkazano.", (await forward.ProblemAsync(HttpStatusCode.Conflict)).Detail());
        Assert.Equal(WorkOrderStatus.Otkazano, (await Move(admin, wo, "Otkazano")).Status);

        var op = await factory.ClientAsAsync(Roles.Operater);
        var wo2 = await TestData.WorkOrder(admin, "Drugi kupac");
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync($"/api/work-orders/{wo2.Id}/status", new { to = "Spreman", version = wo2.Version }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync($"/api/work-orders/{wo2.Id}/transitions", Ct)).StatusCode);
    }

    [Fact]
    public async Task Stale_version_on_status_change_is_409_concurrency()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var wo = await TestData.WorkOrder(admin);
        await Move(admin, wo, "Spreman");
        var stale = await admin.PostAsJsonAsync($"/api/work-orders/{wo.Id}/status", new { to = "CekaMaterijal", version = wo.Version }, Ct);
        Assert.Equal("concurrency", (await stale.ProblemAsync(HttpStatusCode.Conflict)).GetProperty("code").GetString());
    }
}
