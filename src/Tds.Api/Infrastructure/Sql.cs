using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Tds.Api.Data;

namespace Tds.Api.Infrastructure;

/// <summary>
/// Plain SQL for read models that are clearer as one statement (dashboard sections, stock aggregates).
/// Parameters are always bound, never concatenated. Writes always go through EF.
/// </summary>
public static class Sql
{
    public static async Task<List<T>> QueryAsync<T>(
        AppDbContext db, string sql, Func<DbDataReader, T> map, CancellationToken ct, params NpgsqlParameter[] parameters)
    {
        var conn = db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened)
            await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            cmd.Parameters.AddRange(parameters);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var rows = new List<T>();
            while (await reader.ReadAsync(ct))
                rows.Add(map(reader));
            return rows;
        }
        finally
        {
            if (opened)
                await conn.CloseAsync();
        }
    }

    public static T? Nullable<T>(this DbDataReader r, string column) where T : struct =>
        r.IsDBNull(r.GetOrdinal(column)) ? null : r.GetFieldValue<T>(r.GetOrdinal(column));

    public static T Get<T>(this DbDataReader r, string column) => r.GetFieldValue<T>(r.GetOrdinal(column));

    public static string? NullableString(this DbDataReader r, string column) =>
        r.IsDBNull(r.GetOrdinal(column)) ? null : r.GetString(r.GetOrdinal(column));
}
