using System.Net;
using System.Net.Http.Json;
using Tds.Api.Domain;
using Tds.Api.Features.NonConformities;
using Tds.Api.Features.QualityChecks;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

/// <summary>DOMAIN §7.1, §7.1a, §7.3 through the API.</summary>
public class NonConformityTests(PostgresFixture postgres)
{
    private static readonly string Today = new SystemClock(TimeProvider.System).Today.ToString("yyyy-MM-dd");

    private static object Body(NcPrefillDto p, uint version = 0, string decision = "CekaOdluku", string? cause = null, string? action = null, string? effective = null, string? due = null) => new
    {
        workOrderId = p.WorkOrderId, materialLotId = p.MaterialLotId, supplierId = p.SupplierId, qualityCheckId = p.QualityCheckId,
        source = p.Source.ToString(), description = p.Description, productDecision = decision, rootCause = cause, correctiveAction = action,
        effectivenessCheckDate = effective, dueDate = due, version,
    };

    [Fact]
    public async Task Failed_check_offers_a_prefilled_nc_and_allows_only_one()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var inspector = await TestData.Employee(admin);
        var wo = await TestData.WorkOrder(k);
        var check = await TestData.Check(k, wo.Id, inspector.Id, "LaserskoRezanje", "Neuskladjeno", Today);

        // Nothing is created automatically (DOMAIN §7.1).
        Assert.Empty(await (await k.GetAsync($"/api/work-orders/{wo.Id}/non-conformities", Ct)).ReadAsync<List<NonConformityDto>>());

        var prefill = await (await k.GetAsync($"/api/non-conformities/prefill?qualityCheckId={check.Id}", Ct)).ReadAsync<NcPrefillDto>();
        Assert.Equal((NcSource.Laser, wo.Id, (Guid?)null), (prefill.Source, prefill.WorkOrderId, prefill.ExistingId));
        Assert.Equal($"Kontrola {check.Number}: Mjera 120 ±0,5 mm. Kriterij: 119,5–120,5 mm. Izmjereno: 120,1 mm.", prefill.Description);

        var nc = await (await k.PostAsJsonAsync("/api/non-conformities", Body(prefill), Ct)).ReadAsync<NonConformityDto>(HttpStatusCode.Created);
        Assert.Equal((NcStatus.Otvorena, check.Number), (nc.Status, nc.QualityCheckNumber));
        Assert.Matches(@"^NC-\d{4}-001$", nc.Number);

        // Second offer leads to the existing NC; a second create is refused.
        prefill = await (await k.GetAsync($"/api/non-conformities/prefill?qualityCheckId={check.Id}", Ct)).ReadAsync<NcPrefillDto>();
        Assert.Equal((nc.Id, nc.Number), (prefill.ExistingId!.Value, prefill.ExistingNumber));
        var again = await k.PostAsJsonAsync("/api/non-conformities", Body(prefill), Ct);
        Assert.Equal($"Za kontrolu {check.Number} već postoji {nc.Number}.", (await again.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        // After voiding, a new NC for the same check is possible.
        await admin.PostAsJsonAsync($"/api/non-conformities/{nc.Id}/void", new { reason = "Pogrešan opis", version = nc.Version }, Ct);
        Assert.Equal(HttpStatusCode.Created, (await k.PostAsJsonAsync("/api/non-conformities", Body(prefill), Ct)).StatusCode);
    }

    [Fact]
    public async Task Accepted_check_cannot_carry_an_nc()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var inspector = await TestData.Employee(admin);
        var wo = await TestData.WorkOrder(admin);
        var ok = await TestData.Check(admin, wo.Id, inspector.Id, "ZavrsnaKontrola", "Prihvaceno", Today);
        var prefill = await (await admin.GetAsync($"/api/non-conformities/prefill?qualityCheckId={ok.Id}", Ct)).ReadAsync<NcPrefillDto>();
        var r = await admin.PostAsJsonAsync("/api/non-conformities", Body(prefill), Ct);
        Assert.Equal("NC se veže samo za neponištenu kontrolu sa rezultatom Neusklađeno.", (await r.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("qualityCheckId"));
    }

    [Fact]
    public async Task Status_path_with_conditions_closed_is_read_only_and_reopening_works()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var op = await factory.ClientAsAsync(Roles.Operater);

        var nc = await (await k.PostAsJsonAsync("/api/non-conformities", new { source = "Kupac", description = "Reklamacija: ogrebotine na 3 kom", productDecision = "CekaOdluku", version = 0 }, Ct))
            .ReadAsync<NonConformityDto>(HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync($"/api/non-conformities/{nc.Id}/status", new { status = "RadnjaUToku", version = nc.Version }, Ct)).StatusCode);

        async Task<HttpResponseMessage> Move(string to) =>
            await k.PostAsJsonAsync($"/api/non-conformities/{nc.Id}/status", new { status = to, version = nc.Version }, Ct);
        async Task Save(object body) => nc = await (await k.PutAsJsonAsync($"/api/non-conformities/{nc.Id}", body, Ct)).ReadAsync<NonConformityDto>();
        object Fields(string decision, string? cause = null, string? action = null, string? effective = null) =>
            new { source = "Kupac", description = nc.Description, productDecision = decision, rootCause = cause, correctiveAction = action, effectivenessCheckDate = effective, version = nc.Version };

        Assert.Equal("Prije početka radnje odaberi odluku o proizvodu.", (await (await Move("RadnjaUToku")).ProblemAsync(HttpStatusCode.Conflict)).Detail());
        await Save(Fields("Dorada"));
        nc = await (await Move("RadnjaUToku")).ReadAsync<NonConformityDto>();

        Assert.Equal("Za provjeru efektivnosti popuni analizu uzroka i korektivnu radnju.", (await (await Move("ProvjeraEfektivnosti")).ProblemAsync(HttpStatusCode.Conflict)).Detail());
        Assert.EndsWith("ide se korak po korak.", (await (await Move("Zatvorena")).ProblemAsync(HttpStatusCode.Conflict)).Detail());
        await Save(Fields("Dorada", "Nezaštićena površina pri transportu", "Folija i separatori na paleti"));
        nc = await (await Move("ProvjeraEfektivnosti")).ReadAsync<NonConformityDto>();

        Assert.Equal("Za zatvaranje upiši datum provjere efektivnosti.", (await (await Move("Zatvorena")).ProblemAsync(HttpStatusCode.Conflict)).Detail());
        await Save(Fields("Dorada", "Nezaštićena površina pri transportu", "Folija i separatori na paleti", Today));
        nc = await (await Move("Zatvorena")).ReadAsync<NonConformityDto>();

        var edit = await k.PutAsJsonAsync($"/api/non-conformities/{nc.Id}", Fields("Skart"), Ct);
        Assert.Equal($"{nc.Number} je zatvorena. Za izmjenu je vrati u \"Provjera efektivnosti\".", (await edit.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        nc = await (await Move("ProvjeraEfektivnosti")).ReadAsync<NonConformityDto>();   // reopening
        var detail = await (await k.GetAsync($"/api/non-conformities/{nc.Id}", Ct)).ReadAsync<NcDetailDto>();
        Assert.Equal([NcStatus.Zatvorena, NcStatus.RadnjaUToku], detail.Transitions.Select(t => t.To));
        Assert.All(detail.Transitions, t => Assert.True(t.Allowed));

        // Kontrolor cannot void; stale version → 409 concurrency.
        Assert.Equal(HttpStatusCode.Forbidden, (await k.PostAsJsonAsync($"/api/non-conformities/{nc.Id}/void", new { reason = "x", version = nc.Version }, Ct)).StatusCode);
        var stale = await k.PostAsJsonAsync($"/api/non-conformities/{nc.Id}/status", new { status = "Zatvorena", version = nc.Version - 1 }, Ct);
        Assert.Equal("Zapis je u međuvremenu izmijenjen. Osvježi i pokušaj ponovo.", (await stale.ProblemAsync(HttpStatusCode.Conflict)).Detail());
    }

    [Fact]
    public async Task Overdue_nc_is_a_red_dashboard_warning_until_closed()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var nc = await (await k.PostAsJsonAsync("/api/non-conformities", new
        {
            source = "Kupac", description = "Reklamacija Metalbau", productDecision = "Dorada", rootCause = "Uzrok", correctiveAction = "Radnja",
            effectivenessCheckDate = Today, dueDate = "2020-01-01", version = 0,
        }, Ct)).ReadAsync<NonConformityDto>(HttpStatusCode.Created);

        var w = Assert.Single((await (await k.GetAsync("/api/dashboard", Ct)).ReadAsync<Tds.Api.Features.Dashboard.DashboardDto>()).Warnings, x => x.Kind == "NcDue");
        Assert.Equal(("red", nc.Number, "Reklamacija Metalbau"), (w.Severity, w.Title, w.Detail));

        foreach (var s in new[] { "RadnjaUToku", "ProvjeraEfektivnosti", "Zatvorena" })
            nc = await (await k.PostAsJsonAsync($"/api/non-conformities/{nc.Id}/status", new { status = s, version = nc.Version }, Ct)).ReadAsync<NonConformityDto>();
        Assert.DoesNotContain((await (await k.GetAsync("/api/dashboard", Ct)).ReadAsync<Tds.Api.Features.Dashboard.DashboardDto>()).Warnings, x => x.Kind == "NcDue");
    }

    [Fact]
    public async Task List_filters_overdue_and_status()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        await k.PostAsJsonAsync("/api/non-conformities", new { source = "Audit", description = "Zastario obrazac", productDecision = "CekaOdluku", dueDate = "2020-01-01", version = 0 }, Ct);
        await k.PostAsJsonAsync("/api/non-conformities", new { source = "Ostalo", description = "Bez roka", productDecision = "CekaOdluku", version = 0 }, Ct);

        var overdue = await (await k.GetAsync("/api/non-conformities?overdue=true", Ct)).ReadAsync<PagedResult<NonConformityDto>>();
        Assert.Equal(["Zastario obrazac"], overdue.Items.Select(x => x.Description));
        var open = await (await k.GetAsync("/api/non-conformities?status=Otvorena&q=roka", Ct)).ReadAsync<PagedResult<NonConformityDto>>();
        Assert.Equal(["Bez roka"], open.Items.Select(x => x.Description));
    }
}
