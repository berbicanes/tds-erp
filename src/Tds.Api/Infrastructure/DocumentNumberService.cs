using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;

namespace Tds.Api.Infrastructure;

/// <summary>
/// Issues document numbers (DOMAIN §6). Must run inside the transaction that saves the document:
/// the counter row is locked with <c>SELECT … FOR UPDATE</c> until that transaction ends.
/// No duplicates is a requirement; no gaps is not.
/// </summary>
public sealed class DocumentNumberService(AppDbContext db)
{
    public async Task<string> NextAsync(DocumentNumberKind kind, DateOnly documentDate, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Document numbers must be issued inside a transaction.");

        var prefix = DocumentNumberFormat.Prefix(kind);
        var period = DocumentNumberFormat.Period(kind, documentDate);

        // Make sure the row exists so it can be locked (first number of a period).
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO number_sequence (kind, period, last_value) VALUES ({prefix}, {period}, 0) ON CONFLICT DO NOTHING", ct);

        var current = await db.Database
            .SqlQuery<int>($"SELECT last_value AS \"Value\" FROM number_sequence WHERE kind = {prefix} AND period = {period} FOR UPDATE")
            .SingleAsync(ct);

        var next = current + 1;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE number_sequence SET last_value = {next} WHERE kind = {prefix} AND period = {period}", ct);

        return DocumentNumberFormat.Format(kind, documentDate, next);
    }
}
