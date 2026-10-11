using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Tests.Support;

namespace Tds.Api.Tests.Integration;

/// <summary>CLAUDE.md golden rule 4 + "Brisanje": no DELETE endpoints; QMS records are only voided.</summary>
public class NoHardDeleteEndpointTests(PostgresFixture postgres)
{
    [Fact]
    public async Task No_endpoint_accepts_http_delete_and_qms_records_have_void_endpoints()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e => (Route: e.RoutePattern.RawText ?? "", Methods: e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []))
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.Empty(endpoints.Where(e => e.Methods.Contains("DELETE")).Select(e => e.Route));
        foreach (var voidRoute in new[]
                 {
                     "/api/quality-checks/{id:guid}/void", "/api/non-conformities/{id:guid}/void", "/api/documents/{id:guid}/void",
                     "/api/equipment-calibrations/{id:guid}/void", "/api/lots/{id:guid}/void", "/api/production-entries/{id:guid}/void",
                 })
            Assert.Contains(endpoints, e => e.Route == voidRoute && e.Methods.Contains("POST"));
    }
}
