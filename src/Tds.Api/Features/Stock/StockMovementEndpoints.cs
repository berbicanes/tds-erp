using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Stock;

public sealed record StockMovementDto
{
    public required Guid Id { get; init; }
    public required StockMovementType Type { get; init; }
    public required decimal Quantity { get; init; }
    public required DateOnly Date { get; init; }
    public string? Reason { get; init; }
    public Guid? ProductionEntryId { get; init; }
    public Guid? WorkOrderId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    /// <summary>null = system action ("Sistem" in the UI).</summary>
    public string? CreatedByName { get; init; }
    public required bool IsVoided { get; init; }
    public string? VoidedReason { get; init; }
}

public sealed record ManualMovementRequest(StockMovementType? Type, decimal? Quantity, DateOnly? Date, string? Reason);

public sealed class ManualMovementValidator : AbstractValidator<ManualMovementRequest>
{
    public ManualMovementValidator()
    {
        RuleFor(x => x.Type).Required("Odaberi vrstu pokreta.").Must(t => t is StockMovementType.Korekcija or StockMovementType.Otpis)
            .WithMessage("Ručno se unose samo korekcija i otpis.");
        RuleFor(x => x.Quantity).Required("Unesi količinu.").Must(q => q is null || decimal.Round(q.Value, 3) == q).WithMessage("Količina može imati najviše 3 decimale.");
        RuleFor(x => x.Date).Required("Unesi datum pokreta.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Razlog je obavezan.")
            .MaximumLength(StockMovement.ReasonMax).WithMessage($"Razlog može imati najviše {StockMovement.ReasonMax} znakova.");
    }
}

public static class StockMovementEndpoints
{
    public static void MapStockMovementEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/lots/{id:guid}/movements", List).RequireAuthorization();
        app.MapPost("/api/lots/{id:guid}/movements", Create).Validate<ManualMovementRequest>().RequireAuthorization(Policies.KontrolorOrAdmin);
    }

    private static async Task<IResult> List(Guid id, AppDbContext db, CancellationToken ct)
    {
        if (!await db.MaterialLots.AnyAsync(l => l.Id == id, ct))
            return ApiResults.NotFound("Lot ne postoji.");
        var rows = await (
            from m in db.StockMovements
            where m.MaterialLotId == id
            join u in db.Users on m.CreatedBy equals u.Id into us
            from u in us.DefaultIfEmpty()
            orderby m.Date descending, m.CreatedAt descending
            select new StockMovementDto
            {
                Id = m.Id, Type = m.Type, Quantity = m.Quantity, Date = m.Date, Reason = m.Reason,
                ProductionEntryId = m.ProductionEntryId, WorkOrderId = m.WorkOrderId, CreatedAt = m.CreatedAt,
                CreatedByName = u.UserName, IsVoided = m.IsVoided, VoidedReason = m.VoidedReason,
            }).ToListAsync(ct);
        return TypedResults.Ok(rows);
    }

    private static async Task<IResult> Create(Guid id, ManualMovementRequest req, AppDbContext db, IClock clock, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockLotAsync(id, ct);
        var lot = await db.MaterialLots.SingleOrDefaultAsync(l => l.Id == id, ct);
        if (lot is null)
            return ApiResults.NotFound("Lot ne postoji.");

        var onHand = await db.OnHandAsync(id, ct);
        var known = await db.StockKnownAsync(id, ct);
        var movement = req.Type == StockMovementType.Otpis
            ? Domain.Stock.WriteOff(lot, req.Quantity!.Value, req.Reason, req.Date!.Value, onHand, clock.Today, known)
            : Domain.Stock.Correction(lot, req.Quantity!.Value, req.Reason, req.Date!.Value, onHand, clock.Today, known);
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return TypedResults.Created($"/api/lots/{id}/movements", new { id = movement.Id, onHand = onHand + movement.Quantity });
    }
}
