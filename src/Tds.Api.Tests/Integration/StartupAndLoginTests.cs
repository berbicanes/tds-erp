using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tds.Api.Data;
using Tds.Api.Features.Auth;
using Tds.Api.Tests.Support;

namespace Tds.Api.Tests.Integration;

/// <summary>ROADMAP Phase 0: a real Postgres, the real app startup (migrations + seed), then login.</summary>
public class StartupAndLoginTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Empty_database_is_migrated_on_startup_and_admin_can_log_in()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var client = factory.CreateClient();

        // All migrations applied, nothing pending.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync(ct));
            Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync(ct));

            // Phase 0 tables exist, all snake_case.
            await using var conn = new NpgsqlConnection(db.Database.GetConnectionString());
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(
                "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name", conn);
            var tables = new List<string>();
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct))
                    tables.Add(reader.GetString(0));

            Assert.Equal(
                [
                    "__EFMigrationsHistory", "asp_net_role_claims", "asp_net_roles", "asp_net_user_claims",
                    "asp_net_user_logins", "asp_net_user_roles", "asp_net_user_tokens", "asp_net_users",
                    "audit_log", "file_attachment", "number_sequence",
                ],
                tables);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me", ct)).StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { userName = TdsApiFactory.AdminUserName, password = TdsApiFactory.AdminPassword }, ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        Assert.Equal(TdsApiFactory.AdminUserName, me!.UserName);
        Assert.Equal(["Admin"], me.Roles);
    }
}
