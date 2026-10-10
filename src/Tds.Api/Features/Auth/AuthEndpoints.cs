using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Auth;

public sealed record LoginRequest(string UserName, string Password);

public sealed record MeResponse(Guid Id, string UserName, IReadOnlyList<string> Roles);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().WithMessage("Unesi korisničko ime.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Unesi lozinku.");
    }
}

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth");
        auth.MapPost("/login", Login).Validate<LoginRequest>().AllowAnonymous();
        auth.MapPost("/logout", Logout).AllowAnonymous();

        app.MapGet("/api/me", Me).RequireAuthorization();
    }

    private static async Task<IResult> Login(
        LoginRequest request, SignInManager<AppUser> signInManager, UserManager<AppUser> userManager)
    {
        var result = await signInManager.PasswordSignInAsync(
            request.UserName.Trim(), request.Password, isPersistent: true, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return Problems.Unauthorized(
                "Nalog je zaključan zbog 5 neuspjelih pokušaja prijave. Pokušaj ponovo za 15 minuta ili se javi administratoru.");
        if (!result.Succeeded)
            return Problems.Unauthorized("Pogrešno korisničko ime ili lozinka.");

        var user = (await userManager.FindByNameAsync(request.UserName.Trim()))!;
        return TypedResults.Ok(await ToMe(user, userManager));
    }

    private static async Task<IResult> Logout(SignInManager<AppUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<IResult> Me(ClaimsPrincipal principal, UserManager<AppUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
            return Problems.Unauthorized("Nisi prijavljen.");
        return TypedResults.Ok(await ToMe(user, userManager));
    }

    private static async Task<MeResponse> ToMe(AppUser user, UserManager<AppUser> userManager) =>
        new(user.Id, user.UserName!, [.. (await userManager.GetRolesAsync(user)).Order()]);
}
