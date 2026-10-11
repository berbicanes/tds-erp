using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Files;

public sealed record FileDto(Guid Id, string OriginalName, string ContentType, long SizeBytes);

public static class FileEndpoints
{
    public static void MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        // Same-origin cookie auth with SameSite=Strict (CLAUDE.md) instead of antiforgery tokens.
        app.MapPost("/api/files", Upload).DisableAntiforgery().RequireAuthorization(Policies.KontrolorOrAdmin)
            .WithMetadata(new RequestSizeLimitAttribute(FileAttachment.MaxSizeBytes + 1024 * 1024));
        app.MapGet("/api/files/{id:guid}", Download).RequireAuthorization();
    }

    private static async Task<IResult> Upload(IFormFile? file, AppDbContext db, IFileStorage storage, CancellationToken ct)
    {
        if (file is null)
            return ApiResults.Validation("file", "Odaberi fajl.");
        var name = Path.GetFileName(file.FileName);
        if (FileContentRules.CheckName(name) is { } nameError)
            return ApiResults.Validation("file", nameError);
        if (file.Length == 0)
            return ApiResults.Validation("file", "Fajl je prazan.");

        StoredFile stored;
        try
        {
            await using var input = file.OpenReadStream();
            stored = await storage.SaveAsync(input, FileAttachment.MaxSizeBytes, ct);
        }
        catch (FileTooLargeException)
        {
            return ApiResults.Validation("file", "Fajl je veći od 25 MB.");
        }

        string? contentError;
        await using (var check = storage.OpenRead(stored.StoragePath))
            contentError = FileContentRules.CheckContent(name, check);
        if (contentError is not null)
        {
            storage.DeleteUncommitted(stored.StoragePath);
            return ApiResults.Validation("file", contentError);
        }

        var attachment = new FileAttachment
        {
            OriginalName = name.Length > 255 ? name[^255..] : name,
            ContentType = FileContentRules.ContentTypes[Path.GetExtension(name)],
            SizeBytes = stored.SizeBytes,
            StoragePath = stored.StoragePath,
            Sha256 = stored.Sha256,
        };
        db.FileAttachments.Add(attachment);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            storage.DeleteUncommitted(stored.StoragePath);
            throw;
        }
        return TypedResults.Created($"/api/files/{attachment.Id}",
            new FileDto(attachment.Id, attachment.OriginalName, attachment.ContentType, attachment.SizeBytes));
    }

    /// <summary>PDF and images open inline (drawing preview, UI §6); everything else downloads.</summary>
    private static async Task<IResult> Download(Guid id, AppDbContext db, IFileStorage storage, CancellationToken ct)
    {
        var f = await db.FileAttachments.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (f is null)
            return ApiResults.NotFound("Fajl ne postoji.");
        var inline = f.ContentType is "application/pdf" or "image/png" or "image/jpeg";
        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        disposition.SetHttpFileName(f.OriginalName);
        return new FileResultWithDisposition(storage.OpenRead(f.StoragePath), f.ContentType, disposition.ToString());
    }

    private sealed class FileResultWithDisposition(Stream stream, string contentType, string disposition) : IResult
    {
        public async Task ExecuteAsync(HttpContext http)
        {
            http.Response.ContentType = contentType;
            http.Response.Headers.ContentDisposition = disposition;
            http.Response.Headers.XContentTypeOptions = "nosniff";
            // Never kept by the browser: on a shared phone the next user must not find it after logout (PREGLED N6).
            http.Response.Headers.CacheControl = "private, no-store";
            await using (stream)
                await stream.CopyToAsync(http.Response.Body, http.RequestAborted);
        }
    }
}
