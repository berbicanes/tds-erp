using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Users;

public sealed record UserDto(Guid Id, string UserName, string Role, bool IsActive, Guid? EmployeeId, string? EmployeeName);

public sealed record CreateUserRequest(string UserName, string Role, string Password);
public sealed record UpdateUserRequest(string Role, bool IsActive);
public sealed record SetPasswordRequest(string Password);

public sealed class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().WithMessage("Unesi korisničko ime.")
            .MaximumLength(50).WithMessage("Korisničko ime može imati najviše 50 znakova.")
            .Matches("^[a-zA-Z0-9._-]+$").WithMessage("Korisničko ime smije imati samo slova bez dijakritika, cifre, tačku, crticu i donju crtu.");
        RuleFor(x => x.Role).Must(r => Roles.All.Contains(r)).WithMessage("Odaberi ulogu.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Unesi lozinku.");
    }
}

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserValidator() =>
        RuleFor(x => x.Role).Must(r => Roles.All.Contains(r)).WithMessage("Odaberi ulogu.");
}

public sealed class SetPasswordValidator : AbstractValidator<SetPasswordRequest>
{
    public SetPasswordValidator() => RuleFor(x => x.Password).NotEmpty().WithMessage("Unesi novu lozinku.");
}

/// <summary>Login accounts (Admin only, DOMAIN §8). Accounts are deactivated, never deleted.</summary>
public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/users").RequireAuthorization(Policies.Admin);
        g.MapGet("/", List);
        g.MapPost("/", Create).Validate<CreateUserRequest>();
        g.MapPut("/{id:guid}", Update).Validate<UpdateUserRequest>();
        g.MapPost("/{id:guid}/password", SetPassword).Validate<SetPasswordRequest>();
    }

    /// <summary>Deactivated = locked out "forever"; LockoutEnd at max value is our marker.</summary>
    public static bool IsDeactivated(AppUser u) => u.LockoutEnd == DateTimeOffset.MaxValue;

    private static async Task<List<UserDto>> List(AppDbContext db, CancellationToken ct)
    {
        var rows = await (
            from u in db.Users
            join ur in db.UserRoles on u.Id equals ur.UserId into urs
            from ur in urs.DefaultIfEmpty()
            join r in db.Roles on ur.RoleId equals r.Id into rs
            from r in rs.DefaultIfEmpty()
            join e in db.Employees on u.Id equals e.UserId into es
            from e in es.DefaultIfEmpty()
            orderby u.UserName
            select new { u.Id, u.UserName, Role = r.Name, u.LockoutEnd, EmployeeId = (Guid?)e.Id, EmployeeName = e.FullName }
        ).ToListAsync(ct);
        return rows.Select(x => new UserDto(x.Id, x.UserName!, x.Role ?? "", x.LockoutEnd != DateTimeOffset.MaxValue, x.EmployeeId, x.EmployeeName)).ToList();
    }

    private static async Task<IResult> Create(CreateUserRequest req, UserManager<AppUser> users, AppDbContext db, CancellationToken ct)
    {
        var userName = req.UserName.Trim();
        if (await users.FindByNameAsync(userName) is not null)
            return ApiResults.Validation("userName", $"Korisničko ime {userName} je zauzeto.");

        var user = new AppUser { UserName = userName };
        var created = await users.CreateAsync(user, req.Password);
        if (!created.Succeeded)
            return PasswordProblem(created);
        await users.AddToRoleAsync(user, req.Role);
        return TypedResults.Created($"/api/users/{user.Id}", (await List(db, ct)).Single(u => u.Id == user.Id));
    }

    private static async Task<IResult> Update(
        Guid id, UpdateUserRequest req, UserManager<AppUser> users, ICurrentUser current, AppDbContext db, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
            return ApiResults.NotFound("Korisnik ne postoji.");
        if (id == current.UserId && (!req.IsActive || req.Role != Roles.Admin))
            return ApiResults.Conflict("Ne možeš sebi ukloniti ulogu Admin niti deaktivirati svoj nalog.");

        var roles = await users.GetRolesAsync(user);
        if (!roles.SequenceEqual([req.Role]))
        {
            await users.RemoveFromRolesAsync(user, roles);
            await users.AddToRoleAsync(user, req.Role);
        }

        var wasActive = !IsDeactivated(user);
        if (wasActive != req.IsActive)
        {
            await users.SetLockoutEnabledAsync(user, true);
            await users.SetLockoutEndDateAsync(user, req.IsActive ? null : DateTimeOffset.MaxValue);
        }

        // Role or status change: existing sessions must re-validate (SecurityStampValidator).
        await users.UpdateSecurityStampAsync(user);
        return TypedResults.Ok((await List(db, ct)).Single(u => u.Id == id));
    }

    private static async Task<IResult> SetPassword(Guid id, SetPasswordRequest req, UserManager<AppUser> users)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
            return ApiResults.NotFound("Korisnik ne postoji.");
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, req.Password);
        return result.Succeeded ? TypedResults.NoContent() : PasswordProblem(result);
    }

    private static IResult PasswordProblem(IdentityResult result)
    {
        var messages = result.Errors.Select(e => e.Code switch
        {
            nameof(IdentityErrorDescriber.PasswordTooShort) => "Lozinka mora imati najmanje 10 znakova.",
            nameof(IdentityErrorDescriber.PasswordRequiresDigit) => "Lozinka mora imati barem jednu cifru.",
            nameof(IdentityErrorDescriber.PasswordRequiresLower) => "Lozinka mora imati barem jedno malo slovo.",
            nameof(IdentityErrorDescriber.DuplicateUserName) => "Korisničko ime je zauzeto.",
            nameof(IdentityErrorDescriber.InvalidUserName) => "Korisničko ime nije ispravno.",
            _ => "Lozinka ili korisničko ime nisu ispravni.",
        }).Distinct().ToArray();
        return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["password"] = messages },
            detail: string.Join(" ", messages), title: "Neispravni podaci");
    }
}
