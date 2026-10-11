using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Stock;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.WorkOrders;

public sealed record WorkOrderMaterialDto
{
    public required Guid Id { get; init; }
    public required Guid MaterialLotId { get; init; }
    public required string LotNumber { get; init; }
    public required string ItemName { get; init; }
    public string? ItemGrade { get; init; }
    public decimal? ItemThicknessMm { get; init; }
    public required Unit Unit { get; init; }
    public required ReceiptDecision ReceiptDecision { get; init; }
    public required decimal ReservedQty { get; init; }
    /// <summary>Issued from this lot for this order (positive).</summary>
    public required decimal ConsumedQty { get; init; }
    public required decimal EffectiveReservation { get; init; }
    /// <summary>Null = unknown (DOMAIN §2.6).</summary>
    public decimal? LotOnHand { get; init; }
    /// <summary>Null = unknown (DOMAIN §2.6).</summary>
    public decimal? LotAvailable { get; init; }
    public required uint Version { get; init; }
}

public sealed record LinkLotRequest(Guid? MaterialLotId, decimal? ReservedQty);
public sealed record SetReservationRequest(decimal? ReservedQty, uint? Version);

public sealed class LinkLotValidator : AbstractValidator<LinkLotRequest>
{
    public LinkLotValidator()
    {
        RuleFor(x => x.MaterialLotId).NotEmpty().WithMessage("Odaberi lot.");
        RuleFor(x => x.ReservedQty).Required("Unesi rezervaciju (0 = samo veza bez rezervacije).").GreaterThanOrEqualTo(0).WithMessage("Rezervacija ne može biti negativna.")
            .Must(q => q is null || decimal.Round(q.Value, 3) == q).WithMessage("Količina može imati najviše 3 decimale.");
    }
}

public sealed class SetReservationValidator : AbstractValidator<SetReservationRequest>
{
    public SetReservationValidator()
    {
        RuleFor(x => x.ReservedQty).Required("Unesi rezervaciju (0 = samo veza bez rezervacije).").GreaterThanOrEqualTo(0).WithMessage("Rezervacija ne može biti negativna.")
            .Must(q => q is null || decimal.Round(q.Value, 3) == q).WithMessage("Količina može imati najviše 3 decimale.");
        RuleFor(x => x.Version).Required(ValidationExtensions.VersionMissing);
    }
}

public static class WorkOrderMaterialEndpoints
{
    public static void MapWorkOrderMaterialEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/work-orders/{id:guid}/materials", List).RequireAuthorization();
        var write = app.MapGroup("/api/work-orders/{id:guid}/materials").RequireAuthorization(Policies.KontrolorOrAdmin);
        write.MapPost("/", Link).Validate<LinkLotRequest>();
        write.MapPut("/{linkId:guid}", SetReservation).Validate<SetReservationRequest>();
    }

    public static async Task<List<WorkOrderMaterialDto>> ListAsync(AppDbContext db, Guid workOrderId, CancellationToken ct)
    {
        var rows = await (
            from u in Reservations.Usage(db)
            where u.WorkOrderId == workOrderId
            join wm in db.WorkOrderMaterials on u.LinkId equals wm.Id
            join l in db.MaterialLots on u.MaterialLotId equals l.Id
            join i in db.MaterialItems on l.MaterialItemId equals i.Id
            orderby l.LotNumber
            select new { u, wm.Version, l.LotNumber, i.Name, i.Grade, i.ThicknessMm, i.Unit, l.ReceiptDecision }).ToListAsync(ct);
        var stock = (await StockQueries.LotStocksAsync(db, null, ct)).ToDictionary(s => s.LotId);
        return rows.Select(r => new WorkOrderMaterialDto
        {
            Id = r.u.LinkId, MaterialLotId = r.u.MaterialLotId, LotNumber = r.LotNumber, ItemName = r.Name, ItemGrade = r.Grade,
            ItemThicknessMm = r.ThicknessMm, Unit = r.Unit, ReceiptDecision = r.ReceiptDecision, ReservedQty = r.u.ReservedQty,
            ConsumedQty = r.u.IssuedQty, EffectiveReservation = r.u.Effective,
            LotOnHand = stock.TryGetValue(r.u.MaterialLotId, out var s) ? s.KnownOnHand : null,
            LotAvailable = stock.TryGetValue(r.u.MaterialLotId, out var s2) ? s2.KnownAvailable : null,
            Version = r.Version,
        }).ToList();
    }

    private static async Task<IResult> List(Guid id, AppDbContext db, CancellationToken ct) =>
        await db.WorkOrders.AnyAsync(w => w.Id == id, ct)
            ? TypedResults.Ok(await ListAsync(db, id, ct))
            : ApiResults.NotFound("Nalog ne postoji.");

    private static async Task<IResult> Link(Guid id, LinkLotRequest req, AppDbContext db, CancellationToken ct)
    {
        var wo = await db.WorkOrders.SingleOrDefaultAsync(w => w.Id == id, ct);
        if (wo is null)
            return ApiResults.NotFound("Nalog ne postoji.");
        if (wo.IsReadOnly)
            return ApiResults.Conflict($"Nalog {wo.Number} ima status {DisplayNames.Of(wo.Status)}; materijal se više ne veže.");
        var lot = await db.MaterialLots.SingleOrDefaultAsync(l => l.Id == req.MaterialLotId, ct);
        if (lot is null)
            return ApiResults.Validation("materialLotId", "Lot ne postoji.");
        if (await db.WorkOrderMaterials.AnyAsync(x => x.WorkOrderId == id && x.MaterialLotId == lot.Id, ct))
            return ApiResults.Validation("materialLotId", $"Lot {lot.LotNumber} je već vezan za ovaj nalog. Promijeni rezervaciju u listi.");

        var availableBefore = await AvailableAsync(db, lot.Id, ct);
        var link = WorkOrderMaterial.Link(id, lot, req.ReservedQty!.Value);
        db.WorkOrderMaterials.Add(link);
        await db.SaveChangesAsync(ct);

        var dto = (await ListAsync(db, id, ct)).Single(x => x.Id == link.Id);
        return TypedResults.Created($"/api/work-orders/{id}/materials/{link.Id}",
            new WithWarnings<WorkOrderMaterialDto>(dto, Warnings(lot, req.ReservedQty!.Value, availableBefore, dto.Unit)));
    }

    private static async Task<IResult> SetReservation(Guid id, Guid linkId, SetReservationRequest req, AppDbContext db, CancellationToken ct)
    {
        var link = await db.WorkOrderMaterials.SingleOrDefaultAsync(x => x.Id == linkId && x.WorkOrderId == id, ct);
        if (link is null)
            return ApiResults.NotFound("Veza lota i naloga ne postoji.");
        var wo = await db.WorkOrders.SingleAsync(w => w.Id == id, ct);
        if (wo.IsReadOnly)
            return ApiResults.Conflict($"Nalog {wo.Number} ima status {DisplayNames.Of(wo.Status)}; rezervacija se više ne mijenja.");
        var lot = await db.MaterialLots.SingleAsync(l => l.Id == link.MaterialLotId, ct);

        var current = (await ListAsync(db, id, ct)).Single(x => x.Id == linkId);
        var availableWithoutThis = current.LotAvailable + current.EffectiveReservation;
        db.Entry(link).Property(x => x.Version).OriginalValue = req.Version!.Value;
        link.SetReservation(lot, req.ReservedQty!.Value);
        await db.SaveChangesAsync(ct);

        var dto = (await ListAsync(db, id, ct)).Single(x => x.Id == linkId);
        var newEffective = StockLevels.EffectiveReservation(req.ReservedQty!.Value, dto.ConsumedQty);
        return TypedResults.Ok(new WithWarnings<WorkOrderMaterialDto>(dto, Warnings(lot, newEffective, availableWithoutThis, dto.Unit)));
    }

    private static async Task<decimal?> AvailableAsync(AppDbContext db, Guid lotId, CancellationToken ct) =>
        (await StockQueries.LotStocksAsync(db, null, ct, lotId)).SingleOrDefault()?.KnownAvailable;

    /// <summary>Reserving more than is available is allowed but shown (DOMAIN §2.3).</summary>
    private static IReadOnlyList<string> Warnings(MaterialLot lot, decimal reserving, decimal? availableBefore, Unit unit) =>
        availableBefore is not { } available
            ? reserving > 0 ? [$"Stanje lota {lot.LotNumber} nije poznato, pa se rezervacija ne može provjeriti."] : []
            : reserving > available
                ? [$"Rezervacija je veća od raspoloživog na lotu {lot.LotNumber} (raspoloživo {Domain.Stock.Format(available)} {unit})."]
                : [];
}
