using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Health;

/// <summary>Database and disk state only: no versions, paths, sizes or counts.</summary>
public sealed record HealthDto(string Status, string Database, string Disk);

/// <summary>
/// ROADMAP Faza 5 monitoring: anonymous, for the external uptime ping. Under /api so Caddy proxies it.
/// 200 when everything is fine, 503 otherwise.
/// </summary>
public static class HealthEndpoints
{
    public const string MinFreeDiskBytesKey = "Health:MinFreeDiskBytes";
    private const long DefaultMinFreeDiskBytes = 1L << 30; // 1 GiB

    public static void MapHealthEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/health", Check).AllowAnonymous();

    private static async Task<IResult> Check(AppDbContext db, IConfiguration config, ILogger<HealthDto> logger, CancellationToken ct)
    {
        var database = await DatabaseAsync(db, logger, ct);
        var disk = Disk(config, logger);
        var healthy = database == "ok" && disk == "ok";
        return Results.Json(new HealthDto(healthy ? "ok" : "greska", database, disk),
            statusCode: healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<string> DatabaseAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await db.Database.ExecuteSqlRawAsync("SELECT 1", timeout.Token);
            return "ok";
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(e, "Health check: database is not reachable");
            return "greska";
        }
    }

    /// <summary>Free space where uploaded files are stored (same disk as the database on the single VPS).</summary>
    private static string Disk(IConfiguration config, ILogger logger)
    {
        try
        {
            var root = config[$"{FileStorageOptions.Section}:Root"];
            if (string.IsNullOrWhiteSpace(root))
                return "greska";
            var min = config.GetValue<long?>(MinFreeDiskBytesKey) ?? DefaultMinFreeDiskBytes;
            // The storage folder is created on the first upload; measure the closest folder that exists.
            var dir = new DirectoryInfo(Path.GetFullPath(root));
            while (!dir.Exists && dir.Parent is not null)
                dir = dir.Parent;
            var free = new DriveInfo(dir.FullName).AvailableFreeSpace;
            if (free >= min)
                return "ok";
            logger.LogWarning("Health check: low disk space ({Free} bytes free)", free);
            return "malo-prostora";
        }
        catch (Exception e)
        {
            logger.LogError(e, "Health check: disk space could not be read");
            return "greska";
        }
    }
}
