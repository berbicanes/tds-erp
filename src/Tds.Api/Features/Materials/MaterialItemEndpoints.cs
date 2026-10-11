using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Materials;

public sealed record MaterialItemDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Grade { get; init; }
    public decimal? ThicknessMm { get; init; }
    public required Unit Unit { get; init; }
    public decimal? MinimumStock { get; init; }
    public DateOnly? NextStocktakeDate { get; init; }
    public required bool IsActive { get; init; }
    public required uint Version { get; init; }
}

public sealed record MaterialItemOption(Guid Id, string Name, string? Grade, decimal? ThicknessMm, Unit Unit);

public sealed record SaveMaterialItemRequest(
    string Name, string? Grade, decimal? ThicknessMm, Unit? Unit, decimal? MinimumStock,
    DateOnly? NextStocktakeDate, bool? IsActive, uint? Version);

public sealed class SaveMaterialItemValidator : AbstractValidator<SaveMaterialItemRequest>
{
    public SaveMaterialItemValidator(IHttpContextAccessor http)
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Unesi naziv artikla, npr. Lim crni.")
            .MaximumLength(MaterialItem.NameMax).WithMessage($"Naziv može imati najviše {MaterialItem.NameMax} znakova.");
        RuleFor(x => x.Grade).MaximumLength(MaterialItem.GradeMax).WithMessage($"Kvalitet može imati najviše {MaterialItem.GradeMax} znakova.");
        RuleFor(x => x.ThicknessMm).GreaterThan(0).When(x => x.ThicknessMm is not null).WithMessage("Debljina mora biti veća od 0.")
            .LessThan(10000).WithMessage("Debljina je prevelika.")
            .Must(t => t is null || decimal.Round(t.Value, 2) == t).WithMessage("Debljina može imati najviše 2 decimale.");
        RuleFor(x => x.Unit).Required("Odaberi jedinicu.").IsInEnum().WithMessage("Odaberi jedinicu.");
        RuleFor(x => x.IsActive).Required("Odaberi da li je artikal aktivan.");
        this.VersionOnUpdate(http, x => x.Version);
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0).When(x => x.MinimumStock is not null).WithMessage("Minimalna zaliha ne može biti negativna.")
            .Must(m => m is null || decimal.Round(m.Value, 3) == m).WithMessage("Minimalna zaliha može imati najviše 3 decimale.");
    }
}

public static class MaterialItemEndpoints
{
    public static void MapMaterialItemEndpoints(this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup("/api/material-items").RequireAuthorization();
        read.MapGet("/", List);
        read.MapGet("/options", Options);
        read.MapGet("/{id:guid}", Get);

        var write = app.MapGroup("/api/material-items").RequireAuthorization(Policies.KontrolorOrAdmin);
        write.MapPost("/", Create).Validate<SaveMaterialItemRequest>();
        write.MapPut("/{id:guid}", Update).Validate<SaveMaterialItemRequest>();
    }

    private static readonly SortMap<MaterialItemDto> Sort = new SortMap<MaterialItemDto>()
        .Add("name", x => x.Name)
        .Add("grade", x => x.Grade)
        .Add("thicknessMm", x => x.ThicknessMm)
        .Add("nextStocktakeDate", x => x.NextStocktakeDate)
        .Default("name");

    private static IQueryable<MaterialItemDto> Project(AppDbContext db) =>
        db.MaterialItems.Select(m => new MaterialItemDto
        {
            Id = m.Id, Name = m.Name, Grade = m.Grade, ThicknessMm = m.ThicknessMm, Unit = m.Unit,
            MinimumStock = m.MinimumStock, NextStocktakeDate = m.NextStocktakeDate, IsActive = m.IsActive, Version = m.Version,
        });

    private static async Task<PagedResult<MaterialItemDto>> List(
        AppDbContext db, [AsParameters] ListQuery list, bool[]? isActive, CancellationToken ct)
    {
        var q = Project(db);
        if (list.Search is { } s)
            q = q.Where(x => EF.Functions.ILike(x.Name, PagingExtensions.Like(s)) || (x.Grade != null && EF.Functions.ILike(x.Grade, PagingExtensions.Like(s))));
        if (isActive is { Length: 1 })
            q = q.Where(x => x.IsActive == isActive[0]);
        return await q.ToPagedAsync(list, Sort, x => x, ct);
    }

    private static async Task<List<MaterialItemOption>> Options(AppDbContext db, CancellationToken ct) =>
        await db.MaterialItems.Where(m => m.IsActive).OrderBy(m => m.Name).ThenBy(m => m.Grade).ThenBy(m => m.ThicknessMm)
            .Select(m => new MaterialItemOption(m.Id, m.Name, m.Grade, m.ThicknessMm, m.Unit)).ToListAsync(ct);

    private static async Task<IResult> Get(Guid id, AppDbContext db, CancellationToken ct) =>
        await Project(db).SingleOrDefaultAsync(x => x.Id == id, ct) is { } dto
            ? TypedResults.Ok(dto)
            : ApiResults.NotFound("Artikal ne postoji.");

    private static async Task<IResult> Create(SaveMaterialItemRequest req, AppDbContext db, CancellationToken ct)
    {
        if (await Duplicate(req, null, db, ct) is { } problem)
            return problem;
        var m = new MaterialItem { Name = req.Name.Trim() };
        Apply(m, req);
        db.MaterialItems.Add(m);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/material-items/{m.Id}", await Project(db).SingleAsync(x => x.Id == m.Id, ct));
    }

    private static async Task<IResult> Update(Guid id, SaveMaterialItemRequest req, AppDbContext db, CancellationToken ct)
    {
        var m = await db.MaterialItems.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (m is null)
            return ApiResults.NotFound("Artikal ne postoji.");
        if (await Duplicate(req, id, db, ct) is { } problem)
            return problem;
        // DATA_MODEL §5: all lot and movement quantities are in this unit.
        if (m.Unit != req.Unit && await db.MaterialLots.AnyAsync(l => l.MaterialItemId == id, ct))
            return ApiResults.Validation("unit", "Jedinica se ne može promijeniti jer artikal već ima lotove; njihove količine su u ovoj jedinici.");
        db.Entry(m).Property(x => x.Version).OriginalValue = req.Version!.Value;
        m.Name = req.Name.Trim();
        Apply(m, req);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Project(db).SingleAsync(x => x.Id == id, ct));
    }

    private static async Task<IResult?> Duplicate(SaveMaterialItemRequest req, Guid? id, AppDbContext db, CancellationToken ct)
    {
        var name = req.Name.Trim();
        var grade = Clean(req.Grade);
        var exists = await db.MaterialItems.AnyAsync(x => x.Id != id && x.Name == name && x.Grade == grade && x.ThicknessMm == req.ThicknessMm, ct);
        return exists ? ApiResults.Validation("name", "Artikal sa ovim nazivom, kvalitetom i debljinom već postoji.") : null;
    }

    private static void Apply(MaterialItem m, SaveMaterialItemRequest req)
    {
        m.Grade = Clean(req.Grade);
        m.ThicknessMm = req.ThicknessMm;
        m.Unit = req.Unit!.Value;
        m.MinimumStock = req.MinimumStock;
        m.NextStocktakeDate = req.NextStocktakeDate;
        m.IsActive = req.IsActive!.Value;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
