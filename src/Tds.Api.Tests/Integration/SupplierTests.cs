using System.Net;
using System.Net.Http.Json;
using Tds.Api.Domain;
using Tds.Api.Features.Employees;
using Tds.Api.Features.Suppliers;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class SupplierTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Kontrolor_creates_filters_and_blocks_supplier_with_reason_in_notes()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var amra = await (await admin.PostAsJsonAsync("/api/employees", new { fullName = "Amra Hodžić", isActive = true }, Ct))
            .ReadAsync<EmployeeDto>(HttpStatusCode.Created);
        var kontrolor = await factory.ClientAsAsync(Roles.Kontrolor);

        var ss = await (await kontrolor.PostAsJsonAsync("/api/suppliers", new
        {
            name = "Salzgitter Mannesmann Stahlhandel", materialOrService = "Lim S235, 1.4301", approvalStatus = "Odobren",
            initialEvaluationDate = "2025-02-01", nextEvaluationDate = "2026-11-01", responsibleEmployeeId = amra.Id,
        }, Ct)).ReadAsync<SupplierDto>(HttpStatusCode.Created);
        await kontrolor.PostAsJsonAsync("/api/suppliers", new { name = "Messer BH Gas", materialOrService = "Tehnički plinovi", approvalStatus = "UProcjeni" }, Ct);

        Assert.Equal("Amra Hodžić", ss.ResponsibleEmployeeName);

        var odobreni = await (await kontrolor.GetAsync("/api/suppliers?approvalStatus=Odobren", Ct)).ReadAsync<PagedResult<SupplierDto>>();
        Assert.Equal("Salzgitter Mannesmann Stahlhandel", Assert.Single(odobreni.Items).Name);

        var plin = await (await kontrolor.GetAsync("/api/suppliers?q=plin", Ct)).ReadAsync<PagedResult<SupplierDto>>();
        Assert.Equal("Messer BH Gas", Assert.Single(plin.Items).Name);

        var blocked = await (await kontrolor.PutAsJsonAsync($"/api/suppliers/{ss.Id}", new
        {
            name = ss.Name, materialOrService = ss.MaterialOrService, approvalStatus = "Blokiran",
            notes = "Više ne sarađujemo — kasne isporuke 2026.", version = ss.Version,
        }, Ct)).ReadAsync<SupplierDto>();
        Assert.Equal(SupplierApprovalStatus.Blokiran, blocked.ApprovalStatus);
        Assert.Equal("Više ne sarađujemo — kasne isporuke 2026.", blocked.Notes);
    }

    [Fact]
    public async Task Duplicate_name_and_bad_dates_are_rejected()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        await admin.PostAsJsonAsync("/api/suppliers", new { name = "Metalia d.o.o.", approvalStatus = "Odobren" }, Ct);

        var dup = await admin.PostAsJsonAsync("/api/suppliers", new { name = " Metalia d.o.o. ", approvalStatus = "Odobren" }, Ct);
        Assert.Equal("Dobavljač Metalia d.o.o. već postoji.", (await dup.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("name"));

        var dates = await admin.PostAsJsonAsync("/api/suppliers", new
        {
            name = "Novi", approvalStatus = "UProcjeni", initialEvaluationDate = "2026-05-01", nextEvaluationDate = "2026-04-01",
        }, Ct);
        Assert.Equal("Sljedeća ocjena ne može biti prije početne ocjene.",
            (await dates.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("nextEvaluationDate"));
    }

    [Fact]
    public async Task Operater_can_read_but_not_write_and_there_is_no_delete()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var s = await (await admin.PostAsJsonAsync("/api/suppliers", new { name = "Metalia", approvalStatus = "Odobren" }, Ct))
            .ReadAsync<SupplierDto>(HttpStatusCode.Created);
        var operater = await factory.ClientAsAsync(Roles.Operater);

        Assert.Equal(HttpStatusCode.OK, (await operater.GetAsync("/api/suppliers", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operater.PostAsJsonAsync("/api/suppliers", new { name = "X", approvalStatus = "Odobren" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.DeleteAsync($"/api/suppliers/{s.Id}", Ct)).StatusCode);
    }
}
