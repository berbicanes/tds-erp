using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;
using EquipmentEntity = Tds.Api.Domain.Equipment;

namespace Tds.Api.Features.Equipment;

public sealed record EquipmentDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required EquipmentKind Kind { get; init; }
    public string? Manufacturer { get; init; }
    public string? SerialNumber { get; init; }
    public int? CalibrationIntervalMonths { get; init; }
    public DateOnly? LastCalibrationDate { get; init; }
    public DateOnly? NextCalibrationDate { get; init; }
    public Guid? CalibrationCertificateFileId { get; init; }
    public required bool IsActive { get; init; }
    public required uint Version { get; init; }
}

public sealed record EquipmentOption(Guid Id, string Code, string Name, EquipmentKind Kind);

public sealed record SaveEquipmentRequest(
    string Code, string Name, EquipmentKind Kind, string? Manufacturer, string? SerialNumber,
    int? CalibrationIntervalMonths, DateOnly? LastCalibrationDate, DateOnly? NextCalibrationDate,
    bool IsActive, uint Version);

public sealed class SaveEquipmentValidator : AbstractValidator<SaveEquipmentRequest>
{
    public SaveEquipmentValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Unesi oznaku, npr. M-05 ili MS-001.")
            .MaximumLength(EquipmentEntity.CodeMax).WithMessage($"Oznaka može imati najviše {EquipmentEntity.CodeMax} znakova.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Unesi naziv.")
            .MaximumLength(EquipmentEntity.NameMax).WithMessage($"Naziv može imati najviše {EquipmentEntity.NameMax} znakova.");
        RuleFor(x => x.Kind).IsInEnum().WithMessage("Odaberi vrstu opreme.");
        RuleFor(x => x.Manufacturer).MaximumLength(EquipmentEntity.ManufacturerMax).WithMessage($"Proizvođač može imati najviše {EquipmentEntity.ManufacturerMax} znakova.");
        RuleFor(x => x.SerialNumber).MaximumLength(EquipmentEntity.SerialNumberMax).WithMessage($"Serijski broj može imati najviše {EquipmentEntity.SerialNumberMax} znakova.");
        RuleFor(x => x.CalibrationIntervalMonths)
            .Null().When(x => x.Kind == EquipmentKind.Masina).WithMessage("Interval kalibracije se unosi samo za mjerna sredstva.")
            .InclusiveBetween(1, 120).When(x => x.CalibrationIntervalMonths is not null).WithMessage("Interval kalibracije mora biti između 1 i 120 mjeseci.");
    }
}

public static class EquipmentEndpoints
{
    public static void MapEquipmentEndpoints(this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup("/api/equipment").RequireAuthorization();
        read.MapGet("/", List);
        read.MapGet("/options", Options);
        read.MapGet("/{id:guid}", Get);

        var write = app.MapGroup("/api/equipment").RequireAuthorization(Policies.KontrolorOrAdmin);
        write.MapPost("/", Create).Validate<SaveEquipmentRequest>();
        write.MapPut("/{id:guid}", Update).Validate<SaveEquipmentRequest>();
    }

    private static readonly SortMap<EquipmentDto> Sort = new SortMap<EquipmentDto>()
        .Add("code", x => x.Code)
        .Add("name", x => x.Name)
        .Add("kind", x => x.Kind)
        .Add("nextCalibrationDate", x => x.NextCalibrationDate)
        .Add("isActive", x => x.IsActive)
        .Default("code");

    private static IQueryable<EquipmentDto> Project(AppDbContext db) =>
        db.Equipment.Select(e => new EquipmentDto
        {
            Id = e.Id, Code = e.Code, Name = e.Name, Kind = e.Kind, Manufacturer = e.Manufacturer,
            SerialNumber = e.SerialNumber, CalibrationIntervalMonths = e.CalibrationIntervalMonths,
            LastCalibrationDate = e.LastCalibrationDate, NextCalibrationDate = e.NextCalibrationDate,
            CalibrationCertificateFileId = e.CalibrationCertificateFileId, IsActive = e.IsActive, Version = e.Version,
        });

    private static async Task<PagedResult<EquipmentDto>> List(
        AppDbContext db, [AsParameters] ListQuery list, EquipmentKind[]? kind, bool[]? isActive, CancellationToken ct)
    {
        var q = Project(db);
        if (list.Search is { } s)
            q = q.Where(x => EF.Functions.ILike(x.Code, PagingExtensions.Like(s)) || EF.Functions.ILike(x.Name, PagingExtensions.Like(s))
                             || (x.SerialNumber != null && EF.Functions.ILike(x.SerialNumber, PagingExtensions.Like(s))));
        if (kind is { Length: > 0 })
            q = q.Where(x => kind.Contains(x.Kind));
        if (isActive is { Length: 1 })
            q = q.Where(x => x.IsActive == isActive[0]);
        return await q.ToPagedAsync(list, Sort, x => x, ct);
    }

    /// <summary>Active equipment for pickers, e.g. machines in a production entry.</summary>
    private static async Task<List<EquipmentOption>> Options(AppDbContext db, EquipmentKind? kind, CancellationToken ct) =>
        await db.Equipment.Where(e => e.IsActive && (kind == null || e.Kind == kind)).OrderBy(e => e.Code)
            .Select(e => new EquipmentOption(e.Id, e.Code, e.Name, e.Kind)).ToListAsync(ct);

    private static async Task<IResult> Get(Guid id, AppDbContext db, CancellationToken ct) =>
        await Project(db).SingleOrDefaultAsync(x => x.Id == id, ct) is { } dto
            ? TypedResults.Ok(dto)
            : ApiResults.NotFound("Oprema ne postoji.");

    private static async Task<IResult> Create(SaveEquipmentRequest req, AppDbContext db, CancellationToken ct)
    {
        if (await CodeTaken(req.Code, null, db, ct))
            return ApiResults.Validation("code", $"Oznaka {req.Code.Trim()} je već zauzeta.");
        var e = new EquipmentEntity { Code = req.Code.Trim(), Name = req.Name.Trim() };
        Apply(e, req);
        db.Equipment.Add(e);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/equipment/{e.Id}", await Project(db).SingleAsync(x => x.Id == e.Id, ct));
    }

    private static async Task<IResult> Update(Guid id, SaveEquipmentRequest req, AppDbContext db, CancellationToken ct)
    {
        var e = await db.Equipment.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (e is null)
            return ApiResults.NotFound("Oprema ne postoji.");
        if (await CodeTaken(req.Code, id, db, ct))
            return ApiResults.Validation("code", $"Oznaka {req.Code.Trim()} je već zauzeta.");
        db.Entry(e).Property(x => x.Version).OriginalValue = req.Version;
        e.Code = req.Code.Trim();
        e.Name = req.Name.Trim();
        Apply(e, req);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Project(db).SingleAsync(x => x.Id == id, ct));
    }

    private static void Apply(EquipmentEntity e, SaveEquipmentRequest req)
    {
        e.Kind = req.Kind;
        e.Manufacturer = Clean(req.Manufacturer);
        e.SerialNumber = Clean(req.SerialNumber);
        e.IsActive = req.IsActive;
        e.SetCalibration(req.CalibrationIntervalMonths, req.LastCalibrationDate, req.NextCalibrationDate);
    }

    private static Task<bool> CodeTaken(string code, Guid? id, AppDbContext db, CancellationToken ct) =>
        db.Equipment.AnyAsync(x => x.Code == code.Trim() && x.Id != id, ct);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
