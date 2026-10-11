using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Equipment;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class EquipmentTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Startup_seeds_four_machines_once()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<EquipmentSeeder>().SeedAsync(Ct);

        var list = await (await admin.GetAsync("/api/equipment", Ct)).ReadAsync<PagedResult<EquipmentDto>>();
        Assert.Equal(["M-01", "M-02", "M-03", "M-04"], list.Items.Select(x => x.Code));
        Assert.All(list.Items, x => Assert.Equal(EquipmentKind.Masina, x.Kind));
        Assert.Equal("CNC fiber laser 6 kW", list.Items[0].Name);
    }

    [Fact]
    public async Task Seed_does_not_overwrite_edited_machine()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var m1 = (await (await admin.GetAsync("/api/equipment?q=M-01", Ct)).ReadAsync<PagedResult<EquipmentDto>>()).Items.Single();
        await admin.PutAsJsonAsync($"/api/equipment/{m1.Id}", new { code = "M-01", name = "Laser Bodor 6 kW", kind = "Masina", isActive = true, version = m1.Version }, Ct);

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<EquipmentSeeder>().SeedAsync(Ct);

        var after = await (await admin.GetAsync($"/api/equipment/{m1.Id}", Ct)).ReadAsync<EquipmentDto>();
        Assert.Equal("Laser Bodor 6 kW", after.Name);
    }

    [Fact]
    public async Task Kontrolor_creates_measuring_instrument_and_its_calibration_history_drives_the_dates()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var kontrolor = await factory.ClientAsAsync(Roles.Kontrolor);

        var ms = await (await kontrolor.PostAsJsonAsync("/api/equipment", new
        {
            code = "MS-001", name = "Pomično mjerilo 150 mm", kind = "MjernoSredstvo", manufacturer = "Mitutoyo",
            calibrationIntervalMonths = 12, isActive = true,
        }, Ct)).ReadAsync<EquipmentDto>(HttpStatusCode.Created);
        Assert.Equal(CalibrationStatusAtCheck.BezDatumaKalibracije, ms.CalibrationStatus);
        Assert.Null(ms.NextCalibrationDate);

        await kontrolor.PostAsJsonAsync($"/api/equipment/{ms.Id}/calibrations", new { calibrationDate = "2025-03-15", performedBy = "Metrološki lab", result = "Uskladjeno" }, Ct);
        await kontrolor.PostAsJsonAsync($"/api/equipment/{ms.Id}/calibrations", new { calibrationDate = "2026-03-15", performedBy = "Metrološki lab", result = "Uskladjeno" }, Ct);
        ms = await (await kontrolor.GetAsync($"/api/equipment/{ms.Id}", Ct)).ReadAsync<EquipmentDto>();
        Assert.Equal((new DateOnly(2026, 3, 15), new DateOnly(2027, 3, 15)), (ms.LastCalibrationDate!.Value, ms.NextCalibrationDate!.Value));
        Assert.Equal(CalibrationStatusAtCheck.UKalibraciji, ms.CalibrationStatus);

        var machines = await kontrolor.GetFromJsonAsync<List<EquipmentOption>>("/api/equipment/options?kind=Masina", JsonOptions, Ct);
        Assert.DoesNotContain(machines!, o => o.Code == "MS-001");
        Assert.Equal(4, machines!.Count);
    }

    [Fact]
    public async Task Duplicate_code_and_interval_on_machine_are_rejected()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();

        var dup = await admin.PostAsJsonAsync("/api/equipment", new { code = "M-01", name = "Duplikat", kind = "Masina", isActive = true }, Ct);
        Assert.Equal("Oznaka M-01 je već zauzeta.", (await dup.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("code"));

        var interval = await admin.PostAsJsonAsync("/api/equipment",
            new { code = "M-05", name = "Aparat za varenje", kind = "Masina", calibrationIntervalMonths = 12, isActive = true }, Ct);
        Assert.Equal("Interval kalibracije se unosi samo za mjerna sredstva.",
            (await interval.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("calibrationIntervalMonths"));
    }

    [Fact]
    public async Task Operater_can_read_but_not_write()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var operater = await factory.ClientAsAsync(Roles.Operater);

        Assert.Equal(HttpStatusCode.OK, (await operater.GetAsync("/api/equipment", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await operater.PostAsJsonAsync("/api/equipment", new { code = "M-09", name = "X", kind = "Masina", isActive = true }, Ct)).StatusCode);
    }
}
