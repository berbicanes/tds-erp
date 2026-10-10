using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Tds.Api.Domain;
using Tds.Api.Features.Files;
using Tds.Api.Features.Materials;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class FileTests(PostgresFixture postgres)
{
    private static MultipartFormDataContent Form(string name, byte[] content)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream"); // browser-sent type is ignored
        form.Add(file, "file", name);
        return form;
    }

    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nCertifikat 3.1 EN 10204\n%%EOF");

    [Fact]
    public async Task Upload_stores_under_guid_and_download_returns_original_name_and_server_content_type()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);

        var file = await (await k.PostAsync("/api/files", Form("Certifikat H-4471 (Salzgitter).pdf", Pdf), Ct)).ReadAsync<FileDto>(HttpStatusCode.Created);
        Assert.Equal("Certifikat H-4471 (Salzgitter).pdf", file.OriginalName);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(Pdf.Length, file.SizeBytes);

        var onDisk = Directory.GetFiles(factory.StorageRoot, "*", SearchOption.AllDirectories);
        Assert.Matches("^[0-9a-f]{32}$", Path.GetFileName(Assert.Single(onDisk)));

        var op = await factory.ClientAsAsync(Roles.Operater);
        var download = await op.GetAsync($"/api/files/{file.Id}", Ct);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("Certifikat H-4471 (Salzgitter).pdf", download.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(Pdf, await download.Content.ReadAsByteArrayAsync(Ct));
    }

    [Fact]
    public async Task Wrong_extension_renamed_content_empty_and_oversized_files_are_rejected_and_not_stored()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();

        var exe = await admin.PostAsync("/api/files", Form("alat.exe", [0x4D, 0x5A]), Ct);
        Assert.StartsWith("Vrsta fajla nije dozvoljena.", (await exe.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("file"));

        var renamed = await admin.PostAsync("/api/files", Form("crtez.pdf", [0x4D, 0x5A, 0x90, 0x00]), Ct);
        Assert.Equal("Sadržaj fajla ne odgovara vrsti .pdf. Fajl je oštećen ili je pogrešno imenovan.",
            (await renamed.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("file"));

        var empty = await admin.PostAsync("/api/files", Form("prazan.pdf", []), Ct);
        Assert.Equal("Fajl je prazan.", (await empty.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("file"));

        var big = new byte[FileAttachment.MaxSizeBytes + 1];
        Pdf.CopyTo(big, 0);
        var tooBig = await admin.PostAsync("/api/files", Form("veliki.pdf", big), Ct);
        Assert.Equal("Fajl je veći od 25 MB.", (await tooBig.ProblemAsync(HttpStatusCode.BadRequest)).FieldError("file"));

        Assert.Empty(Directory.Exists(factory.StorageRoot) ? Directory.GetFiles(factory.StorageRoot, "*", SearchOption.AllDirectories) : []);
    }

    [Fact]
    public async Task Operater_cannot_upload()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var op = await factory.ClientAsAsync(Roles.Operater);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsync("/api/files", Form("a.pdf", Pdf), Ct)).StatusCode);
    }

    [Fact]
    public async Task Lot_certificate_removes_the_missing_certificate_warning()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var lot = await TestData.AcceptedLot(admin, (await TestData.Item(admin)).Id, (await TestData.Supplier(admin)).Id, (await TestData.Employee(admin)).Id, 10);
        var file = await (await admin.PostAsync("/api/files", Form("cert.pdf", Pdf), Ct)).ReadAsync<FileDto>(HttpStatusCode.Created);

        var r = await (await admin.PostAsJsonAsync($"/api/lots/{lot.Id}/certificate", new { fileId = file.Id, version = lot.Version }, Ct))
            .ReadAsync<WithWarnings<LotDto>>();

        Assert.Empty(r.Warnings);
        Assert.Equal("cert.pdf", r.Data.CertificateFileName);
    }
}
