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

    public const string WrongCredentials = "Pogrešno korisničko ime ili lozinka.";

    private static async Task<IResult> Login(
        LoginRequest request, SignInManager<AppUser> signInManager, UserManager<AppUser> userManager)
    {
        // DOMAIN §8 (PREGLED N4): one message for every failure. A deactivated account is locked "forever" and a
        // locked account's password is never checked, so neither can be guessed nor told apart from a missing one.
        var result = await signInManager.PasswordSignInAsync(
            request.UserName.Trim(), request.Password, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Problems.Unauthorized(WrongCredentials);

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
