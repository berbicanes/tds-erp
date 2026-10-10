using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Tests.Support;

namespace Tds.Api.Tests.Integration;

public class IdentitySeederTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Startup_creates_three_roles_and_admin_from_settings()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        Assert.Equal(["Admin", "Kontrolor", "Operater"], (await roles.Roles.Select(r => r.Name!).ToListAsync(TestContext.Current.CancellationToken)).Order());
        var admin = await users.FindByNameAsync(TdsApiFactory.AdminUserName);
        Assert.NotNull(admin);
        Assert.Null(admin.Email);
        Assert.True(await users.IsInRoleAsync(admin, Roles.Admin));
        Assert.True(await users.CheckPasswordAsync(admin, TdsApiFactory.AdminPassword));
    }

    [Fact]
    public async Task Without_settings_no_admin_is_created_and_startup_succeeds()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres, new Dictionary<string, string?>
        {
            [IdentitySeeder.AdminUserNameKey] = "",
            [IdentitySeeder.AdminPasswordKey] = "",
        });
        factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.Users.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(3, await db.Roles.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Seeding_twice_does_not_duplicate_or_reset_password()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var admin = (await users.FindByNameAsync(TdsApiFactory.AdminUserName))!;
        await users.ChangePasswordAsync(admin, TdsApiFactory.AdminPassword, "nova-lozinka-2");

        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(3, await db.Roles.CountAsync(TestContext.Current.CancellationToken));
        Assert.True(await users.CheckPasswordAsync((await users.FindByNameAsync(TdsApiFactory.AdminUserName))!, "nova-lozinka-2"));
    }

    [Theory]
    [InlineData("kratka-12", false)]   // 9 chars
    [InlineData("dugalozinka1", true)] // no uppercase, no special char
    public async Task Password_policy_min_10_chars_without_special_or_uppercase(string password, bool valid)
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var result = await users.CreateAsync(new AppUser { UserName = "operater1" }, password);

        Assert.Equal(valid, result.Succeeded);
    }

    [Fact]
    public async Task Five_failed_attempts_lock_the_account_for_15_minutes()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var admin = (await users.FindByNameAsync(TdsApiFactory.AdminUserName))!;

        for (var i = 0; i < 4; i++)
            Assert.False((await signIn.CheckPasswordSignInAsync(admin, "pogresna-lozinka", lockoutOnFailure: true)).IsLockedOut);
        var fifth = await signIn.CheckPasswordSignInAsync(admin, "pogresna-lozinka", lockoutOnFailure: true);

        Assert.True(fifth.IsLockedOut);
        var lockoutEnd = (await users.FindByIdAsync(admin.Id.ToString()))!.LockoutEnd!.Value;
        Assert.InRange(lockoutEnd - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
        Assert.True((await signIn.CheckPasswordSignInAsync(admin, TdsApiFactory.AdminPassword, lockoutOnFailure: true)).IsLockedOut);
    }
}
