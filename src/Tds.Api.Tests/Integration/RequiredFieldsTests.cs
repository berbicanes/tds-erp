using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

/// <summary>
/// PREGLED N1 / CLAUDE.md "API": a required field that is left out of the request is a 400 with a message for that
/// field — never bound to 0, 0001-01-01, false or an empty Guid. One case per endpoint that takes a body.
/// </summary>
public class RequiredFieldsTests(PostgresFixture postgres)
{
    private const string Id = "0198c7a0-0000-7000-8000-000000000001";

    public static TheoryData<string, string, string> Endpoints => new()
    {
        { "POST", "/api/auth/login", "userName password" },
        { "POST", "/api/material-items", "name unit isActive" },
        { "PUT", $"/api/material-items/{Id}", "name unit isActive version" },
        { "POST", "/api/lots", "lotNumber materialItemId supplierId receivedDate receivedQty" },
        { "PUT", $"/api/lots/{Id}", "lotNumber supplierId version" },
        { "POST", $"/api/lots/{Id}/decision", "decision version" },
        { "POST", $"/api/lots/{Id}/certificate", "version" },
        { "POST", $"/api/lots/{Id}/void", "reason version" },
        { "POST", $"/api/lots/{Id}/movements", "type quantity date reason" },
        { "POST", "/api/suppliers", "name approvalStatus" },
        { "PUT", $"/api/suppliers/{Id}", "name approvalStatus version" },
        { "POST", $"/api/suppliers/{Id}/certificates", "fileId" },
        { "POST", "/api/quality-checks", "phase characteristic result inspectedByEmployeeId inspectionDate" },
        { "POST", $"/api/quality-checks/{Id}/evidence", "fileId" },
        { "POST", $"/api/quality-checks/{Id}/void", "reason version" },
        { "POST", "/api/non-conformities", "source description productDecision" },
        { "PUT", $"/api/non-conformities/{Id}", "source description productDecision version" },
        { "POST", $"/api/non-conformities/{Id}/status", "status version" },
        { "POST", $"/api/non-conformities/{Id}/evidence", "fileId" },
        { "POST", $"/api/non-conformities/{Id}/void", "reason version" },
        { "POST", "/api/production-entries", "workOrderId date operatorEmployeeId laserCutQty pressedQty goodQty scrapQty laborHours" },
        { "PUT", $"/api/production-entries/{Id}", "workOrderId date operatorEmployeeId laserCutQty pressedQty goodQty scrapQty laborHours version" },
        { "POST", $"/api/production-entries/{Id}/void", "reason version" },
        { "POST", "/api/notes", "text" },
        { "POST", $"/api/notes/{Id}/done", "isDone version" },
        { "POST", "/api/users", "userName role password" },
        { "PUT", $"/api/users/{Id}", "role isActive" },
        { "POST", $"/api/users/{Id}/password", "password" },
        { "POST", "/api/documents", "title code kind docVersion" },
        { "PUT", $"/api/documents/{Id}", "title code kind docVersion version" },
        { "POST", $"/api/documents/{Id}/status", "status version" },
        { "POST", $"/api/documents/{Id}/next-review", "version" },
        { "POST", $"/api/documents/{Id}/new-version", "docVersion" },
        { "POST", $"/api/documents/{Id}/void", "reason version" },
        { "POST", "/api/work-orders", "customer productDescription orderedQty dueDate priority" },
        { "PUT", $"/api/work-orders/{Id}", "customer productDescription orderedQty dueDate priority version" },
        { "POST", $"/api/work-orders/{Id}/quality-approval", "qualityApproval version" },
        { "POST", $"/api/work-orders/{Id}/status", "to version" },
        { "POST", $"/api/work-orders/{Id}/materials", "materialLotId reservedQty" },
        { "PUT", $"/api/work-orders/{Id}/materials/{Id}", "reservedQty version" },
        { "POST", "/api/employees", "fullName isActive" },
        { "PUT", $"/api/employees/{Id}", "fullName isActive version" },
        { "POST", "/api/equipment", "code name kind isActive" },
        { "PUT", $"/api/equipment/{Id}", "code name kind isActive version" },
        { "POST", $"/api/equipment/{Id}/calibrations", "calibrationDate performedBy result" },
        { "POST", $"/api/equipment-calibrations/{Id}/void", "reason version" },
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Omitted_required_fields_are_a_400_per_field(string method, string url, string requiredFields)
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();

        var response = await admin.SendAsync(new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(new { }) }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var errors = body.RootElement.GetProperty("errors").EnumerateObject().ToDictionary(p => p.Name, p => p.Value[0].GetString());
        foreach (var field in requiredFields.Split(' '))
        {
            Assert.True(errors.ContainsKey(field), $"{method} {url}: no error for \"{field}\" (got: {string.Join(", ", errors.Keys)})");
            Assert.False(string.IsNullOrWhiteSpace(errors[field]));
        }
    }

    [Fact]
    public async Task Lot_without_receipt_date_is_rejected_and_nothing_is_written()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var item = await TestData.Item(admin);
        var supplier = await TestData.Supplier(admin);

        var problem = await (await admin.PostAsJsonAsync("/api/lots",
            new { lotNumber = "H-1", materialItemId = item.Id, supplierId = supplier.Id, receivedQty = 10 }, Ct)).ProblemAsync(HttpStatusCode.BadRequest);

        Assert.Equal("Unesi datum prijema.", problem.FieldError("receivedDate"));
        Assert.Equal(0, factory.Service(sp => sp.GetRequiredService<AppDbContext>().MaterialLots.Count() + sp.GetRequiredService<AppDbContext>().StockMovements.Count()));
    }

    [Fact]
    public async Task Production_entry_without_labor_hours_is_rejected_not_stored_as_zero()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var problem = await (await admin.PostAsJsonAsync("/api/production-entries", new
        {
            workOrderId = Guid.CreateVersion7(), date = "2026-10-10", operatorEmployeeId = Guid.CreateVersion7(), equipmentIds = new[] { Guid.CreateVersion7() },
            laserCutQty = 0, pressedQty = 5, goodQty = 0, scrapQty = 0,
        }, Ct)).ProblemAsync(HttpStatusCode.BadRequest);

        Assert.Equal("Unesi radne sate.", problem.FieldError("laborHours"));
        Assert.Single(problem.GetProperty("errors").EnumerateObject());
    }

    [Fact]
    public async Task Stock_movement_without_date_is_rejected()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var problem = await (await admin.PostAsJsonAsync($"/api/lots/{Id}/movements",
            new { type = "Otpis", quantity = 1, reason = "oštećenje" }, Ct)).ProblemAsync(HttpStatusCode.BadRequest);

        Assert.Equal("Unesi datum pokreta.", problem.FieldError("date"));
        Assert.Single(problem.GetProperty("errors").EnumerateObject());
    }
}
