using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;
using Tds.Api.Features.Files;
using Tds.Api.Features.QualityChecks;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

/// <summary>DATA_MODEL §4 supplier_certificate, DOMAIN §7.7.</summary>
public class SupplierCertificateTests(PostgresFixture postgres)
{
    private static async Task<Guid> Pdf(HttpClient c, string name)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.7\nISO\n%%EOF"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", name);
        return (await (await c.PostAsync("/api/files", form, Ct)).ReadAsync<FileDto>(HttpStatusCode.Created)).Id;
    }

    [Fact]
    public async Task Kontrolor_adds_admin_removes_with_audit_and_the_file_stays()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var supplier = await TestData.Supplier(admin);
        var iso = await Pdf(k, "ISO-9001.pdf");
        var en = await Pdf(k, "EN-1090.pdf");

        await k.PostAsJsonAsync($"/api/suppliers/{supplier.Id}/certificates", new { fileId = iso }, Ct);
        await k.PostAsJsonAsync($"/api/suppliers/{supplier.Id}/certificates", new { fileId = en }, Ct);
        await k.PostAsJsonAsync($"/api/suppliers/{supplier.Id}/certificates", new { fileId = en }, Ct);   // idempotent
        var list = await (await k.GetAsync($"/api/suppliers/{supplier.Id}/certificates", Ct)).ReadAsync<List<EvidenceFileDto>>();
        Assert.Equal(["EN-1090.pdf", "ISO-9001.pdf"], list.Select(f => f.OriginalName).Order());

        Assert.Equal(HttpStatusCode.Forbidden, (await k.PostAsync($"/api/suppliers/{supplier.Id}/certificates/{iso}/remove", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/suppliers/{supplier.Id}/certificates/{iso}/remove", null, Ct)).StatusCode);
        list = await (await k.GetAsync($"/api/suppliers/{supplier.Id}/certificates", Ct)).ReadAsync<List<EvidenceFileDto>>();
        Assert.Equal(["EN-1090.pdf"], list.Select(f => f.OriginalName));
        Assert.Equal(HttpStatusCode.OK, (await k.GetAsync($"/api/files/{iso}", Ct)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var logs = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.Where(a => a.EntityId == supplier.Id).OrderBy(a => a.At).ToListAsync(Ct);
        var removal = JsonDocument.Parse(logs[^1].ChangesJson).RootElement.GetProperty("CertificateFileIds");
        Assert.Equal(2, removal[0].GetArrayLength());
        Assert.Equal(en.ToString(), Assert.Single(removal[1].EnumerateArray()).GetString());
    }
}
