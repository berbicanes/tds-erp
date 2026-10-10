using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Tds.Api.Tests.Support.PostgresFixture))]

namespace Tds.Api.Tests.Support;

/// <summary>One real Postgres 17 container for the whole test run; each test gets its own database.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Every test gets its own database (and its own connection pool), so allow plenty of connections.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
        .WithCommand("-c", "max_connections=500")
        .Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Creates an empty database and returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = "test_" + Guid.NewGuid().ToString("N");
        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE {name}", conn);
        await cmd.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = name,
            MaxPoolSize = 10,
            ConnectionIdleLifetime = 10,
            ConnectionPruningInterval = 5,
        }.ConnectionString;
    }
}
