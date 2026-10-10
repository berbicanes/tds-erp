using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;

namespace Tds.Api.Tests.Support;

/// <summary>Runs the real API (all middleware, migrations, seed) against a fresh Testcontainers database.</summary>
public sealed class TdsApiFactory(string connectionString, IDictionary<string, string?>? settings = null)
    : WebApplicationFactory<Program>
{
    public const string AdminUserName = "admin";
    public const string AdminPassword = "test-lozinka-1";

    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "tds-test-storage-" + Guid.NewGuid().ToString("N"));

    public static async Task<TdsApiFactory> CreateAsync(PostgresFixture postgres, IDictionary<string, string?>? settings = null)
    {
        var factory = new TdsApiFactory(await postgres.CreateDatabaseAsync(), settings);
        // HTTPS so the Secure auth cookie is sent back by the test client.
        factory.ClientOptions.BaseAddress = new Uri("https://localhost");
        return factory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("FileStorage:Root", StorageRoot);

        var all = new Dictionary<string, string?>
        {
            [IdentitySeeder.AdminUserNameKey] = AdminUserName,
            [IdentitySeeder.AdminPasswordKey] = AdminPassword,
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
            all[key] = value;
        foreach (var (key, value) in all)
            builder.UseSetting(key, value);
    }

    public T Service<T>(Func<IServiceProvider, T> get)
    {
        using var scope = Services.CreateScope();
        return get(scope.ServiceProvider);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(StorageRoot))
            Directory.Delete(StorageRoot, recursive: true);
    }
}
