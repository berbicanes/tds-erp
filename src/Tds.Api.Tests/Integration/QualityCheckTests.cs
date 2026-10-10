using System.Net;
using System.Net.Http.Json;
using Tds.Api.Domain;
using Tds.Api.Features.QualityChecks;
using Tds.Api.Features.WorkOrders;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class QualityCheckTests(PostgresFixture postgres)
{
    private static async Task<WorkOrderDto> Wo(HttpClient c, Guid id) => await (await c.GetAsync($"/api/work-orders/{id}", Ct)).ReadAsync<WorkOrderDto>();

    [Fact]
    public async Task Accepted_checks_turn_on_first_piece_and_final_flags_on_the_order()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var admin = await factory.AdminAsync();
        var inspector = await TestData.Employee(admin);
        var wo = await TestData.WorkOrder(k);

        var kk = await TestData.Check(k, wo.Id, inspector.Id, "LaserskoRezanje", date: "2026-10-08");
        Assert.Equal("KK-2026-0001", kk.Number);
        Assert.Equal("Amra Hodžić", kk.InspectedByName);

        var after = await Wo(k, wo.Id);
        Assert.True(after.LaserFirstPieceApproved);
        Assert.False(after.PressFirstPieceApproved);
        Assert.False(after.FinalInspectionPassed);

        await TestData.Check(k, wo.Id, inspector.Id, "HidraulicnoPresanje", result: "Ceka");
        await TestData.Check(k, wo.Id, inspector.Id, "ZavrsnaKontrola", result: "Neuskladjeno");
        after = await Wo(k, wo.Id);
        Assert.False(after.PressFirstPieceApproved);   // Čeka is not accepted
        Assert.False(after.FinalInspectionPassed);     // Neusaglašeno is not accepted

        await TestData.Check(k, wo.Id, inspector.Id, "ZavrsnaKontrola");
        Assert.True((await Wo(k, wo.Id)).FinalInspectionPassed);

        var list = await (await k.GetAsync($"/api/work-orders/{wo.Id}/quality-checks", Ct)).ReadAsync<List<QualityCheckDto>>();
        Assert.Equal(4, list.Count);
    }

    [Fact]
    public async Task Voiding_the_only_accepted_check_turns_the_flag_off_and_is_admin_only()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var inspector = await TestData.Employee(admin);
        var wo = await TestData.WorkOrder(admin);
        var kk = await TestData.Check(k, wo.Id, inspector.Id, "LaserskoRezanje");

        Assert.Equal(HttpStatusCode.Forbidden, (await k.PostAsJsonAsync($"/api/quality-checks/{kk.Id}/void", new { reason = "Greška", version = kk.Version }, Ct)).StatusCode);

        var voided = await (await admin.PostAsJsonAsync($"/api/quality-checks/{kk.Id}/void", new { reason = "Pogrešan nalog", version = kk.Version }, Ct)).ReadAsync<QualityCheckDto>();
        Assert.True(voided.IsVoided);
        Assert.False((await Wo(admin, wo.Id)).LaserFirstPieceApproved);
    }

    [Fact]
    public async Task Incoming_material_phase_and_missing_fields_are_rejected_in_phase_1()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var inspector = await TestData.Employee(admin);
        var wo = await TestData.WorkOrder(admin);

        var incoming = await admin.PostAsJsonAsync("/api/quality-checks", new
        {
            workOrderId = wo.Id, phase = "UlazniMaterijal", characteristic = "Certifikat", result = "Prihvaceno",
            inspectedByEmployeeId = inspector.Id, inspectionDate = "2026-10-08",
        }, Ct);
        Assert.Equal("Na nalogu se unose kontrole faza Lasersko rezanje, Hidraulično presanje i Završna kontrola.",
            (await incoming.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("phase"));

        var empty = await admin.PostAsJsonAsync("/api/quality-checks", new
        {
            workOrderId = wo.Id, phase = "LaserskoRezanje", characteristic = "", sampleSize = 0, result = "Prihvaceno",
            inspectedByEmployeeId = inspector.Id, inspectionDate = "2026-10-08",
        }, Ct);
        var p = await empty.ProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal("Unesi karakteristiku / zahtjev crteža.", p.FieldError("characteristic"));
        Assert.Equal("Veličina uzorka mora biti najmanje 1.", p.FieldError("sampleSize"));
    }

    [Fact]
    public async Task Operater_can_read_checks_but_not_create()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var inspector = await TestData.Employee(admin);
        var wo = await TestData.WorkOrder(admin);
        var op = await factory.ClientAsAsync(Roles.Operater);

        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync($"/api/work-orders/{wo.Id}/quality-checks", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync("/api/quality-checks", new
        {
            workOrderId = wo.Id, phase = "LaserskoRezanje", characteristic = "X", result = "Prihvaceno", inspectedByEmployeeId = inspector.Id, inspectionDate = "2026-10-08",
        }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Future_inspection_date_is_rejected()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var inspector = await TestData.Employee(admin);
        var wo = await TestData.WorkOrder(admin);
        var tomorrow = new Tds.Api.Infrastructure.SystemClock(TimeProvider.System).Today.AddDays(1).ToString("yyyy-MM-dd");

        var r = await admin.PostAsJsonAsync("/api/quality-checks", new
        {
            workOrderId = wo.Id, phase = "LaserskoRezanje", characteristic = "Mjera", result = "Prihvaceno",
            inspectedByEmployeeId = inspector.Id, inspectionDate = tomorrow,
        }, Ct);

        Assert.Equal("Datum kontrole ne može biti u budućnosti.", (await r.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("inspectionDate"));
    }
}
