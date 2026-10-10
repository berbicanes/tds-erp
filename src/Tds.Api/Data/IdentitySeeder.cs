using Microsoft.AspNetCore.Identity;
using Tds.Api.Domain;

namespace Tds.Api.Data;

/// <summary>
/// Ensures the three roles exist and creates the first Admin from TDS_ADMIN_USERNAME / TDS_ADMIN_PASSWORD.
/// Never overwrites an existing user. Credentials are never hard-coded.
/// </summary>
public sealed class IdentitySeeder(
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<AppUser> userManager,
    IConfiguration configuration,
    ILogger<IdentitySeeder> logger)
{
    public const string AdminUserNameKey = "TDS_ADMIN_USERNAME";
    public const string AdminPasswordKey = "TDS_ADMIN_PASSWORD";

    public async Task SeedAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                Check(await roleManager.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() }), $"role {role}");
        }

        var userName = configuration[AdminUserNameKey];
        var password = configuration[AdminPasswordKey];
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("{UserKey} / {PasswordKey} are not set; no Admin user is seeded", AdminUserNameKey, AdminPasswordKey);
            return;
        }

        if (await userManager.FindByNameAsync(userName) is not null)
            return;

        var admin = new AppUser { UserName = userName };
        Check(await userManager.CreateAsync(admin, password), "admin user");
        Check(await userManager.AddToRoleAsync(admin, Roles.Admin), "admin role");
        logger.LogInformation("Seeded Admin user {UserName}", userName);
    }

    private static void Check(IdentityResult result, string what)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Seeding {what} failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
    }
}
