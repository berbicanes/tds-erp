using System.Net;
using Tds.Api.Features.Health;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class HealthTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Health_is_anonymous_and_reports_only_database_and_disk_state()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var response = await factory.CreateClient().GetAsync("/api/health", Ct);

        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"status":"ok","database":"ok","disk":"ok"}""", body);
    }

    [Fact]
    public async Task Low_disk_space_is_unhealthy()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres,
            new Dictionary<string, string?> { [HealthEndpoints.MinFreeDiskBytesKey] = long.MaxValue.ToString() });
        var response = await factory.CreateClient().GetAsync("/api/health", Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("""{"status":"greska","database":"ok","disk":"malo-prostora"}""", await response.Content.ReadAsStringAsync(Ct));
    }
}
