using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Equipment;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

/// <summary>DATA_MODEL §9a, DOMAIN §7.5.</summary>
public class CalibrationHistoryTests(PostgresFixture postgres)
{
    private static async Task<EquipmentDto> Instrument(HttpClient c, string code = "MS-003") =>
        await (await c.PostAsJsonAsync("/api/equipment", new { code, name = "Mikrometar 0–25 mm", kind = "MjernoSredstvo", calibrationIntervalMonths = 12, isActive = true }, Ct))
            .ReadAsync<EquipmentDto>(HttpStatusCode.Created);

    [Fact]
    public async Task History_is_one_readable_list_and_latest_non_voided_drives_the_state()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var ms = await Instrument(k);

        await k.PostAsJsonAsync($"/api/equipment/{ms.Id}/calibrations", new { calibrationDate = "2024-10-01", performedBy = "Metrološki laboratorij Sarajevo", result = "Uskladjeno" }, Ct);
        await k.PostAsJsonAsync($"/api/equipment/{ms.Id}/calibrations", new { calibrationDate = "2025-10-01", performedBy = "Metrološki laboratorij Sarajevo", result = "Uskladjeno", note = "Certifikat 2025/77" }, Ct);
        var failed = await (await k.PostAsJsonAsync($"/api/equipment/{ms.Id}/calibrations",
            new { calibrationDate = "2026-10-01", nextCalibrationDate = "2027-10-01", performedBy = "Kalibra d.o.o.", result = "Neuskladjeno" }, Ct)).ReadAsync<CalibrationDto>(HttpStatusCode.Created);

        var history = await (await k.GetAsync($"/api/equipment/{ms.Id}/calibrations", Ct)).ReadAsync<List<CalibrationDto>>();
        Assert.Equal(["2026-10-01", "2025-10-01", "2024-10-01"], history.Select(h => h.CalibrationDate.ToString("yyyy-MM-dd")));
        Assert.Equal("kontrolor", history[0].CreatedByName);

        // Neuskladjeno → out of calibration although the next date is a year away.
        var now = await (await k.GetAsync($"/api/equipment/{ms.Id}", Ct)).ReadAsync<EquipmentDto>();
        Assert.Equal(CalibrationStatusAtCheck.VanKalibracije, now.CalibrationStatus);

        // Kontrolor cannot void; Admin voids the wrong entry and the previous one is the latest again.
        Assert.Equal(HttpStatusCode.Forbidden, (await k.PostAsJsonAsync($"/api/equipment-calibrations/{failed.Id}/void", new { reason = "Pogrešan instrument", version = failed.Version }, Ct)).StatusCode);
        await admin.PostAsJsonAsync($"/api/equipment-calibrations/{failed.Id}/void", new { reason = "Pogrešan instrument", version = failed.Version }, Ct);
        now = await (await k.GetAsync($"/api/equipment/{ms.Id}", Ct)).ReadAsync<EquipmentDto>();
        Assert.Equal((new DateOnly(2025, 10, 1), new DateOnly(2026, 10, 1)), (now.LastCalibrationDate!.Value, now.NextCalibrationDate!.Value));
        Assert.Equal(3, (await (await k.GetAsync($"/api/equipment/{ms.Id}/calibrations", Ct)).ReadAsync<List<CalibrationDto>>()).Count); // voided stays listed
    }

    [Fact]
    public async Task Calibration_rules_are_enforced()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var ms = await Instrument(admin);
        var machine = (await (await admin.GetAsync("/api/equipment?q=M-01", Ct)).ReadAsync<Tds.Api.Infrastructure.PagedResult<EquipmentDto>>()).Items.Single();

        var onMachine = await admin.PostAsJsonAsync($"/api/equipment/{machine.Id}/calibrations", new { calibrationDate = "2026-10-01", performedBy = "Lab", result = "Uskladjeno" }, Ct);
        Assert.Equal("M-01 nije mjerno sredstvo; kalibracija se bilježi samo za mjerna sredstva.", (await onMachine.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        var noBy = await admin.PostAsJsonAsync($"/api/equipment/{ms.Id}/calibrations", new { calibrationDate = "2026-10-01", performedBy = "", result = "Uskladjeno" }, Ct);
        Assert.Equal("Upiši ko je izvršio kalibraciju.", (await noBy.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("performedBy"));

        await admin.PostAsJsonAsync($"/api/equipment/{ms.Id}/calibrations", new { calibrationDate = "2026-10-01", performedBy = "Lab", result = "Uskladjeno" }, Ct);
        var current = await (await admin.GetAsync($"/api/equipment/{ms.Id}", Ct)).ReadAsync<EquipmentDto>();
        var kind = await admin.PutAsJsonAsync($"/api/equipment/{ms.Id}", new { code = ms.Code, name = ms.Name, kind = "Masina", isActive = true, version = current.Version }, Ct);
        Assert.Equal("Vrsta se ne može promijeniti jer oprema ima zabilježene kalibracije.", (await kind.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("kind"));
    }

    [Fact]
    public async Task Migrations_move_calibration_data_then_drop_the_legacy_columns()
    {
        var cs = await postgres.CreateDatabaseAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).UseSnakeCaseNamingConvention().Options;
        await using var db = new AppDbContext(options);
        var migrator = db.GetService<IMigrator>();
        var all = db.Database.GetMigrations().ToList();
        var before = all[all.FindIndex(m => m.EndsWith("_EquipmentCalibrations")) - 1];
        await migrator.MigrateAsync(before, Ct);

        // Old data model: dates on the equipment card.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO equipment (id, code, name, kind, calibration_interval_months, last_calibration_date, next_calibration_date, is_active, created_at, updated_at)
            VALUES ('01900000-0000-7000-8000-000000000001', 'MS-001', 'Pomično mjerilo', 'MjernoSredstvo', 12, '2025-11-01', '2026-11-01', true, now(), now()),
                   ('01900000-0000-7000-8000-000000000002', 'MS-002', 'Samo ručni rok', 'MjernoSredstvo', NULL, NULL, '2026-12-01', true, now(), now()),
                   ('01900000-0000-7000-8000-000000000003', 'M-09', 'Mašina', 'Masina', NULL, NULL, NULL, true, now(), now())
            """, Ct);

        await migrator.MigrateAsync(null, Ct);

        var moved = await db.EquipmentCalibrations.ToListAsync(Ct);
        var c = Assert.Single(moved);
        Assert.Equal((new DateOnly(2025, 11, 1), new DateOnly(2026, 11, 1), CalibrationResult.Uskladjeno), (c.CalibrationDate, c.NextCalibrationDate!.Value, c.Result));
        // A manual next date without a calibration date is not moved (owner's decision, Phase 3).
        Assert.DoesNotContain(moved, m => m.EquipmentId == Guid.Parse("01900000-0000-7000-8000-000000000002"));

        // The legacy columns are dropped by the follow-up migration (DROP approved by the owner for these three only).
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT count(*) FROM information_schema.columns
            WHERE table_name = 'equipment' AND column_name IN ('last_calibration_date', 'next_calibration_date', 'calibration_certificate_file_id')
            """, conn);
        Assert.Equal(0L, (long)(await cmd.ExecuteScalarAsync(Ct))!);
    }
}
