using FluentValidation;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.WorkOrders;

public sealed record ChangeStatusRequest(WorkOrderStatus? To, string? Reason, uint? Version);

public sealed class ChangeStatusValidator : AbstractValidator<ChangeStatusRequest>
{
    public ChangeStatusValidator()
    {
        RuleFor(x => x.To).Required("Odaberi novi status.").IsInEnum().WithMessage("Odaberi novi status.");
        RuleFor(x => x.Reason).MaximumLength(300).WithMessage("Razlog može imati najviše 300 znakova.");
        RuleFor(x => x.Version).Required(ValidationExtensions.VersionMissing);
    }
}

public static class WorkOrderStatusEndpoints
{
    public static void MapWorkOrderStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/work-orders/{id:guid}").RequireAuthorization(Policies.KontrolorOrAdmin);
        g.MapGet("/transitions", Options);
        g.MapPost("/status", Change).Validate<ChangeStatusRequest>();
    }

    private static GateFacts Facts(WorkOrderDto w) =>
        new(w.QualityApproval, w.LaserFirstPieceApproved, w.PressFirstPieceApproved, w.FinalInspectionPassed, w.ReadyForShipment);

    /// <summary>UI "Promijeni status" (UI §6): only table targets; failed gates come with the reason.</summary>
    private static async Task<IResult> Options(Guid id, AppDbContext db, IClock clock, ClaimsPrincipal user, CancellationToken ct)
    {
        var w = await WorkOrderEndpoints.GetDtoAsync(db, id, clock.Today, ct);
        return w is null
            ? ApiResults.NotFound("Nalog ne postoji.")
            : TypedResults.Ok(WorkOrderTransitions.Options(w.Status, Facts(w), user.IsInRole(Roles.Admin)));
    }

    private static async Task<IResult> Change(
        Guid id, ChangeStatusRequest req, AppDbContext db, IClock clock, ClaimsPrincipal user, AuditContext audit, CancellationToken ct)
    {
        var dto = await WorkOrderEndpoints.GetDtoAsync(db, id, clock.Today, ct);
        if (dto is null)
            return ApiResults.NotFound("Nalog ne postoji.");

        var wo = await db.WorkOrders.SingleAsync(w => w.Id == id, ct);
        db.Entry(wo).Property(x => x.Version).OriginalValue = req.Version!.Value;
        var isAdmin = user.IsInRole(Roles.Admin);
        var check = WorkOrderTransitions.Check(wo.Status, req.To!.Value, Facts(dto), isAdmin);
        WorkOrderTransitions.Apply(wo, req.To!.Value, Facts(dto), isAdmin, req.Reason);

        // The reason (gate override, reopening) goes into this change's AuditLog entry (DOMAIN §4).
        var overridden = !check.Allowed && check.CanOverride;
        if (overridden || dto.Status == WorkOrderStatus.Otkazano)
            audit.Reason = overridden ? $"Preskočen uslov \"{check.Reason}\": {req.Reason!.Trim()}" : req.Reason!.Trim();

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await WorkOrderEndpoints.GetDtoAsync(db, id, clock.Today, ct));
    }
}
