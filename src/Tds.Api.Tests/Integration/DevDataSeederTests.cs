using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class DevDataSeederTests(PostgresFixture postgres)
{
    private sealed class FakeEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Tds.Api";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static DevDataSeeder Seeder(IServiceProvider sp, string env) => new(
        sp.GetRequiredService<AppDbContext>(), new FakeEnv(env), sp.GetRequiredService<IClock>(),
        sp.GetRequiredService<DocumentNumberService>(), NullLogger<DevDataSeeder>.Instance);

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public async Task Refuses_to_run_outside_development(string env)
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres, new Dictionary<string, string?> { ["Seed:DevData"] = "true" });
        factory.CreateClient();
        using var scope = factory.Services.CreateScope();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Seeder(scope.ServiceProvider, env).SeedAsync(Ct));
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().WorkOrders.AnyAsync(Ct));
    }

    [Fact]
    public async Task Seeds_consistent_example_data_once_in_development()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await Seeder(scope.ServiceProvider, "Development").SeedAsync(Ct);
        await Seeder(scope.ServiceProvider, "Development").SeedAsync(Ct); // second run does nothing

        Assert.Equal(5, await db.WorkOrders.CountAsync(Ct));
        Assert.Contains(await db.MaterialItems.Select(i => i.Grade).Distinct().ToListAsync(Ct), g => g == "AlMg3");

        // Stock only from movements: H-4471 = 24 received − 4 − 3 issued.
        var lot = await db.MaterialLots.SingleAsync(l => l.LotNumber == "H-4471", Ct);
        Assert.Equal(17m, await db.OnHandAsync(lot.Id, Ct));
        Assert.Equal(24m, lot.ReceivedQty);

        // DOMAIN §2.6 example: one lot with unknown stock, exactly once.
        var imported = await db.MaterialLots.SingleAsync(l => l.LotNumber == DevDataSeeder.NotionLotNumber, Ct);
        Assert.Null(imported.ReceivedQty);
        Assert.False(await db.StockMovements.AnyAsync(m => m.MaterialLotId == imported.Id, Ct));
    }
}
