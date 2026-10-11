using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Equipment;
using Tds.Api.Features.Files;
using Tds.Api.Features.QualityChecks;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

/// <summary>Phase 3: incoming material checks, instrument calibration stamp (DOMAIN §7.5), evidence files (§7.7).</summary>
public class QualityCheckPhase3Tests(PostgresFixture postgres)
{
    private static readonly string Today = new SystemClock(TimeProvider.System).Today.ToString("yyyy-MM-dd");

    private static async Task<EquipmentDto> Instrument(HttpClient c, string code, string? calibratedOn, string? next, string result = "Uskladjeno")
    {
        var ms = await (await c.PostAsJsonAsync("/api/equipment", new { code, name = "Pomično mjerilo", kind = "MjernoSredstvo", isActive = true }, Ct))
            .ReadAsync<EquipmentDto>(HttpStatusCode.Created);
        if (calibratedOn is not null)
            await c.PostAsJsonAsync($"/api/equipment/{ms.Id}/calibrations", new { calibrationDate = calibratedOn, nextCalibrationDate = next, performedBy = "Lab", result }, Ct);
        return ms;
    }

    private static async Task<FileDto> Upload(HttpClient c, string name = "zapis.pdf")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.7\nzapis\n%%EOF"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", name);
        return await (await c.PostAsync("/api/files", form, Ct)).ReadAsync<FileDto>(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Incoming_material_check_belongs_to_the_lot()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var admin = await factory.AdminAsync();
        var inspector = await TestData.Employee(admin);
        var lot = await TestData.Lot(admin, (await TestData.Item(admin)).Id, (await TestData.Supplier(admin)).Id, 10);

        var r = await TestData.CheckWithWarnings(k, new
        {
            materialLotId = lot.Id, phase = "UlazniMaterijal", characteristic = "Certifikat 3.1 odgovara heat broju", result = "Prihvaceno",
            inspectedByEmployeeId = inspector.Id, inspectionDate = Today,
        });
        Assert.Equal(("H-4471", (Guid?)null, CalibrationStatusAtCheck.BezMjernogSredstva), (r.Data.LotNumber, r.Data.WorkOrderId, r.Data.CalibrationStatusAtCheck));
        Assert.Empty(r.Warnings);

        var onLot = await (await k.GetAsync($"/api/lots/{lot.Id}/quality-checks", Ct)).ReadAsync<List<QualityCheckDto>>();
        Assert.Equal(r.Data.Id, Assert.Single(onLot).Id);

        var bothOwners = await k.PostAsJsonAsync("/api/quality-checks", new
        {
            materialLotId = lot.Id, workOrderId = (await TestData.WorkOrder(k)).Id, phase = "UlazniMaterijal", characteristic = "X", result = "Prihvaceno",
            inspectedByEmployeeId = inspector.Id, inspectionDate = Today,
        }, Ct);
        Assert.Equal("Ulazna kontrola se veže za lot, ne za nalog.", (await bothOwners.ProblemAsync(HttpStatusCode.Conflict)).Detail());
    }

    [Fact]
    public async Task Out_of_calibration_instrument_is_allowed_with_a_permanent_warning()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var inspector = await TestData.Employee(admin);
        var wo = await TestData.WorkOrder(k);
        var expired = await Instrument(k, "MS-001", "2025-01-10", "2026-01-10");
        var noDate = await Instrument(k, "MS-002", null, null);
        var ok = await Instrument(k, "MS-003", "2026-06-01", "2027-06-01");
        var failed = await Instrument(k, "MS-004", "2026-06-01", "2027-06-01", "Neuskladjeno");

        object Body(Guid instrument) => new { workOrderId = wo.Id, phase = "LaserskoRezanje", characteristic = "Mjera 120", result = "Prihvaceno", inspectedByEmployeeId = inspector.Id, inspectionDate = Today, measuringEquipmentId = instrument };

        var r1 = await TestData.CheckWithWarnings(k, Body(expired.Id));
        Assert.Equal(CalibrationStatusAtCheck.VanKalibracije, r1.Data.CalibrationStatusAtCheck);
        Assert.Equal(["Mjerno sredstvo MS-001 van kalibracije — kontrola ima trajnu oznaku."], r1.Warnings);
        Assert.Equal(CalibrationStatusAtCheck.BezDatumaKalibracije, (await TestData.CheckWithWarnings(k, Body(noDate.Id))).Data.CalibrationStatusAtCheck);
        var r3 = await TestData.CheckWithWarnings(k, Body(ok.Id));
        Assert.Equal(CalibrationStatusAtCheck.UKalibraciji, r3.Data.CalibrationStatusAtCheck);
        Assert.Empty(r3.Warnings);
        Assert.Equal(CalibrationStatusAtCheck.VanKalibracije, (await TestData.CheckWithWarnings(k, Body(failed.Id))).Data.CalibrationStatusAtCheck);

        // The flag is permanent: recalibrating the instrument does not change the earlier check.
        await k.PostAsJsonAsync($"/api/equipment/{expired.Id}/calibrations", new { calibrationDate = Today, nextCalibrationDate = "2099-01-01", performedBy = "Lab", result = "Uskladjeno" }, Ct);
        var again = await (await k.GetAsync($"/api/quality-checks/{r1.Data.Id}", Ct)).ReadAsync<QualityCheckDetailDto>();
        Assert.Equal(CalibrationStatusAtCheck.VanKalibracije, again.Check.CalibrationStatusAtCheck);

        // List filter "calibration warning".
        var flagged = await (await k.GetAsync("/api/quality-checks?calibrationWarning=true", Ct)).ReadAsync<PagedResult<QualityCheckDto>>();
        Assert.Equal(["MS-001", "MS-002", "MS-004"], flagged.Items.Select(x => x.MeasuringEquipmentCode).Order());
    }

    [Fact]
    public async Task Machines_and_inactive_instruments_cannot_be_used_as_measuring_equipment()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var inspector = await TestData.Employee(admin);
        var wo = await TestData.WorkOrder(admin);
        var machine = (await admin.GetFromJsonAsync<List<EquipmentOption>>("/api/equipment/options?kind=Masina", JsonOptions, Ct))![0];

        var r = await admin.PostAsJsonAsync("/api/quality-checks", new
        {
            workOrderId = wo.Id, phase = "LaserskoRezanje", characteristic = "Mjera", result = "Prihvaceno", inspectedByEmployeeId = inspector.Id,
            inspectionDate = Today, measuringEquipmentId = machine.Id,
        }, Ct);
        Assert.Equal("Odaberi aktivno mjerno sredstvo.", (await r.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("measuringEquipmentId"));
    }

    [Fact]
    public async Task Evidence_is_added_by_kontrolor_removed_only_by_admin_audited_and_the_file_stays()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var inspector = await TestData.Employee(admin);
        var wo = await TestData.WorkOrder(k);
        var first = await Upload(k, "foto-1.pdf");
        var check = (await TestData.CheckWithWarnings(k, new
        {
            workOrderId = wo.Id, phase = "ZavrsnaKontrola", characteristic = "Vizuelno", result = "Prihvaceno", inspectedByEmployeeId = inspector.Id,
            inspectionDate = Today, evidenceFileIds = new[] { first.Id },
        })).Data;
        var second = await Upload(k, "mjerni-zapis.pdf");

        Assert.Equal(HttpStatusCode.NoContent, (await k.PostAsJsonAsync($"/api/quality-checks/{check.Id}/evidence", new { fileId = second.Id }, Ct)).StatusCode);
        var detail = await (await k.GetAsync($"/api/quality-checks/{check.Id}", Ct)).ReadAsync<QualityCheckDetailDto>();
        Assert.Equal(["foto-1.pdf", "mjerni-zapis.pdf"], detail.Evidence.Select(e => e.OriginalName));

        Assert.Equal(HttpStatusCode.Forbidden, (await k.PostAsync($"/api/quality-checks/{check.Id}/evidence/{first.Id}/remove", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/quality-checks/{check.Id}/evidence/{first.Id}/remove", null, Ct)).StatusCode);

        detail = await (await k.GetAsync($"/api/quality-checks/{check.Id}", Ct)).ReadAsync<QualityCheckDetailDto>();
        Assert.Equal(["mjerni-zapis.pdf"], detail.Evidence.Select(e => e.OriginalName));
        Assert.Equal(HttpStatusCode.OK, (await k.GetAsync($"/api/files/{first.Id}", Ct)).StatusCode); // file itself kept

        using var scope = factory.Services.CreateScope();
        var logs = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.Where(a => a.EntityId == check.Id).OrderBy(a => a.At).ToListAsync(Ct);
        var last = JsonDocument.Parse(logs[^1].ChangesJson).RootElement.GetProperty("EvidenceFileIds");
        Assert.Equal(2, last[0].GetArrayLength());
        Assert.Equal(second.Id.ToString(), Assert.Single(last[1].EnumerateArray()).GetString());
    }
}
