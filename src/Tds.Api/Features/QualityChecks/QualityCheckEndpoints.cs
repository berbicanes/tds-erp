using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Equipment;
using Tds.Api.Features.Materials;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.QualityChecks;

public sealed record EvidenceFileDto(Guid FileId, string OriginalName, string ContentType, long SizeBytes);

public sealed record QualityCheckDto
{
    public required Guid Id { get; init; }
    public required string Number { get; init; }
    public Guid? WorkOrderId { get; init; }
    public string? WorkOrderNumber { get; init; }
    public Guid? MaterialLotId { get; init; }
    public string? LotNumber { get; init; }
    public required InspectionPhase Phase { get; init; }
    public required string Characteristic { get; init; }
    public string? AcceptanceCriterion { get; init; }
    public string? MeasuredResult { get; init; }
    public int? SampleSize { get; init; }
    public required InspectionResult Result { get; init; }
    public required Guid InspectedByEmployeeId { get; init; }
    public required string InspectedByName { get; init; }
    public required DateOnly InspectionDate { get; init; }
    public Guid? MeasuringEquipmentId { get; init; }
    public string? MeasuringEquipmentCode { get; init; }
    public string? MeasuringEquipmentName { get; init; }
    /// <summary>Permanent: state of the instrument when the check was made (DOMAIN §7.5).</summary>
    public required CalibrationStatusAtCheck CalibrationStatusAtCheck { get; init; }
    public required int EvidenceCount { get; init; }
    /// <summary>The non-voided NC raised from this check, if any (DOMAIN §7.1a).</summary>
    public Guid? NonConformityId { get; init; }
    public string? NonConformityNumber { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required bool IsVoided { get; init; }
    public string? VoidedReason { get; init; }
    public required uint Version { get; init; }
}

public sealed record QualityCheckDetailDto(QualityCheckDto Check, IReadOnlyList<EvidenceFileDto> Evidence);

public sealed record CreateQualityCheckRequest(
    Guid? WorkOrderId, Guid? MaterialLotId, InspectionPhase? Phase, string Characteristic, string? AcceptanceCriterion,
    string? MeasuredResult, int? SampleSize, InspectionResult? Result, Guid? InspectedByEmployeeId, DateOnly? InspectionDate,
    Guid? MeasuringEquipmentId, List<Guid>? EvidenceFileIds);

public sealed record FileIdRequest(Guid? FileId);

public sealed class FileIdValidator : AbstractValidator<FileIdRequest>
{
    public FileIdValidator() => RuleFor(x => x.FileId).NotEmpty().WithMessage("Odaberi fajl.");
}

public sealed class CreateQualityCheckValidator : AbstractValidator<CreateQualityCheckRequest>
{
    public CreateQualityCheckValidator()
    {
        RuleFor(x => x.Phase).Required("Odaberi fazu.").IsInEnum().WithMessage("Odaberi fazu.");
        RuleFor(x => x.Characteristic).NotEmpty().WithMessage("Unesi karakteristiku / zahtjev crteža.")
            .MaximumLength(QualityCheck.TextMax).WithMessage($"Najviše {QualityCheck.TextMax} znakova.");
        RuleFor(x => x.AcceptanceCriterion).MaximumLength(QualityCheck.TextMax).WithMessage($"Najviše {QualityCheck.TextMax} znakova.");
        RuleFor(x => x.MeasuredResult).MaximumLength(QualityCheck.TextMax).WithMessage($"Najviše {QualityCheck.TextMax} znakova.");
        RuleFor(x => x.SampleSize).GreaterThanOrEqualTo(1).When(x => x.SampleSize is not null).WithMessage("Veličina uzorka mora biti najmanje 1.");
        RuleFor(x => x.Result).Required("Odaberi rezultat.").IsInEnum().WithMessage("Odaberi rezultat.");
        RuleFor(x => x.InspectedByEmployeeId).NotEmpty().WithMessage("Odaberi ko je kontrolisao.");
        RuleFor(x => x.InspectionDate).NotEmpty().WithMessage("Unesi datum kontrole.");
    }
}

public static class QualityCheckEndpoints
{
    public static void MapQualityCheckEndpoints(this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup("/api").RequireAuthorization();
        read.MapGet("/quality-checks", List);
        read.MapGet("/quality-checks/{id:guid}", Get);
        read.MapGet("/work-orders/{id:guid}/quality-checks", ListForWorkOrder);
        read.MapGet("/lots/{id:guid}/quality-checks", ListForLot);

        app.MapPost("/api/quality-checks", Create).Validate<CreateQualityCheckRequest>().RequireAuthorization(Policies.KontrolorOrAdmin);
        app.MapPost("/api/quality-checks/{id:guid}/evidence", AddEvidence).Validate<FileIdRequest>().RequireAuthorization(Policies.KontrolorOrAdmin);
        // DOMAIN §7.7: only Admin removes a file link; the file itself stays.
        app.MapPost("/api/quality-checks/{id:guid}/evidence/{fileId:guid}/remove", RemoveEvidence).RequireAuthorization(Policies.Admin);
        app.MapPost("/api/quality-checks/{id:guid}/void", Void).Validate<VoidRequest>().RequireAuthorization(Policies.Admin);
    }

    public static IQueryable<QualityCheckDto> Project(AppDbContext db) =>
        from q in db.QualityChecks
        join e in db.Employees on q.InspectedByEmployeeId equals e.Id
        join w in db.WorkOrders on q.WorkOrderId equals w.Id into ws
        from w in ws.DefaultIfEmpty()
        join l in db.MaterialLots on q.MaterialLotId equals l.Id into ls
        from l in ls.DefaultIfEmpty()
        join m in db.Equipment on q.MeasuringEquipmentId equals m.Id into ms
        from m in ms.DefaultIfEmpty()
        let nc = db.NonConformities.Where(n => n.QualityCheckId == q.Id && !n.IsVoided).Select(n => new { n.Id, n.Number }).FirstOrDefault()
        select new QualityCheckDto
        {
            Id = q.Id, Number = q.Number, WorkOrderId = q.WorkOrderId, WorkOrderNumber = w.Number,
            MaterialLotId = q.MaterialLotId, LotNumber = l.LotNumber, Phase = q.Phase, Characteristic = q.Characteristic,
            AcceptanceCriterion = q.AcceptanceCriterion, MeasuredResult = q.MeasuredResult, SampleSize = q.SampleSize,
            Result = q.Result, InspectedByEmployeeId = q.InspectedByEmployeeId, InspectedByName = e.FullName,
            InspectionDate = q.InspectionDate, MeasuringEquipmentId = q.MeasuringEquipmentId,
            MeasuringEquipmentCode = m.Code, MeasuringEquipmentName = m.Name, CalibrationStatusAtCheck = q.CalibrationStatusAtCheck,
            EvidenceCount = q.Evidence.Count, NonConformityId = nc == null ? null : nc.Id, NonConformityNumber = nc == null ? null : nc.Number,
            CreatedAt = q.CreatedAt, IsVoided = q.IsVoided, VoidedReason = q.VoidedReason, Version = q.Version,
        };

    private static async Task<PagedResult<QualityCheckDto>> List(
        AppDbContext db, [AsParameters] ListQuery list, InspectionPhase[]? phase, InspectionResult[]? result,
        bool? calibrationWarning, bool? includeVoided, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var q = Project(db);
        if (includeVoided != true)
            q = q.Where(x => !x.IsVoided);
        if (phase is { Length: > 0 })
            q = q.Where(x => phase.Contains(x.Phase));
        if (result is { Length: > 0 })
            q = q.Where(x => result.Contains(x.Result));
        if (calibrationWarning == true)
            q = q.Where(x => x.CalibrationStatusAtCheck == CalibrationStatusAtCheck.VanKalibracije
                             || x.CalibrationStatusAtCheck == CalibrationStatusAtCheck.BezDatumaKalibracije);
        if (from is { } f) q = q.Where(x => x.InspectionDate >= f);
        if (to is { } t) q = q.Where(x => x.InspectionDate <= t);
        if (list.Search is { } s)
        {
            var like = PagingExtensions.Like(s);
            q = q.Where(x => EF.Functions.ILike(x.Number, like) || EF.Functions.ILike(x.Characteristic, like)
                             || (x.WorkOrderNumber != null && EF.Functions.ILike(x.WorkOrderNumber, like))
                             || (x.LotNumber != null && EF.Functions.ILike(x.LotNumber, like))
                             || EF.Functions.ILike(x.InspectedByName, like));
        }
        var sort = new SortMap<QualityCheckDto>()
            .Add("number", x => x.Number).Add("inspectionDate", x => x.InspectionDate).Add("phase", x => x.Phase).Add("result", x => x.Result)
            .Default("number", descending: true);
        return await q.ToPagedAsync(list, sort, x => x, ct);
    }

    private static async Task<IResult> Get(Guid id, AppDbContext db, CancellationToken ct)
    {
        var check = await Project(db).SingleOrDefaultAsync(q => q.Id == id, ct);
        if (check is null)
            return ApiResults.NotFound("Kontrola ne postoji.");
        var evidence = await (from ev in db.Set<QualityCheckEvidence>()
                              where ev.QualityCheckId == id
                              join f in db.FileAttachments on ev.FileId equals f.Id
                              orderby f.CreatedAt
                              select new EvidenceFileDto(f.Id, f.OriginalName, f.ContentType, f.SizeBytes)).ToListAsync(ct);
        return TypedResults.Ok(new QualityCheckDetailDto(check, evidence));
    }

    private static async Task<IResult> ListForWorkOrder(Guid id, AppDbContext db, CancellationToken ct)
    {
        if (!await db.WorkOrders.AnyAsync(w => w.Id == id, ct))
            return ApiResults.NotFound("Nalog ne postoji.");
        return TypedResults.Ok(await Project(db).Where(q => q.WorkOrderId == id)
            .OrderBy(q => q.Phase).ThenByDescending(q => q.InspectionDate).ThenByDescending(q => q.Number).ToListAsync(ct));
    }

    private static async Task<IResult> ListForLot(Guid id, AppDbContext db, CancellationToken ct)
    {
        if (!await db.MaterialLots.AnyAsync(l => l.Id == id, ct))
            return ApiResults.NotFound("Lot ne postoji.");
        return TypedResults.Ok(await Project(db).Where(q => q.MaterialLotId == id)
            .OrderByDescending(q => q.InspectionDate).ThenByDescending(q => q.Number).ToListAsync(ct));
    }

    private static async Task<IResult> Create(
        CreateQualityCheckRequest req, AppDbContext db, DocumentNumberService numbers, IClock clock, CancellationToken ct)
    {
        if (req.InspectionDate > clock.Today)
            return ApiResults.Validation("inspectionDate", "Datum kontrole ne može biti u budućnosti.");
        if (req.Phase != InspectionPhase.UlazniMaterijal && req.WorkOrderId is null)
            return ApiResults.Validation("workOrderId", $"Kontrola faze {DisplayNames.Of(req.Phase!.Value)} se veže za nalog — odaberi nalog.");
        if (req.Phase == InspectionPhase.UlazniMaterijal && req.MaterialLotId is null)
            return ApiResults.Validation("materialLotId", "Ulazna kontrola se veže za lot — odaberi lot.");
        QualityCheck.CheckOwner(req.Phase!.Value, req.WorkOrderId, req.MaterialLotId);
        if (req.WorkOrderId is { } wo && !await db.WorkOrders.AnyAsync(w => w.Id == wo, ct))
            return ApiResults.Validation("workOrderId", "Nalog ne postoji.");
        if (req.MaterialLotId is { } lot && !await db.MaterialLots.AnyAsync(l => l.Id == lot, ct))
            return ApiResults.Validation("materialLotId", "Lot ne postoji.");
        if (!await db.Employees.AnyAsync(e => e.Id == req.InspectedByEmployeeId, ct))
            return ApiResults.Validation("inspectedByEmployeeId", "Zaposlenik ne postoji.");
        if (req.MeasuringEquipmentId is { } me
            && !await db.Equipment.AnyAsync(e => e.Id == me && e.IsActive && e.Kind == EquipmentKind.MjernoSredstvo, ct))
            return ApiResults.Validation("measuringEquipmentId", "Odaberi aktivno mjerno sredstvo.");
        var fileIds = (req.EvidenceFileIds ?? []).Distinct().ToList();
        if (fileIds.Count > 0 && await db.FileAttachments.CountAsync(f => fileIds.Contains(f.Id), ct) != fileIds.Count)
            return ApiResults.Validation("evidenceFileIds", "Neki od fajlova ne postoje.");

        // DOMAIN §7.5: stamp the instrument's state now; it never changes later.
        var calibration = req.MeasuringEquipmentId is { } instrument
            ? await EquipmentEndpoints.CurrentStatusAsync(db, instrument, clock.Today, ct)
            : CalibrationStatusAtCheck.BezMjernogSredstva;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var check = new QualityCheck
        {
            Number = await numbers.NextAsync(DocumentNumberKind.QualityCheck, req.InspectionDate!.Value, ct),
            WorkOrderId = req.WorkOrderId,
            MaterialLotId = req.MaterialLotId,
            Phase = req.Phase!.Value,
            Characteristic = req.Characteristic.Trim(),
            AcceptanceCriterion = Clean(req.AcceptanceCriterion),
            MeasuredResult = Clean(req.MeasuredResult),
            SampleSize = req.SampleSize,
            Result = req.Result!.Value,
            InspectedByEmployeeId = req.InspectedByEmployeeId!.Value,
            InspectionDate = req.InspectionDate!.Value,
            MeasuringEquipmentId = req.MeasuringEquipmentId,
            CalibrationStatusAtCheck = calibration,
        };
        foreach (var f in fileIds)
            check.Evidence.Add(new QualityCheckEvidence { QualityCheckId = check.Id, FileId = f });
        db.QualityChecks.Add(check);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var dto = await Project(db).SingleAsync(q => q.Id == check.Id, ct);
        IReadOnlyList<string> warnings = Calibration.NeedsWarning(calibration)
            ? [calibration == CalibrationStatusAtCheck.VanKalibracije
                ? $"Mjerno sredstvo {dto.MeasuringEquipmentCode} van kalibracije — kontrola ima trajnu oznaku."
                : $"Mjerno sredstvo {dto.MeasuringEquipmentCode} nema datum kalibracije — kontrola ima trajnu oznaku."]
            : [];
        return TypedResults.Created($"/api/quality-checks/{check.Id}", new WithWarnings<QualityCheckDto>(dto, warnings));
    }

    private static async Task<IResult> AddEvidence(Guid id, FileIdRequest req, AppDbContext db, CancellationToken ct)
    {
        var check = await db.QualityChecks.Include(q => q.Evidence).SingleOrDefaultAsync(q => q.Id == id, ct);
        if (check is null)
            return ApiResults.NotFound("Kontrola ne postoji.");
        if (!await db.FileAttachments.AnyAsync(f => f.Id == req.FileId, ct))
            return ApiResults.Validation("fileId", "Fajl ne postoji.");
        if (check.Evidence.All(e => e.FileId != req.FileId))
            check.Evidence.Add(new QualityCheckEvidence { QualityCheckId = id, FileId = req.FileId!.Value });
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> RemoveEvidence(Guid id, Guid fileId, AppDbContext db, CancellationToken ct)
    {
        var check = await db.QualityChecks.Include(q => q.Evidence).SingleOrDefaultAsync(q => q.Id == id, ct);
        if (check is null)
            return ApiResults.NotFound("Kontrola ne postoji.");
        check.Evidence.RemoveAll(e => e.FileId == fileId);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> Void(Guid id, VoidRequest req, AppDbContext db, CancellationToken ct)
    {
        var check = await db.QualityChecks.SingleOrDefaultAsync(q => q.Id == id, ct);
        if (check is null)
            return ApiResults.NotFound("Kontrola ne postoji.");
        db.Entry(check).Property(x => x.Version).OriginalValue = req.Version!.Value;
        check.Void(req.Reason);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Project(db).SingleAsync(q => q.Id == id, ct));
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
