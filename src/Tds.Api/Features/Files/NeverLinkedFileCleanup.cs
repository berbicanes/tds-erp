using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Files;

/// <summary>
/// DOMAIN §7.8 — the only exception to "nothing is deleted": a file uploaded more than 24 hours ago that was never
/// linked to any record (no current link, and no other record's AuditLog mentions it). Content and row are deleted
/// and every deletion is logged. A file whose link was removed (§7.7) stays forever.
/// </summary>
public sealed class NeverLinkedFileCleanup(AppDbContext db, IFileStorage storage, IClock clock, ILogger<NeverLinkedFileCleanup> logger)
{
    public static readonly TimeSpan MinAge = TimeSpan.FromHours(24);

    private sealed record Candidate(Guid Id, string OriginalName, long SizeBytes, string StoragePath, DateTimeOffset CreatedAt);

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var candidates = await Sql.QueryAsync(db, CandidateSql(db),
            r => new Candidate(r.Get<Guid>("id"), r.Get<string>("original_name"), r.Get<long>("size_bytes"),
                r.Get<string>("storage_path"), r.Get<DateTimeOffset>("created_at")),
            ct, new NpgsqlParameter("cutoff", clock.UtcNow - MinAge));

        var deleted = 0;
        foreach (var c in candidates)
        {
            // The row goes first (outside the audit interceptor: nothing else is ever hard-deleted); the FK
            // constraints (RESTRICT) still refuse it if a link appeared meanwhile.
            int rows;
            try
            {
                rows = await db.FileAttachments.Where(f => f.Id == c.Id).ExecuteDeleteAsync(ct);
            }
            catch (Exception e) when (e.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
            {
                continue;
            }
            if (rows == 0)
                continue;
            try
            {
                storage.DeleteNeverLinked(c.StoragePath);
            }
            catch (IOException e)
            {
                logger.LogWarning(e, "Never-linked file {FileId}: row deleted, content {Path} could not be deleted", c.Id, c.StoragePath);
            }
            deleted++;
            logger.LogInformation("Deleted never-linked file {FileId} \"{Name}\" ({Size} bytes, uploaded {CreatedAt:u})",
                c.Id, c.OriginalName, c.SizeBytes, c.CreatedAt);
        }
        return deleted;
    }

    /// <summary>Links come from the EF model (every foreign key to file_attachment), so a new link type is covered too.</summary>
    private static string CandidateSql(AppDbContext db)
    {
        var file = db.Model.FindEntityType(typeof(FileAttachment))!;
        var links = file.GetReferencingForeignKeys().Select(fk =>
        {
            var table = fk.DeclaringEntityType.GetTableName()!;
            var column = fk.Properties.Single().GetColumnName();
            return $"AND NOT EXISTS (SELECT 1 FROM \"{table}\" x WHERE x.\"{column}\" = f.id)";
        });
        return $"""
            SELECT f.id, f.original_name, f.size_bytes, f.storage_path, f.created_at
            FROM file_attachment f
            WHERE f.created_at < @cutoff
              {string.Join("\n  ", links)}
              AND NOT EXISTS (SELECT 1 FROM audit_log a WHERE a.entity_id <> f.id AND strpos(a.changes_json::text, f.id::text) > 0)
            """;
    }
}

/// <summary>Runs <see cref="NeverLinkedFileCleanup"/> once a day (first run 10 minutes after start).</summary>
public sealed class NeverLinkedFileCleanupJob(IServiceScopeFactory scopes, ILogger<NeverLinkedFileCleanupJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var deleted = await scope.ServiceProvider.GetRequiredService<NeverLinkedFileCleanup>().RunAsync(stoppingToken);
                    logger.LogInformation("Never-linked file cleanup finished: {Deleted} deleted", deleted);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogError(e, "Never-linked file cleanup failed");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }
}
