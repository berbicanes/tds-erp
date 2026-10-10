using Tds.Api.Domain;

namespace Tds.Api.Infrastructure;

/// <summary>Authorization policies from DOMAIN §8.</summary>
public static class Policies
{
    public const string Admin = "Admin";
    public const string KontrolorOrAdmin = "KontrolorOrAdmin";

    public static void AddTdsPolicies(this Microsoft.AspNetCore.Authorization.AuthorizationBuilder auth) => auth
        .AddPolicy(Admin, p => p.RequireRole(Roles.Admin))
        .AddPolicy(KontrolorOrAdmin, p => p.RequireRole(Roles.Admin, Roles.Kontrolor));
}
