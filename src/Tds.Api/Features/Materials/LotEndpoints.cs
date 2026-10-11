using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Materials;

public sealed record LotDto
{
    public required Guid Id { get; init; }
    public required string LotNumber { get; init; }
    public required Guid MaterialItemId { get; init; }
    public required string ItemName { get; init; }
    public string? ItemGrade { get; init; }
    public decimal? ItemThicknessMm { get; init; }
    public required Unit Unit { get; init; }
    public required Guid SupplierId { get; init; }
    public required string SupplierName { get; init; }
    public required DateOnly ReceivedDate { get; init; }
    public string? DimensionsDescription { get; init; }
    /// <summary>Null = unknown (lot imported from Notion).</summary>
    public decimal? ReceivedQty { get; init; }
    /// <summary>IZV: Σ non-voided movements.</summary>
    public required decimal OnHand { get; init; }
    public required ReceiptDecision ReceiptDecision { get; init; }
    public Guid? InspectedByEmployeeId { get; init; }
    public string? InspectedByName { get; init; }
    public string? StorageLocation { get; init; }
    public Guid? CertificateFileId { get; init; }
    public string? CertificateFileName { get; init; }
    public required bool IsVoided { get; init; }
    public string? VoidedReason { get; init; }
    public required uint Version { get; init; }
}

public sealed record ReceiveLotRequest(
    string LotNumber, Guid MaterialItemId, Guid SupplierId, DateOnly ReceivedDate, decimal ReceivedQty,
    string? DimensionsDescription, string? StorageLocation);

public sealed record UpdateLotRequest(string LotNumber, Guid SupplierId, string? DimensionsDescription, string? StorageLocation, uint Version);

public sealed record DecideLotRequest(ReceiptDecision Decision, Guid? InspectedByEmployeeId, string? Reason, uint Version);

public sealed record VoidRequest(string Reason, uint Version);

public sealed record SetFileRequest(Guid? FileId, uint Version);

public sealed class ReceiveLotValidator : AbstractValidator<ReceiveLotRequest>
{
    public ReceiveLotValidator()
    {
        RuleFor(x => x.LotNumber).NotEmpty().WithMessage("Unesi oznaku lota / heat broj sa certifikata.")
            .MaximumLength(MaterialLot.LotNumberMax).WithMessage($"Oznaka lota može imati najviše {MaterialLot.LotNumberMax} znakova.");
        RuleFor(x => x.MaterialItemId).NotEmpty().WithMessage("Odaberi artikal.");
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("Odaberi dobavljača.");
        RuleFor(x => x.ReceivedQty).GreaterThan(0).WithMessage("Primljena količina mora biti veća od 0.")
            .Must(q => decimal.Round(q, 3) == q).WithMessage("Količina može imati najviše 3 decimale.")
            .LessThan(1_000_000_000m).WithMessage("Količina je prevelika.");
        RuleFor(x => x.DimensionsDescription).MaximumLength(MaterialLot.DimensionsMax).WithMessage($"Najviše {MaterialLot.DimensionsMax} znakova.");
        RuleFor(x => x.StorageLocation).MaximumLength(MaterialLot.StorageLocationMax).WithMessage($"Najviše {MaterialLot.StorageLocationMax} znakova.");
    }
}

public sealed class UpdateLotValidator : AbstractValidator<UpdateLotRequest>
{
    public UpdateLotValidator()
    {
        RuleFor(x => x.LotNumber).NotEmpty().WithMessage("Unesi oznaku lota / heat broj sa certifikata.")
            .MaximumLength(MaterialLot.LotNumberMax).WithMessage($"Oznaka lota može imati najviše {MaterialLot.LotNumberMax} znakova.");
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("Odaberi dobavljača.");
        RuleFor(x => x.DimensionsDescription).MaximumLength(MaterialLot.DimensionsMax).WithMessage($"Najviše {MaterialLot.DimensionsMax} znakova.");
        RuleFor(x => x.StorageLocation).MaximumLength(MaterialLot.StorageLocationMax).WithMessage($"Najviše {MaterialLot.StorageLocationMax} znakova.");
    }
}

public sealed class DecideLotValidator : AbstractValidator<DecideLotRequest>
{
    public DecideLotValidator()
    {
        RuleFor(x => x.Decision).IsInEnum().WithMessage("Odaberi odluku prijema.");
        RuleFor(x => x.Reason).MaximumLength(StockMovement.ReasonMax).WithMessage($"Razlog može imati najviše {StockMovement.ReasonMax} znakova.");
    }
}

public sealed class VoidRequestValidator : AbstractValidator<VoidRequest>
{
    public VoidRequestValidator() =>
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Razlog poništavanja je obavezan.")
            .MaximumLength(VoidableEntity.VoidedReasonMax).WithMessage($"Razlog može imati najviše {VoidableEntity.VoidedReasonMax} znakova.");
}

public static class LotEndpoints
{
    public static void MapLotEndpoints(this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup("/api/lots").RequireAuthorization();
        read.MapGet("/", List);
        read.MapGet("/{id:guid}", Get);

        var write = app.MapGroup("/api/lots").RequireAuthorization(Policies.KontrolorOrAdmin);
        write.MapPost("/", Receive).Validate<ReceiveLotRequest>();
        write.MapPut("/{id:guid}", Update).Validate<UpdateLotRequest>();
        write.MapPost("/{id:guid}/decision", Decide).Validate<DecideLotRequest>();
        write.MapPost("/{id:guid}/certificate", SetCertificate);

        app.MapPost("/api/lots/{id:guid}/void", Void).Validate<VoidRequest>().RequireAuthorization(Policies.Admin);
    }

    private static readonly SortMap<LotDto> Sort = new SortMap<LotDto>()
        .Add("lotNumber", x => x.LotNumber)
        .Add("itemName", x => x.ItemName)
        .Add("supplierName", x => x.SupplierName)
        .Add("receivedDate", x => x.ReceivedDate)
        .Add("onHand", x => x.OnHand)
        .Add("receiptDecision", x => x.ReceiptDecision)
        .Default("receivedDate", descending: true);

    public static IQueryable<LotDto> Project(AppDbContext db) =>
        from l in db.MaterialLots
        join i in db.MaterialItems on l.MaterialItemId equals i.Id
        join s in db.Suppliers on l.SupplierId equals s.Id
        join e in db.Employees on l.InspectedByEmployeeId equals e.Id into es
        from e in es.DefaultIfEmpty()
        join f in db.FileAttachments on l.CertificateFileId equals f.Id into fs
        from f in fs.DefaultIfEmpty()
        select new LotDto
        {
            Id = l.Id, LotNumber = l.LotNumber, MaterialItemId = l.MaterialItemId,
            ItemName = i.Name, ItemGrade = i.Grade, ItemThicknessMm = i.ThicknessMm, Unit = i.Unit,
            SupplierId = l.SupplierId, SupplierName = s.Name, ReceivedDate = l.ReceivedDate,
            DimensionsDescription = l.DimensionsDescription, ReceivedQty = l.ReceivedQty,
            OnHand = db.StockMovements.Where(m => m.MaterialLotId == l.Id && !m.IsVoided).Sum(m => (decimal?)m.Quantity) ?? 0m,
            ReceiptDecision = l.ReceiptDecision, InspectedByEmployeeId = l.InspectedByEmployeeId, InspectedByName = e.FullName,
            StorageLocation = l.StorageLocation, CertificateFileId = l.CertificateFileId, CertificateFileName = f.OriginalName,
            IsVoided = l.IsVoided, VoidedReason = l.VoidedReason, Version = l.Version,
        };

    private static async Task<PagedResult<LotDto>> List(
        AppDbContext db, [AsParameters] ListQuery list, ReceiptDecision[]? receiptDecision, Guid? materialItemId,
        bool? includeVoided, CancellationToken ct)
    {
        var q = Project(db);
        if (includeVoided != true)
            q = q.Where(x => !x.IsVoided);
        if (list.Search is { } s)
            q = q.Where(x => EF.Functions.ILike(x.LotNumber, PagingExtensions.Like(s))
                             || EF.Functions.ILike(x.ItemName, PagingExtensions.Like(s))
                             || (x.ItemGrade != null && EF.Functions.ILike(x.ItemGrade, PagingExtensions.Like(s)))
                             || EF.Functions.ILike(x.SupplierName, PagingExtensions.Like(s)));
        if (receiptDecision is { Length: > 0 })
            q = q.Where(x => receiptDecision.Contains(x.ReceiptDecision));
        if (materialItemId is { } item)
            q = q.Where(x => x.MaterialItemId == item);
        return await q.ToPagedAsync(list, Sort, x => x, ct);
    }

    private static async Task<IResult> Get(Guid id, AppDbContext db, CancellationToken ct) =>
        await Project(db).SingleOrDefaultAsync(x => x.Id == id, ct) is { } dto
            ? TypedResults.Ok(dto)
            : ApiResults.NotFound("Lot ne postoji.");

    private static async Task<IResult> Receive(ReceiveLotRequest req, AppDbContext db, IClock clock, CancellationToken ct)
    {
        var lotNumber = req.LotNumber.Trim();
        if (await db.MaterialLots.AnyAsync(l => l.LotNumber == lotNumber, ct))
            return ApiResults.Validation("lotNumber", $"Lot {lotNumber} već postoji.");
        var item = await db.MaterialItems.SingleOrDefaultAsync(i => i.Id == req.MaterialItemId, ct);
        if (item is null)
            return ApiResults.Validation("materialItemId", "Artikal ne postoji.");
        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == req.SupplierId, ct);
        if (supplier is null)
            return ApiResults.Validation("supplierId", "Dobavljač ne postoji.");

        var (lot, receipt) = MaterialLot.Receive(lotNumber, item.Id, supplier.Id, supplier.ApprovalStatus,
            req.ReceivedDate, req.ReceivedQty, clock.Today);
        lot.DimensionsDescription = Clean(req.DimensionsDescription);
        lot.StorageLocation = Clean(req.StorageLocation);

        // One SaveChanges = one transaction: the lot never exists without its Prijem (DOMAIN §2.2).
        db.MaterialLots.Add(lot);
        db.StockMovements.Add(receipt);
        await db.SaveChangesAsync(ct);

        var dto = await Project(db).SingleAsync(x => x.Id == lot.Id, ct);
        return TypedResults.Created($"/api/lots/{lot.Id}", new WithWarnings<LotDto>(dto, lot.Warnings(supplier.Name, supplier.ApprovalStatus)));
    }

    private static async Task<IResult> Update(Guid id, UpdateLotRequest req, AppDbContext db, CancellationToken ct)
    {
        var lot = await db.MaterialLots.SingleOrDefaultAsync(l => l.Id == id, ct);
        if (lot is null)
            return ApiResults.NotFound("Lot ne postoji.");
        if (lot.IsVoided)
            return ApiResults.Conflict($"Lot {lot.LotNumber} je poništen i ne može se mijenjati.");
        var lotNumber = req.LotNumber.Trim();
        if (await db.MaterialLots.AnyAsync(l => l.LotNumber == lotNumber && l.Id != id, ct))
            return ApiResults.Validation("lotNumber", $"Lot {lotNumber} već postoji.");
        if (req.SupplierId != lot.SupplierId)
        {
            var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == req.SupplierId, ct);
            if (supplier is null)
                return ApiResults.Validation("supplierId", "Dobavljač ne postoji.");
            if (supplier.ApprovalStatus == SupplierApprovalStatus.Blokiran)
                return ApiResults.Validation("supplierId", "Dobavljač je blokiran.");
        }

        db.Entry(lot).Property(x => x.Version).OriginalValue = req.Version;
        lot.LotNumber = lotNumber;
        lot.SupplierId = req.SupplierId;
        lot.DimensionsDescription = Clean(req.DimensionsDescription);
        lot.StorageLocation = Clean(req.StorageLocation);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Project(db).SingleAsync(x => x.Id == id, ct));
    }

    private static async Task<IResult> Decide(Guid id, DecideLotRequest req, AppDbContext db, IClock clock, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockLotAsync(id, ct);
        var lot = await db.MaterialLots.SingleOrDefaultAsync(l => l.Id == id, ct);
        if (lot is null)
            return ApiResults.NotFound("Lot ne postoji.");
        if (req.InspectedByEmployeeId is { } emp && !await db.Employees.AnyAsync(e => e.Id == emp, ct))
            return ApiResults.Validation("inspectedByEmployeeId", "Zaposlenik ne postoji.");

        db.Entry(lot).Property(x => x.Version).OriginalValue = req.Version;
        var writeOff = lot.Decide(req.Decision, req.InspectedByEmployeeId, req.Reason, await db.OnHandAsync(id, ct), clock.Today);
        if (writeOff is not null)
            db.StockMovements.Add(writeOff);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var supplier = await db.Suppliers.SingleAsync(s => s.Id == lot.SupplierId, ct);
        var dto = await Project(db).SingleAsync(x => x.Id == id, ct);
        return TypedResults.Ok(new WithWarnings<LotDto>(dto, lot.Warnings(supplier.Name, supplier.ApprovalStatus)));
    }

    /// <summary>Attaches (or replaces) the material certificate. The previous file stays stored (no deletes).</summary>
    private static async Task<IResult> SetCertificate(Guid id, SetFileRequest req, AppDbContext db, CancellationToken ct)
    {
        var lot = await db.MaterialLots.SingleOrDefaultAsync(l => l.Id == id, ct);
        if (lot is null)
            return ApiResults.NotFound("Lot ne postoji.");
        if (lot.IsVoided)
            return ApiResults.Conflict($"Lot {lot.LotNumber} je poništen i ne može se mijenjati.");
        if (req.FileId is { } f && !await db.FileAttachments.AnyAsync(x => x.Id == f, ct))
            return ApiResults.Validation("fileId", "Fajl ne postoji.");
        db.Entry(lot).Property(x => x.Version).OriginalValue = req.Version;
        lot.CertificateFileId = req.FileId;
        await db.SaveChangesAsync(ct);

        var supplier = await db.Suppliers.SingleAsync(s => s.Id == lot.SupplierId, ct);
        return TypedResults.Ok(new WithWarnings<LotDto>(await Project(db).SingleAsync(x => x.Id == id, ct), lot.Warnings(supplier.Name, supplier.ApprovalStatus)));
    }

    /// <summary>
    /// Admin only (DOMAIN §2.5a): allowed when the lot has no non-voided movement except Prijem and is not
    /// linked to any work order. Voids the Prijem too.
    /// </summary>
    private static async Task<IResult> Void(Guid id, VoidRequest req, AppDbContext db, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockLotAsync(id, ct);
        var lot = await db.MaterialLots.SingleOrDefaultAsync(l => l.Id == id, ct);
        if (lot is null)
            return ApiResults.NotFound("Lot ne postoji.");

        var movements = await db.StockMovements.Where(m => m.MaterialLotId == id && !m.IsVoided).ToListAsync(ct);
        if (movements.Any(m => m.Type != StockMovementType.Prijem))
            return ApiResults.Conflict($"Lot {lot.LotNumber} ima pokrete zalihe osim prijema i ne može se poništiti.");
        if (await LotLinks.IsLinkedToWorkOrderAsync(db, id, ct))
            return ApiResults.Conflict($"Lot {lot.LotNumber} je vezan za nalog i ne može se poništiti.");

        db.Entry(lot).Property(x => x.Version).OriginalValue = req.Version;
        lot.Void(req.Reason);
        foreach (var m in movements)
            m.Void(req.Reason);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return TypedResults.Ok(await Project(db).SingleAsync(x => x.Id == id, ct));
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public static class LotLinks
{
    public static Task<bool> IsLinkedToWorkOrderAsync(AppDbContext db, Guid lotId, CancellationToken ct) =>
        db.WorkOrderMaterials.AnyAsync(l => l.MaterialLotId == lotId, ct);
}
