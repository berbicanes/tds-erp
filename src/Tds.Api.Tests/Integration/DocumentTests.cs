using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Dashboard;
using Tds.Api.Features.Documents;
using Tds.Api.Features.Files;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

/// <summary>DOMAIN §7.4 through the API and the database.</summary>
public class DocumentTests(PostgresFixture postgres)
{
    private static readonly string Today = new SystemClock(TimeProvider.System).Today.ToString("yyyy-MM-dd");

    private static async Task<Guid> Pdf(HttpClient c)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.7\nPR-08\n%%EOF"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "PR-08.pdf");
        return (await (await c.PostAsync("/api/files", form, Ct)).ReadAsync<FileDto>(HttpStatusCode.Created)).Id;
    }

    private static async Task<DocumentDto> Approved(HttpClient c, Guid fileId, string version = "01")
    {
        var d = await (await c.PostAsJsonAsync("/api/documents", new
        {
            title = "Upravljanje neusaglašenostima", code = "PR-08", kind = "Procedura", docVersion = version, fileId, version = 0,
        }, Ct)).ReadAsync<DocumentDto>(HttpStatusCode.Created);
        d = await (await c.PostAsJsonAsync($"/api/documents/{d.Id}/status", new { status = "NaPregledu", version = d.Version }, Ct)).ReadAsync<DocumentDto>();
        return await (await c.PostAsJsonAsync($"/api/documents/{d.Id}/status", new { status = "Odobren", approvalDate = Today, version = d.Version }, Ct)).ReadAsync<DocumentDto>();
    }

    [Fact]
    public async Task Approving_a_new_version_withdraws_the_previous_one_in_the_same_transaction()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var file = await Pdf(k);
        var v1 = await Approved(k, file);
        Assert.Equal(DocumentStatus.Odobren, v1.Status);

        var v2 = await (await k.PostAsJsonAsync($"/api/documents/{v1.Id}/new-version", new { docVersion = "02" }, Ct)).ReadAsync<DocumentDto>(HttpStatusCode.Created);
        Assert.Equal((DocumentStatus.Nacrt, "PR-08", file), (v2.Status, v2.Code, v2.FileId!.Value));
        v2 = await (await k.PostAsJsonAsync($"/api/documents/{v2.Id}/status", new { status = "NaPregledu", version = v2.Version }, Ct)).ReadAsync<DocumentDto>();
        v2 = await (await k.PostAsJsonAsync($"/api/documents/{v2.Id}/status", new { status = "Odobren", approvalDate = Today, version = v2.Version }, Ct)).ReadAsync<DocumentDto>();

        var detail = await (await k.GetAsync($"/api/documents/{v2.Id}", Ct)).ReadAsync<DocumentDetailDto>();
        Assert.Equal([("02", DocumentStatus.Odobren), ("01", DocumentStatus.Povucen)], detail.Versions.Select(v => (v.DocVersion, v.Status)));
        // Same transaction: both rows carry the same xmin (transaction id) and the withdrawal is audited with its reason.
        Assert.Equal(detail.Versions[0].Version, detail.Versions[1].Version);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await db.AuditLogs.Where(a => a.EntityId == v1.Id).OrderByDescending(a => a.At).FirstAsync(Ct);
        var changes = System.Text.Json.JsonDocument.Parse(log.ChangesJson).RootElement;
        Assert.Equal("Odobrena verzija 02; prethodna povučena automatski.", changes.GetProperty("reason")[1].GetString());
        Assert.Equal(["Odobren", "Povucen"], changes.GetProperty("Status").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Database_refuses_a_second_approved_version_of_the_same_code()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var v1 = await Approved(k, await Pdf(k));
        var v2 = await (await k.PostAsJsonAsync($"/api/documents/{v1.Id}/new-version", new { docVersion = "02" }, Ct)).ReadAsync<DocumentDto>(HttpStatusCode.Created);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE controlled_document SET status = 'Odobren' WHERE id = {v2.Id}", Ct));
        Assert.Equal(("23505", "ux_controlled_document_one_approved_per_code"), (ex.SqlState, ex.ConstraintName));
    }

    [Fact]
    public async Task Approved_is_read_only_except_next_review_and_withdrawn_is_final()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var file = await Pdf(k);
        var d = await Approved(k, file);

        var edit = await k.PutAsJsonAsync($"/api/documents/{d.Id}", new { title = "Novi naziv", code = d.Code, kind = "Procedura", docVersion = "01", fileId = file, version = d.Version }, Ct);
        Assert.StartsWith("Odobren dokument je zaključan", (await edit.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        d = await (await k.PostAsJsonAsync($"/api/documents/{d.Id}/next-review", new { nextReviewDate = "2020-01-01", version = d.Version }, Ct)).ReadAsync<DocumentDto>();
        var w = Assert.Single((await (await k.GetAsync("/api/dashboard", Ct)).ReadAsync<DashboardDto>()).Warnings, x => x.Kind == "DocumentReview");
        Assert.Equal(("red", "PR-08", "01"), (w.Severity, w.Title, w.ItemName));

        d = await (await k.PostAsJsonAsync($"/api/documents/{d.Id}/status", new { status = "Povucen", version = d.Version }, Ct)).ReadAsync<DocumentDto>();
        Assert.Equal("Povučen dokument je konačan. Za izmjenu napravi novu verziju.",
            (await (await k.PostAsJsonAsync($"/api/documents/{d.Id}/status", new { status = "Odobren", version = d.Version }, Ct)).ProblemAsync(HttpStatusCode.Conflict)).Detail());
        Assert.Equal(HttpStatusCode.Conflict, (await k.PostAsJsonAsync($"/api/documents/{d.Id}/next-review", new { nextReviewDate = "2027-01-01", version = d.Version }, Ct)).StatusCode);
        Assert.DoesNotContain((await (await k.GetAsync("/api/dashboard", Ct)).ReadAsync<DashboardDto>()).Warnings, x => x.Kind == "DocumentReview");

        // Only Admin voids; operater only reads.
        var op = await factory.ClientAsAsync(Roles.Operater);
        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync("/api/documents", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync("/api/documents", new { title = "X", code = "X", kind = "Procedura", docVersion = "1", version = 0 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await k.PostAsJsonAsync($"/api/documents/{d.Id}/void", new { reason = "x", version = d.Version }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/documents/{d.Id}/void", new { reason = "Dupli unos", version = d.Version }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Future_approval_date_is_rejected_also_on_a_draft()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var tomorrow = new SystemClock(TimeProvider.System).Today.AddDays(1).ToString("yyyy-MM-dd");

        var create = await k.PostAsJsonAsync("/api/documents",
            new { title = "Radno uputstvo laser", code = "RU-02", kind = "RadnoUputstvo", docVersion = "01", approvalDate = tomorrow }, Ct);
        Assert.Equal("Datum odobrenja ne može biti u budućnosti.", (await create.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("approvalDate"));

        var d = await (await k.PostAsJsonAsync("/api/documents",
            new { title = "Radno uputstvo laser", code = "RU-02", kind = "RadnoUputstvo", docVersion = "01", approvalDate = Today }, Ct)).ReadAsync<DocumentDto>(HttpStatusCode.Created);
        var update = await k.PutAsJsonAsync($"/api/documents/{d.Id}",
            new { title = "Radno uputstvo laser", code = "RU-02", kind = "RadnoUputstvo", docVersion = "01", approvalDate = tomorrow, version = d.Version }, Ct);
        Assert.Equal("Datum odobrenja ne može biti u budućnosti.", (await update.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("approvalDate"));
        Assert.Equal(Today, (await (await k.GetAsync($"/api/documents/{d.Id}", Ct)).ReadAsync<DocumentDetailDto>()).Document.ApprovalDate!.Value.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task Approval_conditions_and_duplicate_versions_are_rejected()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var d = await (await k.PostAsJsonAsync("/api/documents", new { title = "Obrazac prijema", code = "OB-03", kind = "ObrazacZapis", docVersion = "01", version = 0 }, Ct))
            .ReadAsync<DocumentDto>(HttpStatusCode.Created);
        var dup = await k.PostAsJsonAsync("/api/documents", new { title = "Obrazac prijema", code = "OB-03", kind = "ObrazacZapis", docVersion = "01", version = 0 }, Ct);
        Assert.Equal("Verzija 01 dokumenta OB-03 već postoji.", (await dup.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("docVersion"));

        d = await (await k.PostAsJsonAsync($"/api/documents/{d.Id}/status", new { status = "NaPregledu", version = d.Version }, Ct)).ReadAsync<DocumentDto>();
        var future = await k.PostAsJsonAsync($"/api/documents/{d.Id}/status", new { status = "Odobren", approvalDate = "2099-01-01", version = d.Version }, Ct);
        Assert.Equal("Datum odobrenja ne može biti u budućnosti.", (await future.ProblemAsync(HttpStatusCode.Conflict)).Detail());
        var noFile = await k.PostAsJsonAsync($"/api/documents/{d.Id}/status", new { status = "Odobren", approvalDate = Today, version = d.Version }, Ct);
        Assert.Equal("Za odobravanje priloži fajl dokumenta.", (await noFile.ProblemAsync(HttpStatusCode.Conflict)).Detail());
        Assert.Equal(DocumentStatus.NaPregledu, (await (await k.GetAsync($"/api/documents/{d.Id}", Ct)).ReadAsync<DocumentDetailDto>()).Document.Status);
    }
}
