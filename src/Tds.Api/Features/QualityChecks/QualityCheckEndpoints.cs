using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Materials;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.QualityChecks;

public sealed record QualityCheckDto
{
    public required Guid Id { get; init; }
    public required string Number { get; init; }
    public Guid? WorkOrderId { get; init; }
    public required InspectionPhase Phase { get; init; }
    public required string Characteristic { get; init; }
    public string? AcceptanceCriterion { get; init; }
    public string? MeasuredResult { get; init; }
    public int? SampleSize { get; init; }
    public required InspectionResult Result { get; init; }
    public required Guid InspectedByEmployeeId { get; init; }
    public required string InspectedByName { get; init; }
    public required DateOnly InspectionDate { get; init; }
    public required bool IsVoided { get; init; }
    public string? VoidedReason { get; init; }
    public required uint Version { get; init; }
}

public sealed record CreateQualityCheckRequest(
    Guid WorkOrderId, InspectionPhase Phase, string Characteristic, string? AcceptanceCriterion, string? MeasuredResult,
    int? SampleSize, InspectionResult Result, Guid InspectedByEmployeeId, DateOnly InspectionDate);

public sealed class CreateQualityCheckValidator : AbstractValidator<CreateQualityCheckRequest>
{
    public CreateQualityCheckValidator()
    {
        RuleFor(x => x.WorkOrderId).NotEmpty().WithMessage("Odaberi nalog.");
        RuleFor(x => x.Phase).Must(p => QualityCheck.WorkOrderPhases.Contains(p))
            .WithMessage("Na nalogu se unose kontrole faza Lasersko rezanje, Hidraulično presanje i Završna kontrola.");
        RuleFor(x => x.Characteristic).NotEmpty().WithMessage("Unesi karakteristiku / zahtjev crteža.")
            .MaximumLength(QualityCheck.TextMax).WithMessage($"Najviše {QualityCheck.TextMax} znakova.");
        RuleFor(x => x.AcceptanceCriterion).MaximumLength(QualityCheck.TextMax).WithMessage($"Najviše {QualityCheck.TextMax} znakova.");
        RuleFor(x => x.MeasuredResult).MaximumLength(QualityCheck.TextMax).WithMessage($"Najviše {QualityCheck.TextMax} znakova.");
        RuleFor(x => x.SampleSize).GreaterThanOrEqualTo(1).When(x => x.SampleSize is not null).WithMessage("Veličina uzorka mora biti najmanje 1.");
        RuleFor(x => x.Result).IsInEnum().WithMessage("Odaberi rezultat.");
        RuleFor(x => x.InspectedByEmployeeId).NotEmpty().WithMessage("Odaberi ko je kontrolisao.");
        RuleFor(x => x.InspectionDate).NotEmpty().WithMessage("Unesi datum kontrole.");
    }
}

public static class QualityCheckEndpoints
{
    public static void MapQualityCheckEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/work-orders/{id:guid}/quality-checks", ListForWorkOrder).RequireAuthorization();
        app.MapPost("/api/quality-checks", Create).Validate<CreateQualityCheckRequest>().RequireAuthorization(Policies.KontrolorOrAdmin);
        app.MapPost("/api/quality-checks/{id:guid}/void", Void).Validate<VoidRequest>().RequireAuthorization(Policies.Admin);
    }

    private static IQueryable<QualityCheckDto> Project(AppDbContext db) =>
        from q in db.QualityChecks
        join e in db.Employees on q.InspectedByEmployeeId equals e.Id
        select new QualityCheckDto
        {
            Id = q.Id, Number = q.Number, WorkOrderId = q.WorkOrderId, Phase = q.Phase, Characteristic = q.Characteristic,
            AcceptanceCriterion = q.AcceptanceCriterion, MeasuredResult = q.MeasuredResult, SampleSize = q.SampleSize,
            Result = q.Result, InspectedByEmployeeId = q.InspectedByEmployeeId, InspectedByName = e.FullName,
            InspectionDate = q.InspectionDate, IsVoided = q.IsVoided, VoidedReason = q.VoidedReason, Version = q.Version,
        };

    private static async Task<IResult> ListForWorkOrder(Guid id, AppDbContext db, CancellationToken ct)
    {
        if (!await db.WorkOrders.AnyAsync(w => w.Id == id, ct))
            return ApiResults.NotFound("Nalog ne postoji.");
        return TypedResults.Ok(await Project(db).Where(q => q.WorkOrderId == id)
            .OrderBy(q => q.Phase).ThenByDescending(q => q.InspectionDate).ThenByDescending(q => q.Number).ToListAsync(ct));
    }

    private static async Task<IResult> Create(CreateQualityCheckRequest req, AppDbContext db, DocumentNumberService numbers, IClock clock, CancellationToken ct)
    {
        if (req.InspectionDate > clock.Today)
            return ApiResults.Validation("inspectionDate", "Datum kontrole ne može biti u budućnosti.");
        if (!await db.WorkOrders.AnyAsync(w => w.Id == req.WorkOrderId, ct))
            return ApiResults.Validation("workOrderId", "Nalog ne postoji.");
        if (!await db.Employees.AnyAsync(e => e.Id == req.InspectedByEmployeeId, ct))
            return ApiResults.Validation("inspectedByEmployeeId", "Zaposlenik ne postoji.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var check = new QualityCheck
        {
            Number = await numbers.NextAsync(DocumentNumberKind.QualityCheck, req.InspectionDate, ct),
            WorkOrderId = req.WorkOrderId,
            Phase = req.Phase,
            Characteristic = req.Characteristic.Trim(),
            AcceptanceCriterion = Clean(req.AcceptanceCriterion),
            MeasuredResult = Clean(req.MeasuredResult),
            SampleSize = req.SampleSize,
            Result = req.Result,
            InspectedByEmployeeId = req.InspectedByEmployeeId,
            InspectionDate = req.InspectionDate,
        };
        db.QualityChecks.Add(check);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return TypedResults.Created($"/api/quality-checks/{check.Id}", await Project(db).SingleAsync(q => q.Id == check.Id, ct));
    }

    private static async Task<IResult> Void(Guid id, VoidRequest req, AppDbContext db, CancellationToken ct)
    {
        var check = await db.QualityChecks.SingleOrDefaultAsync(q => q.Id == id, ct);
        if (check is null)
            return ApiResults.NotFound("Kontrola ne postoji.");
        db.Entry(check).Property(x => x.Version).OriginalValue = req.Version;
        check.Void(req.Reason);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Project(db).SingleAsync(q => q.Id == id, ct));
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
