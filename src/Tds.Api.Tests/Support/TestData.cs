using System.Net;
using System.Net.Http.Json;
using Tds.Api.Features.Employees;
using Tds.Api.Features.Materials;
using Tds.Api.Features.Suppliers;
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

    public static async Task<LotDto> AcceptedLot(HttpClient admin, Guid itemId, Guid supplierId, Guid inspectorId, decimal qty, string lotNumber = "H-4471")
    {
        var lot = await Lot(admin, itemId, supplierId, qty, lotNumber);
        var r = await (await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/decision",
            new { decision = "Prihvacen", inspectedByEmployeeId = inspectorId, version = lot.Version }, Ct)).ReadAsync<WithWarnings<LotDto>>();
        return r.Data;
    }
}
