using System.Net;
using System.Net.Http.Json;
using Tds.Api.Domain;
using Tds.Api.Features.Materials;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class MaterialItemTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Kontrolor_creates_items_and_list_sorts_by_thickness()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);

        foreach (var t in new[] { 10m, 3m, 2.5m })
            await (await k.PostAsJsonAsync("/api/material-items",
                new { name = "Lim crni", grade = "S235", thicknessMm = t, unit = "tabla", minimumStock = 5, isActive = true }, Ct))
                .ReadAsync<MaterialItemDto>(HttpStatusCode.Created);
        await k.PostAsJsonAsync("/api/material-items", new { name = "Lim inox", grade = "1.4301", thicknessMm = 2, unit = "kg", isActive = true }, Ct);

        var crni = await (await k.GetAsync("/api/material-items?q=S235&sort=thicknessMm", Ct)).ReadAsync<PagedResult<MaterialItemDto>>();
        Assert.Equal([2.5m, 3m, 10m], crni.Items.Select(x => x.ThicknessMm!.Value));
        Assert.All(crni.Items, x => Assert.Equal(Tds.Api.Domain.Unit.tabla, x.Unit));

        var inox = await (await k.GetAsync("/api/material-items?q=1.4301", Ct)).ReadAsync<PagedResult<MaterialItemDto>>();
        Assert.Null(Assert.Single(inox.Items).MinimumStock);
    }

    [Fact]
    public async Task Same_name_grade_thickness_is_rejected_including_empty_grade()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        await admin.PostAsJsonAsync("/api/material-items", new { name = "Lim crni", grade = "S235", thicknessMm = 3, unit = "tabla", isActive = true }, Ct);
        await admin.PostAsJsonAsync("/api/material-items", new { name = "Cijev", unit = "m", isActive = true }, Ct);

        var dup = await admin.PostAsJsonAsync("/api/material-items", new { name = "Lim crni", grade = " S235 ", thicknessMm = 3.00, unit = "kg", isActive = true }, Ct);
        Assert.Equal("Artikal sa ovim nazivom, kvalitetom i debljinom već postoji.", (await dup.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("name"));

        var dupNulls = await admin.PostAsJsonAsync("/api/material-items", new { name = "Cijev", unit = "m", isActive = true }, Ct);
        await dupNulls.ProblemAsync(HttpStatusCode.BadRequest);

        // Different thickness is a different item.
        var other = await admin.PostAsJsonAsync("/api/material-items", new { name = "Lim crni", grade = "S235", thicknessMm = 4, unit = "tabla", isActive = true }, Ct);
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
    }

    [Theory]
    [InlineData("{\"name\":\"Lim\",\"unit\":\"tabla\",\"thicknessMm\":0,\"isActive\":true}", "thicknessMm", "Debljina mora biti veća od 0.")]
    [InlineData("{\"name\":\"Lim\",\"unit\":\"tabla\",\"thicknessMm\":2.555,\"isActive\":true}", "thicknessMm", "Debljina može imati najviše 2 decimale.")]
    [InlineData("{\"name\":\"Lim\",\"unit\":\"tabla\",\"minimumStock\":-1,\"isActive\":true}", "minimumStock", "Minimalna zaliha ne može biti negativna.")]
    [InlineData("{\"name\":\" \",\"unit\":\"tabla\",\"isActive\":true}", "name", "Unesi naziv artikla, npr. Lim crni.")]
    public async Task Invalid_values_are_rejected_per_field(string json, string field, string message)
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();

        var r = await admin.PostAsync("/api/material-items", new StringContent(json, System.Text.Encoding.UTF8, "application/json"), Ct);

        Assert.Equal(message, (await r.ProblemAsync(HttpStatusCode.BadRequest)).FieldError(field));
    }

    [Fact]
    public async Task Operater_cannot_write()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var op = await factory.ClientAsAsync(Roles.Operater);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await op.PostAsJsonAsync("/api/material-items", new { name = "Lim", unit = "kg", isActive = true }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Unit_cannot_change_once_the_item_has_a_lot()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var item = await TestData.Item(admin, unit: "tabla");
        var free = await TestData.Item(admin, "Lim inox", "1.4301", 2, "kg");

        // Without lots the unit can still be corrected.
        var ok = await (await admin.PutAsJsonAsync($"/api/material-items/{free.Id}",
            new { name = free.Name, grade = free.Grade, thicknessMm = free.ThicknessMm, unit = "tabla", isActive = true, version = free.Version }, Ct)).ReadAsync<MaterialItemDto>();
        Assert.Equal(Tds.Api.Domain.Unit.tabla, ok.Unit);

        var lot = await TestData.Lot(admin, item.Id, (await TestData.Supplier(admin)).Id, 5);
        await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/void", new { reason = "Greška", version = lot.Version }, Ct); // even a voided lot counts

        var change = await admin.PutAsJsonAsync($"/api/material-items/{item.Id}",
            new { name = item.Name, grade = item.Grade, thicknessMm = item.ThicknessMm, unit = "kg", isActive = true, version = item.Version }, Ct);
        Assert.Equal("Jedinica se ne može promijeniti jer artikal već ima lotove; njihove količine su u ovoj jedinici.",
            (await change.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("unit"));

        // Other fields of the same item still change.
        var rename = await admin.PutAsJsonAsync($"/api/material-items/{item.Id}",
            new { name = "Lim crni topljeni", grade = item.Grade, thicknessMm = item.ThicknessMm, unit = "tabla", isActive = true, version = item.Version }, Ct);
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
    }
}
