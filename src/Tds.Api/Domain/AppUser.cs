using Microsoft.AspNetCore.Identity;

namespace Tds.Api.Domain;

/// <summary>Login account. Signs in with UserName; Email is optional. Not audited (DATA_MODEL exception).</summary>
public class AppUser : IdentityUser<Guid>
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Kontrolor = "Kontrolor";
    public const string Operater = "Operater";

    public static readonly IReadOnlyList<string> All = [Admin, Kontrolor, Operater];
}
