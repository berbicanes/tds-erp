using System.Net;
using System.Net.Http.Json;
using Tds.Api.Features.Employees;
using Tds.Api.Features.Materials;
using Tds.Api.Features.QualityChecks;
using Tds.Api.Features.Suppliers;
using Tds.Api.Features.WorkOrders;
using Tds.Api.Infrastructure;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Support;

/// <summary>Small builders for integration tests (through the real API).</summary>
public static class TestData
{
    public static async Task<EmployeeDto> Employee(HttpClient admin, string name = "Amra Hodžić") =>
        await (await admin.PostAsJsonAsync("/api/employees", new { fullName = name, isActive = true }, Ct)).ReadAsync<EmployeeDto>(HttpStatusCode.Created);

    public static async Task<SupplierDto> Supplier(HttpClient admin, string name = "Metalia d.o.o.", string status = "Odobren") =>
        await (await admin.PostAsJsonAsync("/api/suppliers", new { name, approvalStatus = status }, Ct)).ReadAsync<SupplierDto>(HttpStatusCode.Created);

    public static async Task<MaterialItemDto> Item(HttpClient admin, string name = "Lim crni", string grade = "S235", decimal thickness = 3, string unit = "tabla", decimal? minimumStock = null) =>
        await (await admin.PostAsJsonAsync("/api/material-items",
            new { name, grade, thicknessMm = thickness, unit, minimumStock, isActive = true }, Ct)).ReadAsync<MaterialItemDto>(HttpStatusCode.Created);

    public static async Task<LotDto> Lot(HttpClient admin, Guid itemId, Guid supplierId, decimal qty, string lotNumber = "H-4471", string receivedDate = "2026-10-01")
    {
        var r = await (await admin.PostAsJsonAsync("/api/lots",
            new { lotNumber, materialItemId = itemId, supplierId, receivedDate, receivedQty = qty }, Ct))
            .ReadAsync<WithWarnings<LotDto>>(HttpStatusCode.Created);
        return r.Data;
    }

    public static async Task<WorkOrderDto> WorkOrder(HttpClient client, string customer = "Metalbau AG", int qty = 100, string dueDate = "2026-11-15", string priority = "Normalan") =>
        await (await client.PostAsJsonAsync("/api/work-orders", new
        {
            customer, productDescription = "Nosač ugaoni 120×80, S235 3 mm", orderedQty = qty, dueDate, priority,
            drawingNumber = "TDS-0412", drawingRevision = "B",
        }, Ct)).ReadAsync<WorkOrderDto>(HttpStatusCode.Created);

    public static async Task<QualityCheckDto> Check(HttpClient client, Guid workOrderId, Guid inspectorId, string phase, string result = "Prihvaceno", string date = "2026-10-08") =>
        await (await client.PostAsJsonAsync("/api/quality-checks", new
        {
            workOrderId, phase, characteristic = "Mjera 120 ±0,5 mm", acceptanceCriterion = "119,5–120,5 mm", measuredResult = "120,1 mm",
            sampleSize = 1, result, inspectedByEmployeeId = inspectorId, inspectionDate = date,
        }, Ct)).ReadAsync<QualityCheckDto>(HttpStatusCode.Created);

    public static async Task<LotDto> AcceptedLot(HttpClient admin, Guid itemId, Guid supplierId, Guid inspectorId, decimal qty, string lotNumber = "H-4471")
    {
        var lot = await Lot(admin, itemId, supplierId, qty, lotNumber);
        var r = await (await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/decision",
            new { decision = "Prihvacen", inspectedByEmployeeId = inspectorId, version = lot.Version }, Ct)).ReadAsync<WithWarnings<LotDto>>();
        return r.Data;
    }
}
