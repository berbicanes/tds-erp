using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Employees;

/// <summary>Init-only properties (not a positional record) so EF can sort/filter on the projection.</summary>
public sealed record EmployeeDto
{
    public required Guid Id { get; init; }
    public required string FullName { get; init; }
    public string? Position { get; init; }
    public required bool IsActive { get; init; }
    public Guid? UserId { get; init; }
    public string? UserName { get; init; }
    public required uint Version { get; init; }
}

public sealed record EmployeeOption(Guid Id, string FullName, string? Position);

public sealed record SaveEmployeeRequest(string FullName, string? Position, bool IsActive, Guid? UserId, uint Version);

public sealed class SaveEmployeeValidator : AbstractValidator<SaveEmployeeRequest>
{
    public SaveEmployeeValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Unesi ime i prezime.")
            .MaximumLength(Employee.FullNameMax).WithMessage($"Ime i prezime može imati najviše {Employee.FullNameMax} znakova.");
        RuleFor(x => x.Position).MaximumLength(Employee.PositionMax)
            .WithMessage($"Radno mjesto može imati najviše {Employee.PositionMax} znakova.");
    }
}

public static class EmployeeEndpoints
{
    public static void MapEmployeeEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/employees").RequireAuthorization(Policies.Admin);
        admin.MapGet("/", List);
        admin.MapGet("/{id:guid}", Get);
        admin.MapPost("/", Create).Validate<SaveEmployeeRequest>();
        admin.MapPut("/{id:guid}", Update).Validate<SaveEmployeeRequest>();

        // Pickers (operator in a production entry, inspector, responsible person): any signed-in user.
        app.MapGet("/api/employees/options", Options).RequireAuthorization();
    }

    private static readonly SortMap<EmployeeDto> Sort = new SortMap<EmployeeDto>()
        .Add("fullName", x => x.FullName)
        .Add("position", x => x.Position)
        .Add("isActive", x => x.IsActive)
        .Default("fullName");

    private static IQueryable<EmployeeDto> Project(AppDbContext db) =>
        from e in db.Employees
        join u in db.Users on e.UserId equals u.Id into users
        from u in users.DefaultIfEmpty()
        select new EmployeeDto
        {
            Id = e.Id, FullName = e.FullName, Position = e.Position, IsActive = e.IsActive,
            UserId = e.UserId, UserName = u.UserName, Version = e.Version,
        };

    private static async Task<PagedResult<EmployeeDto>> List(
        AppDbContext db, [AsParameters] ListQuery list, bool[]? isActive, CancellationToken ct)
    {
        var q = Project(db);
        if (list.Search is { } s)
            q = q.Where(x => EF.Functions.ILike(x.FullName, PagingExtensions.Like(s))
                             || (x.Position != null && EF.Functions.ILike(x.Position, PagingExtensions.Like(s))));
        if (isActive is { Length: 1 })
            q = q.Where(x => x.IsActive == isActive[0]);
        return await q.ToPagedAsync(list, Sort, x => x, ct);
    }

    private static async Task<IResult> Get(Guid id, AppDbContext db, CancellationToken ct) =>
        await Project(db).SingleOrDefaultAsync(x => x.Id == id, ct) is { } dto
            ? TypedResults.Ok(dto)
            : ApiResults.NotFound("Zaposlenik ne postoji.");

    private static async Task<List<EmployeeOption>> Options(AppDbContext db, CancellationToken ct) =>
        await db.Employees.Where(e => e.IsActive).OrderBy(e => e.FullName)
            .Select(e => new EmployeeOption(e.Id, e.FullName, e.Position)).ToListAsync(ct);

    private static async Task<IResult> Create(SaveEmployeeRequest req, AppDbContext db, CancellationToken ct)
    {
        if (await CheckUser(req.UserId, null, db, ct) is { } problem)
            return problem;
        var e = new Employee { FullName = req.FullName.Trim(), Position = Clean(req.Position), IsActive = req.IsActive, UserId = req.UserId };
        db.Employees.Add(e);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/employees/{e.Id}", await Project(db).SingleAsync(x => x.Id == e.Id, ct));
    }

    private static async Task<IResult> Update(Guid id, SaveEmployeeRequest req, AppDbContext db, CancellationToken ct)
    {
        var e = await db.Employees.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (e is null)
            return ApiResults.NotFound("Zaposlenik ne postoji.");
        if (await CheckUser(req.UserId, id, db, ct) is { } problem)
            return problem;

        db.Entry(e).Property(x => x.Version).OriginalValue = req.Version;
        e.FullName = req.FullName.Trim();
        e.Position = Clean(req.Position);
        e.IsActive = req.IsActive;
        e.UserId = req.UserId;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Project(db).SingleAsync(x => x.Id == id, ct));
    }

    private static async Task<IResult?> CheckUser(Guid? userId, Guid? employeeId, AppDbContext db, CancellationToken ct)
    {
        if (userId is null)
            return null;
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
            return ApiResults.Validation("userId", "Korisnički nalog ne postoji.");
        var other = await db.Employees.Where(x => x.UserId == userId && x.Id != employeeId).Select(x => x.FullName).FirstOrDefaultAsync(ct);
        return other is null ? null : ApiResults.Validation("userId", $"Korisnički nalog je već vezan za zaposlenika {other}.");
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
