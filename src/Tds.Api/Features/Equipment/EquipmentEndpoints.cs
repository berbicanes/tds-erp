using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Materials;
using Tds.Api.Infrastructure;
using EquipmentEntity = Tds.Api.Domain.Equipment;

namespace Tds.Api.Features.Equipment;

/// <summary>SQL-side row; calibration state is added in memory from the latest calibration (DOMAIN §7.5).</summary>
public sealed record EquipmentRow
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
    public CalibrationResult? LastCalibrationResult { get; init; }
    public required bool IsActive { get; init; }
    public required uint Version { get; init; }
}

public sealed record EquipmentDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required EquipmentKind Kind { get; init; }
    public string? Manufacturer { get; init; }
    public string? SerialNumber { get; init; }
    public int? CalibrationIntervalMonths { get; init; }
    /// <summary>IZV from the latest non-voided calibration.</summary>
    public DateOnly? LastCalibrationDate { get; init; }
    /// <summary>IZV from the latest non-voided calibration.</summary>
    public DateOnly? NextCalibrationDate { get; init; }
    public CalibrationResult? LastCalibrationResult { get; init; }
    /// <summary>IZV, only for measuring instruments (null for machines).</summary>
    public CalibrationStatusAtCheck? CalibrationStatus { get; init; }
    public required bool IsActive { get; init; }
    public required uint Version { get; init; }

    public static EquipmentDto From(EquipmentRow r, DateOnly today) => new()
    {
        Id = r.Id, Code = r.Code, Name = r.Name, Kind = r.Kind, Manufacturer = r.Manufacturer, SerialNumber = r.SerialNumber,
        CalibrationIntervalMonths = r.CalibrationIntervalMonths, LastCalibrationDate = r.LastCalibrationDate,
        NextCalibrationDate = r.NextCalibrationDate, LastCalibrationResult = r.LastCalibrationResult,
        CalibrationStatus = r.Kind == EquipmentKind.MjernoSredstvo ? Calibration.Status(LastOf(r), today) : null,
        IsActive = r.IsActive, Version = r.Version,
    };

    public static LastCalibration? LastOf(EquipmentRow r) =>
        r.LastCalibrationDate is { } d ? new LastCalibration(d, r.NextCalibrationDate, r.LastCalibrationResult!.Value) : null;
}

public sealed record EquipmentOption(Guid Id, string Code, string Name, EquipmentKind Kind, CalibrationStatusAtCheck? CalibrationStatus);

public sealed record SaveEquipmentRequest(
    string Code, string Name, EquipmentKind? Kind, string? Manufacturer, string? SerialNumber,
    int? CalibrationIntervalMonths, bool? IsActive, uint? Version);

public sealed record CalibrationDto
{
    public required Guid Id { get; init; }
    public required DateOnly CalibrationDate { get; init; }
    public DateOnly? NextCalibrationDate { get; init; }
    public required string PerformedBy { get; init; }
    public required CalibrationResult Result { get; init; }
    public Guid? CertificateFileId { get; init; }
    public string? CertificateFileName { get; init; }
    public string? Note { get; init; }
    public string? CreatedByName { get; init; }
    public required bool IsVoided { get; init; }
    public string? VoidedReason { get; init; }
    public required uint Version { get; init; }
}

public sealed record RecordCalibrationRequest(
    DateOnly? CalibrationDate, DateOnly? NextCalibrationDate, string PerformedBy, CalibrationResult? Result, Guid? CertificateFileId, string? Note);

public sealed class SaveEquipmentValidator : AbstractValidator<SaveEquipmentRequest>
{
    public SaveEquipmentValidator(IHttpContextAccessor http)
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Unesi oznaku, npr. M-05 ili MS-001.")
            .MaximumLength(EquipmentEntity.CodeMax).WithMessage($"Oznaka može imati najviše {EquipmentEntity.CodeMax} znakova.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Unesi naziv.")
            .MaximumLength(EquipmentEntity.NameMax).WithMessage($"Naziv može imati najviše {EquipmentEntity.NameMax} znakova.");
        RuleFor(x => x.Kind).Required("Odaberi vrstu opreme.").IsInEnum().WithMessage("Odaberi vrstu opreme.");
        RuleFor(x => x.Manufacturer).MaximumLength(EquipmentEntity.ManufacturerMax).WithMessage($"Proizvođač može imati najviše {EquipmentEntity.ManufacturerMax} znakova.");
        RuleFor(x => x.SerialNumber).MaximumLength(EquipmentEntity.SerialNumberMax).WithMessage($"Serijski broj može imati najviše {EquipmentEntity.SerialNumberMax} znakova.");
        RuleFor(x => x.IsActive).Required("Odaberi da li je oprema aktivna.");
        this.VersionOnUpdate(http, x => x.Version);
        RuleFor(x => x.CalibrationIntervalMonths)
            .Null().When(x => x.Kind == EquipmentKind.Masina).WithMessage("Interval kalibracije se unosi samo za mjerna sredstva.")
            .InclusiveBetween(1, 120).When(x => x.CalibrationIntervalMonths is not null).WithMessage("Interval kalibracije mora biti između 1 i 120 mjeseci.");
    }
}

public sealed class RecordCalibrationValidator : AbstractValidator<RecordCalibrationRequest>
{
    public RecordCalibrationValidator()
    {
        RuleFor(x => x.CalibrationDate).NotEmpty().WithMessage("Unesi datum kalibracije.");
        RuleFor(x => x.PerformedBy).NotEmpty().WithMessage("Upiši ko je izvršio kalibraciju.")
            .MaximumLength(EquipmentCalibration.PerformedByMax).WithMessage($"Najviše {EquipmentCalibration.PerformedByMax} znakova.");
        RuleFor(x => x.Result).Required("Odaberi rezultat kalibracije.").IsInEnum().WithMessage("Odaberi rezultat kalibracije.");
        RuleFor(x => x.Note).MaximumLength(EquipmentCalibration.NoteMax).WithMessage($"Napomena može imati najviše {EquipmentCalibration.NoteMax} znakova.");
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
        read.MapGet("/{id:guid}/calibrations", Calibrations);

        var write = app.MapGroup("/api/equipment").RequireAuthorization(Policies.KontrolorOrAdmin);
        write.MapPost("/", Create).Validate<SaveEquipmentRequest>();
        write.MapPut("/{id:guid}", Update).Validate<SaveEquipmentRequest>();
        write.MapPost("/{id:guid}/calibrations", RecordCalibration).Validate<RecordCalibrationRequest>();

        app.MapPost("/api/equipment-calibrations/{id:guid}/void", VoidCalibration).Validate<VoidRequest>().RequireAuthorization(Policies.Admin);
    }

    private static readonly SortMap<EquipmentRow> Sort = new SortMap<EquipmentRow>()
        .Add("code", x => x.Code)
        .Add("name", x => x.Name)
        .Add("kind", x => x.Kind)
        .Add("nextCalibrationDate", x => x.NextCalibrationDate)
        .Add("isActive", x => x.IsActive)
        .Default("code");

    /// <summary>Latest/next calibration come from the latest non-voided calibration (DOMAIN §7.5).</summary>
    public static IQueryable<EquipmentRow> Rows(AppDbContext db) =>
        from e in db.Equipment
        let last = db.EquipmentCalibrations
            .Where(c => c.EquipmentId == e.Id && !c.IsVoided)
            .OrderByDescending(c => c.CalibrationDate).ThenByDescending(c => c.CreatedAt)
            .FirstOrDefault()
        select new EquipmentRow
        {
            Id = e.Id, Code = e.Code, Name = e.Name, Kind = e.Kind, Manufacturer = e.Manufacturer,
            SerialNumber = e.SerialNumber, CalibrationIntervalMonths = e.CalibrationIntervalMonths,
            LastCalibrationDate = last == null ? null : last.CalibrationDate,
            NextCalibrationDate = last == null ? null : last.NextCalibrationDate,
            LastCalibrationResult = last == null ? null : last.Result,
            IsActive = e.IsActive, Version = e.Version,
        };

    /// <summary>The state of one instrument now (used by quality checks to stamp CalibrationStatusAtCheck).</summary>
    public static async Task<CalibrationStatusAtCheck> CurrentStatusAsync(AppDbContext db, Guid equipmentId, DateOnly today, CancellationToken ct)
    {
        var row = await Rows(db).SingleAsync(e => e.Id == equipmentId, ct);
        return Calibration.Status(EquipmentDto.LastOf(row), today);
    }

    private static async Task<PagedResult<EquipmentDto>> List(
        AppDbContext db, IClock clock, [AsParameters] ListQuery list, EquipmentKind[]? kind, bool[]? isActive,
        CalibrationStatusAtCheck[]? calibrationStatus, CancellationToken ct)
    {
        var q = Rows(db);
        if (list.Search is { } s)
            q = q.Where(x => EF.Functions.ILike(x.Code, PagingExtensions.Like(s)) || EF.Functions.ILike(x.Name, PagingExtensions.Like(s))
                             || (x.SerialNumber != null && EF.Functions.ILike(x.SerialNumber, PagingExtensions.Like(s))));
        if (kind is { Length: > 0 })
            q = q.Where(x => kind.Contains(x.Kind));
        if (isActive is { Length: 1 })
            q = q.Where(x => x.IsActive == isActive[0]);

        var today = clock.Today;
        if (calibrationStatus is { Length: > 0 })
        {
            // Small table (dozens of rows): filter on the derived state in memory.
            var all = (await Sort.Apply(q, list.Sort).ToListAsync(ct)).Select(r => EquipmentDto.From(r, today))
                .Where(d => d.CalibrationStatus is { } cs && calibrationStatus.Contains(cs)).ToList();
            return new PagedResult<EquipmentDto>(all.Skip((list.SafePage - 1) * list.SafePageSize).Take(list.SafePageSize).ToList(),
                all.Count, list.SafePage, list.SafePageSize);
        }
        var page = await q.ToPagedAsync(list, Sort, x => x, ct);
        return new PagedResult<EquipmentDto>(page.Items.Select(r => EquipmentDto.From(r, today)).ToList(), page.Total, page.Page, page.PageSize);
    }

    /// <summary>Active equipment for pickers (machines in a production entry, instruments in a check — with their state).</summary>
    private static async Task<List<EquipmentOption>> Options(AppDbContext db, IClock clock, EquipmentKind? kind, CancellationToken ct) =>
        (await Rows(db).Where(e => e.IsActive && (kind == null || e.Kind == kind)).OrderBy(e => e.Code).ToListAsync(ct))
        .Select(r => EquipmentDto.From(r, clock.Today))
        .Select(d => new EquipmentOption(d.Id, d.Code, d.Name, d.Kind, d.CalibrationStatus)).ToList();

    public static async Task<EquipmentDto?> GetDtoAsync(AppDbContext db, Guid id, DateOnly today, CancellationToken ct) =>
        await Rows(db).SingleOrDefaultAsync(x => x.Id == id, ct) is { } r ? EquipmentDto.From(r, today) : null;

    private static async Task<IResult> Get(Guid id, AppDbContext db, IClock clock, CancellationToken ct) =>
        await GetDtoAsync(db, id, clock.Today, ct) is { } dto ? TypedResults.Ok(dto) : ApiResults.NotFound("Oprema ne postoji.");

    private static async Task<IResult> Create(SaveEquipmentRequest req, AppDbContext db, IClock clock, CancellationToken ct)
    {
        if (await CodeTaken(req.Code, null, db, ct))
            return ApiResults.Validation("code", $"Oznaka {req.Code.Trim()} je već zauzeta.");
        var e = new EquipmentEntity { Code = req.Code.Trim(), Name = req.Name.Trim() };
        Apply(e, req);
        db.Equipment.Add(e);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/equipment/{e.Id}", await GetDtoAsync(db, e.Id, clock.Today, ct));
    }

    private static async Task<IResult> Update(Guid id, SaveEquipmentRequest req, AppDbContext db, IClock clock, CancellationToken ct)
    {
        var e = await db.Equipment.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (e is null)
            return ApiResults.NotFound("Oprema ne postoji.");
        if (await CodeTaken(req.Code, id, db, ct))
            return ApiResults.Validation("code", $"Oznaka {req.Code.Trim()} je već zauzeta.");
        if (e.Kind != req.Kind && await db.EquipmentCalibrations.AnyAsync(c => c.EquipmentId == id, ct))
            return ApiResults.Validation("kind", "Vrsta se ne može promijeniti jer oprema ima zabilježene kalibracije.");
        db.Entry(e).Property(x => x.Version).OriginalValue = req.Version!.Value;
        e.Code = req.Code.Trim();
        e.Name = req.Name.Trim();
        Apply(e, req);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await GetDtoAsync(db, id, clock.Today, ct));
    }

    private static IQueryable<CalibrationDto> CalibrationRows(AppDbContext db) =>
        from c in db.EquipmentCalibrations
        join f in db.FileAttachments on c.CertificateFileId equals f.Id into fs
        from f in fs.DefaultIfEmpty()
        join u in db.Users on c.CreatedBy equals u.Id into us
        from u in us.DefaultIfEmpty()
        select new CalibrationDto
        {
            Id = c.Id, CalibrationDate = c.CalibrationDate, NextCalibrationDate = c.NextCalibrationDate, PerformedBy = c.PerformedBy,
            Result = c.Result, CertificateFileId = c.CertificateFileId, CertificateFileName = f.OriginalName, Note = c.Note,
            CreatedByName = u.UserName, IsVoided = c.IsVoided, VoidedReason = c.VoidedReason, Version = c.Version,
        };

    /// <summary>"Pokaži istoriju kalibracije za MS-003": one readable list, newest first, voided ones marked.</summary>
    private static async Task<IResult> Calibrations(Guid id, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Equipment.AnyAsync(e => e.Id == id, ct))
            return ApiResults.NotFound("Oprema ne postoji.");
        var ids = db.EquipmentCalibrations.Where(c => c.EquipmentId == id).Select(c => c.Id);
        return TypedResults.Ok(await CalibrationRows(db).Where(c => ids.Contains(c.Id))
            .OrderByDescending(c => c.CalibrationDate).ToListAsync(ct));
    }

    private static async Task<IResult> RecordCalibration(Guid id, RecordCalibrationRequest req, AppDbContext db, IClock clock, CancellationToken ct)
    {
        var e = await db.Equipment.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (e is null)
            return ApiResults.NotFound("Oprema ne postoji.");
        if (req.CertificateFileId is { } f && !await db.FileAttachments.AnyAsync(x => x.Id == f, ct))
            return ApiResults.Validation("certificateFileId", "Fajl certifikata ne postoji.");
        var c = EquipmentCalibration.Record(e, req.CalibrationDate!.Value, req.NextCalibrationDate, req.PerformedBy, req.Result!.Value,
            req.CertificateFileId, req.Note, clock.Today);
        db.EquipmentCalibrations.Add(c);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/equipment/{id}/calibrations", await CalibrationRows(db).SingleAsync(x => x.Id == c.Id, ct));
    }

    private static async Task<IResult> VoidCalibration(Guid id, VoidRequest req, AppDbContext db, CancellationToken ct)
    {
        var c = await db.EquipmentCalibrations.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (c is null)
            return ApiResults.NotFound("Kalibracija ne postoji.");
        db.Entry(c).Property(x => x.Version).OriginalValue = req.Version!.Value;
        c.Void(req.Reason);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await CalibrationRows(db).SingleAsync(x => x.Id == id, ct));
    }

    private static void Apply(EquipmentEntity e, SaveEquipmentRequest req)
    {
        e.Kind = req.Kind!.Value;
        e.Manufacturer = Clean(req.Manufacturer);
        e.SerialNumber = Clean(req.SerialNumber);
        e.IsActive = req.IsActive!.Value;
        e.CalibrationIntervalMonths = req.CalibrationIntervalMonths;
    }

    private static Task<bool> CodeTaken(string code, Guid? id, AppDbContext db, CancellationToken ct) =>
        db.Equipment.AnyAsync(x => x.Code == code.Trim() && x.Id != id, ct);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
