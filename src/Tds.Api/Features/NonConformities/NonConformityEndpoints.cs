using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Materials;
using Tds.Api.Features.QualityChecks;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.NonConformities;

public sealed record NonConformityDto
{
    public required Guid Id { get; init; }
    public required string Number { get; init; }
    public Guid? WorkOrderId { get; init; }
    public string? WorkOrderNumber { get; init; }
    public Guid? MaterialLotId { get; init; }
    public string? LotNumber { get; init; }
    public Guid? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public Guid? QualityCheckId { get; init; }
    public string? QualityCheckNumber { get; init; }
    public required NcSource Source { get; init; }
    public required string Description { get; init; }
    public string? Containment { get; init; }
    public required ProductDecision ProductDecision { get; init; }
    public string? RootCause { get; init; }
    public string? CorrectiveAction { get; init; }
    public Guid? ActionOwnerEmployeeId { get; init; }
    public string? ActionOwnerName { get; init; }
    public DateOnly? DueDate { get; init; }
    public DateOnly? EffectivenessCheckDate { get; init; }
    public required NcStatus Status { get; init; }
    public required int EvidenceCount { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required bool IsVoided { get; init; }
    public string? VoidedReason { get; init; }
    public required uint Version { get; init; }
}

public sealed record NcDetailDto(NonConformityDto Nc, IReadOnlyList<EvidenceFileDto> Evidence, IReadOnlyList<NcTransitionCheck> Transitions);

/// <summary>DOMAIN §7.1a: what the "Otvori NC" form starts with, or the NC that already exists for the check.</summary>
public sealed record NcPrefillDto(
    Guid? ExistingId, string? ExistingNumber, Guid QualityCheckId, string QualityCheckNumber, NcSource Source,
    Guid? WorkOrderId, string? WorkOrderNumber, Guid? MaterialLotId, string? LotNumber, Guid? SupplierId, string? SupplierName, string Description);

public sealed record SaveNcRequest(
    Guid? WorkOrderId, Guid? MaterialLotId, Guid? SupplierId, Guid? QualityCheckId, NcSource? Source, string Description,
    string? Containment, ProductDecision? ProductDecision, string? RootCause, string? CorrectiveAction,
    Guid? ActionOwnerEmployeeId, DateOnly? DueDate, DateOnly? EffectivenessCheckDate, List<Guid>? EvidenceFileIds, uint? Version);

public sealed record NcStatusRequest(NcStatus? Status, uint? Version);

public sealed class NcStatusValidator : AbstractValidator<NcStatusRequest>
{
    public NcStatusValidator()
    {
        RuleFor(x => x.Status).Required("Odaberi novi status.").IsInEnum().WithMessage("Odaberi novi status.");
        RuleFor(x => x.Version).Required(ValidationExtensions.VersionMissing);
    }
}

public sealed class SaveNcValidator : AbstractValidator<SaveNcRequest>
{
    public SaveNcValidator(IHttpContextAccessor http)
    {
        RuleFor(x => x.Source).Required("Odaberi izvor.").IsInEnum().WithMessage("Odaberi izvor.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Opiši neusaglašenost.")
            .MaximumLength(NonConformity.TextMax).WithMessage($"Najviše {NonConformity.TextMax} znakova.");
        RuleFor(x => x.Containment).MaximumLength(NonConformity.TextMax).WithMessage($"Najviše {NonConformity.TextMax} znakova.");
        RuleFor(x => x.RootCause).MaximumLength(NonConformity.TextMax).WithMessage($"Najviše {NonConformity.TextMax} znakova.");
        RuleFor(x => x.CorrectiveAction).MaximumLength(NonConformity.TextMax).WithMessage($"Najviše {NonConformity.TextMax} znakova.");
        RuleFor(x => x.ProductDecision).Required("Odaberi odluku o proizvodu.").IsInEnum().WithMessage("Odaberi odluku o proizvodu.");
        this.VersionOnUpdate(http, x => x.Version);
    }
}

public static class NonConformityEndpoints
{
    public static void MapNonConformityEndpoints(this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup("/api").RequireAuthorization();
        read.MapGet("/non-conformities", List);
        read.MapGet("/non-conformities/{id:guid}", Get);
        read.MapGet("/non-conformities/prefill", Prefill);
        read.MapGet("/work-orders/{id:guid}/non-conformities", ListForWorkOrder);
        read.MapGet("/lots/{id:guid}/non-conformities", ListForLot);

        var write = app.MapGroup("/api/non-conformities").RequireAuthorization(Policies.KontrolorOrAdmin);
        write.MapPost("/", Create).Validate<SaveNcRequest>();
        write.MapPut("/{id:guid}", Update).Validate<SaveNcRequest>();
        write.MapPost("/{id:guid}/status", ChangeStatus).Validate<NcStatusRequest>();
        write.MapPost("/{id:guid}/evidence", AddEvidence).Validate<FileIdRequest>();

        var admin = app.MapGroup("/api/non-conformities").RequireAuthorization(Policies.Admin);
        admin.MapPost("/{id:guid}/evidence/{fileId:guid}/remove", RemoveEvidence);
        admin.MapPost("/{id:guid}/void", Void).Validate<VoidRequest>();
    }

    public static IQueryable<NonConformityDto> Project(AppDbContext db) =>
        from n in db.NonConformities
        join w in db.WorkOrders on n.WorkOrderId equals w.Id into ws
        from w in ws.DefaultIfEmpty()
        join l in db.MaterialLots on n.MaterialLotId equals l.Id into ls
        from l in ls.DefaultIfEmpty()
        join s in db.Suppliers on n.SupplierId equals s.Id into ss
        from s in ss.DefaultIfEmpty()
        join q in db.QualityChecks on n.QualityCheckId equals q.Id into qs
        from q in qs.DefaultIfEmpty()
        join e in db.Employees on n.ActionOwnerEmployeeId equals e.Id into es
        from e in es.DefaultIfEmpty()
        select new NonConformityDto
        {
            Id = n.Id, Number = n.Number, WorkOrderId = n.WorkOrderId, WorkOrderNumber = w.Number,
            MaterialLotId = n.MaterialLotId, LotNumber = l.LotNumber, SupplierId = n.SupplierId, SupplierName = s.Name,
            QualityCheckId = n.QualityCheckId, QualityCheckNumber = q.Number, Source = n.Source, Description = n.Description,
            Containment = n.Containment, ProductDecision = n.ProductDecision, RootCause = n.RootCause,
            CorrectiveAction = n.CorrectiveAction, ActionOwnerEmployeeId = n.ActionOwnerEmployeeId, ActionOwnerName = e.FullName,
            DueDate = n.DueDate, EffectivenessCheckDate = n.EffectivenessCheckDate, Status = n.Status,
            EvidenceCount = n.Evidence.Count, CreatedAt = n.CreatedAt, IsVoided = n.IsVoided, VoidedReason = n.VoidedReason,
            Version = n.Version,
        };

    private static async Task<PagedResult<NonConformityDto>> List(
        AppDbContext db, IClock clock, [AsParameters] ListQuery list, NcStatus[]? status, NcSource[]? source,
        bool? overdue, bool? includeVoided, CancellationToken ct)
    {
        var q = Project(db);
        if (includeVoided != true)
            q = q.Where(x => !x.IsVoided);
        if (status is { Length: > 0 })
            q = q.Where(x => status.Contains(x.Status));
        if (source is { Length: > 0 })
            q = q.Where(x => source.Contains(x.Source));
        if (overdue == true)
        {
            var today = clock.Today;
            q = q.Where(x => x.Status != NcStatus.Zatvorena && x.DueDate != null && x.DueDate < today);
        }
        if (list.Search is { } s)
        {
            var like = PagingExtensions.Like(s);
            q = q.Where(x => EF.Functions.ILike(x.Number, like) || EF.Functions.ILike(x.Description, like)
                             || (x.WorkOrderNumber != null && EF.Functions.ILike(x.WorkOrderNumber, like))
                             || (x.LotNumber != null && EF.Functions.ILike(x.LotNumber, like))
                             || (x.SupplierName != null && EF.Functions.ILike(x.SupplierName, like)));
        }
        var sort = new SortMap<NonConformityDto>()
            .Add("number", x => x.Number).Add("status", x => x.Status).Add("source", x => x.Source).Add("dueDate", x => x.DueDate)
            .Default("number", descending: true);
        return await q.ToPagedAsync(list, sort, x => x, ct);
    }

    private static async Task<IResult> Get(Guid id, AppDbContext db, IClock clock, CancellationToken ct)
    {
        var dto = await Project(db).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (dto is null)
            return ApiResults.NotFound("Neusaglašenost ne postoji.");
        var nc = await db.NonConformities.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        var evidence = await (from ev in db.Set<NonConformityEvidence>()
                              where ev.NonConformityId == id
                              join f in db.FileAttachments on ev.FileId equals f.Id
                              orderby f.CreatedAt
                              select new EvidenceFileDto(f.Id, f.OriginalName, f.ContentType, f.SizeBytes)).ToListAsync(ct);
        return TypedResults.Ok(new NcDetailDto(dto, evidence, nc.IsVoided ? [] : NcTransitions.Options(nc, clock.Today)));
    }

    private static async Task<IResult> Prefill(Guid qualityCheckId, AppDbContext db, CancellationToken ct)
    {
        var c = await QualityCheckEndpoints.Project(db).SingleOrDefaultAsync(x => x.Id == qualityCheckId, ct);
        if (c is null)
            return ApiResults.NotFound("Kontrola ne postoji.");
        var supplier = c.MaterialLotId is { } lotId
            ? await (from l in db.MaterialLots where l.Id == lotId join s in db.Suppliers on l.SupplierId equals s.Id select new { s.Id, s.Name }).SingleAsync(ct)
            : null;
        var description = string.Join(" ", new[]
        {
            $"Kontrola {c.Number}: {c.Characteristic}.",
            c.AcceptanceCriterion is { } a ? $"Kriterij: {a}." : null,
            c.MeasuredResult is { } m ? $"Izmjereno: {m}." : null,
        }.Where(x => x is not null));
        return TypedResults.Ok(new NcPrefillDto(c.NonConformityId, c.NonConformityNumber, c.Id, c.Number, QualityCheck.SourceFor(c.Phase),
            c.WorkOrderId, c.WorkOrderNumber, c.MaterialLotId, c.LotNumber, supplier?.Id, supplier?.Name, description));
    }

    private static async Task<IResult> ListForWorkOrder(Guid id, AppDbContext db, CancellationToken ct)
    {
        if (!await db.WorkOrders.AnyAsync(w => w.Id == id, ct))
            return ApiResults.NotFound("Nalog ne postoji.");
        return TypedResults.Ok(await Project(db).Where(x => x.WorkOrderId == id).OrderByDescending(x => x.Number).ToListAsync(ct));
    }

    private static async Task<IResult> ListForLot(Guid id, AppDbContext db, CancellationToken ct)
    {
        if (!await db.MaterialLots.AnyAsync(l => l.Id == id, ct))
            return ApiResults.NotFound("Lot ne postoji.");
        return TypedResults.Ok(await Project(db).Where(x => x.MaterialLotId == id).OrderByDescending(x => x.Number).ToListAsync(ct));
    }

    /// <summary>Reference checks shared by create and update; returns a field error or null.</summary>
    private static async Task<IResult?> CheckReferences(SaveNcRequest req, AppDbContext db, CancellationToken ct)
    {
        if (req.WorkOrderId is { } wo && !await db.WorkOrders.AnyAsync(x => x.Id == wo, ct))
            return ApiResults.Validation("workOrderId", "Nalog ne postoji.");
        if (req.MaterialLotId is { } lot && !await db.MaterialLots.AnyAsync(x => x.Id == lot, ct))
            return ApiResults.Validation("materialLotId", "Lot ne postoji.");
        if (req.SupplierId is { } sup && !await db.Suppliers.AnyAsync(x => x.Id == sup, ct))
            return ApiResults.Validation("supplierId", "Dobavljač ne postoji.");
        if (req.ActionOwnerEmployeeId is { } emp && !await db.Employees.AnyAsync(x => x.Id == emp, ct))
            return ApiResults.Validation("actionOwnerEmployeeId", "Zaposlenik ne postoji.");
        return null;
    }

    private static async Task<IResult> Create(SaveNcRequest req, AppDbContext db, DocumentNumberService numbers, IClock clock, CancellationToken ct)
    {
        if (await CheckReferences(req, db, ct) is { } error)
            return error;
        if (req.QualityCheckId is { } qc)
        {
            var check = await db.QualityChecks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == qc, ct);
            if (check is null)
                return ApiResults.Validation("qualityCheckId", "Kontrola ne postoji.");
            if (check.IsVoided || check.Result != InspectionResult.Neuskladjeno)
                return ApiResults.Validation("qualityCheckId", "NC se veže samo za neponištenu kontrolu sa rezultatom Neusklađeno.");
            if (await db.NonConformities.Where(n => n.QualityCheckId == qc && !n.IsVoided).Select(n => n.Number).FirstOrDefaultAsync(ct) is { } existing)
                return ApiResults.Conflict($"Za kontrolu {check.Number} već postoji {existing}.");
        }
        var fileIds = (req.EvidenceFileIds ?? []).Distinct().ToList();
        if (fileIds.Count > 0 && await db.FileAttachments.CountAsync(f => fileIds.Contains(f.Id), ct) != fileIds.Count)
            return ApiResults.Validation("evidenceFileIds", "Neki od fajlova ne postoje.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var nc = new NonConformity
        {
            Number = await numbers.NextAsync(DocumentNumberKind.NonConformity, clock.Today, ct),
            QualityCheckId = req.QualityCheckId,
            Description = req.Description.Trim(),
        };
        Apply(nc, req);
        foreach (var f in fileIds)
            nc.Evidence.Add(new NonConformityEvidence { NonConformityId = nc.Id, FileId = f });
        db.NonConformities.Add(nc);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ApiResults.Conflict("Za ovu kontrolu je upravo otvorena NC. Osvježi stranicu.");
        }
        await tx.CommitAsync(ct);
        return TypedResults.Created($"/api/non-conformities/{nc.Id}", await Project(db).SingleAsync(x => x.Id == nc.Id, ct));
    }

    private static async Task<IResult> Update(Guid id, SaveNcRequest req, AppDbContext db, CancellationToken ct)
    {
        var nc = await db.NonConformities.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (nc is null)
            return ApiResults.NotFound("Neusaglašenost ne postoji.");
        if (nc.IsVoided)
            return ApiResults.Conflict($"{nc.Number} je poništena i ne može se mijenjati.");
        if (nc.IsReadOnly)
            return ApiResults.Conflict($"{nc.Number} je zatvorena. Za izmjenu je vrati u \"Provjera efektivnosti\".");
        if (await CheckReferences(req, db, ct) is { } error)
            return error;
        db.Entry(nc).Property(x => x.Version).OriginalValue = req.Version!.Value;
        nc.Description = req.Description.Trim();
        Apply(nc, req);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Project(db).SingleAsync(x => x.Id == id, ct));
    }

    private static async Task<IResult> ChangeStatus(Guid id, NcStatusRequest req, AppDbContext db, IClock clock, CancellationToken ct)
    {
        var nc = await db.NonConformities.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (nc is null)
            return ApiResults.NotFound("Neusaglašenost ne postoji.");
        db.Entry(nc).Property(x => x.Version).OriginalValue = req.Version!.Value;
        nc.MoveTo(req.Status!.Value, clock.Today);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Project(db).SingleAsync(x => x.Id == id, ct));
    }

    private static async Task<IResult> AddEvidence(Guid id, FileIdRequest req, AppDbContext db, CancellationToken ct)
    {
        var nc = await db.NonConformities.Include(x => x.Evidence).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (nc is null)
            return ApiResults.NotFound("Neusaglašenost ne postoji.");
        if (!await db.FileAttachments.AnyAsync(f => f.Id == req.FileId, ct))
            return ApiResults.Validation("fileId", "Fajl ne postoji.");
        if (nc.Evidence.All(e => e.FileId != req.FileId))
            nc.Evidence.Add(new NonConformityEvidence { NonConformityId = id, FileId = req.FileId!.Value });
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> RemoveEvidence(Guid id, Guid fileId, AppDbContext db, CancellationToken ct)
    {
        var nc = await db.NonConformities.Include(x => x.Evidence).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (nc is null)
            return ApiResults.NotFound("Neusaglašenost ne postoji.");
        nc.Evidence.RemoveAll(e => e.FileId == fileId);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> Void(Guid id, VoidRequest req, AppDbContext db, CancellationToken ct)
    {
        var nc = await db.NonConformities.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (nc is null)
            return ApiResults.NotFound("Neusaglašenost ne postoji.");
        db.Entry(nc).Property(x => x.Version).OriginalValue = req.Version!.Value;
        nc.Void(req.Reason);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Project(db).SingleAsync(x => x.Id == id, ct));
    }

    private static void Apply(NonConformity nc, SaveNcRequest req)
    {
        nc.WorkOrderId = req.WorkOrderId;
        nc.MaterialLotId = req.MaterialLotId;
        nc.SupplierId = req.SupplierId;
        nc.Source = req.Source!.Value;
        nc.Containment = Clean(req.Containment);
        nc.ProductDecision = req.ProductDecision!.Value;
        nc.RootCause = Clean(req.RootCause);
        nc.CorrectiveAction = Clean(req.CorrectiveAction);
        nc.ActionOwnerEmployeeId = req.ActionOwnerEmployeeId;
        nc.DueDate = req.DueDate;
        nc.EffectivenessCheckDate = req.EffectivenessCheckDate;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
