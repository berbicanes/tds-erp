using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.WorkOrders;

public sealed record SaveWorkOrderRequest(
    string Customer, string ProductDescription, int OrderedQty, DateOnly DueDate, DateOnly? ReleaseDate,
    Priority Priority, string? DrawingNumber, string? DrawingRevision, Guid? DrawingFileId,
    Guid? ResponsibleEmployeeId, string? Notes, uint Version);

public sealed record SetQualityApprovalRequest(QualityApproval QualityApproval, uint Version);

public sealed class SaveWorkOrderValidator : AbstractValidator<SaveWorkOrderRequest>
{
    public SaveWorkOrderValidator()
    {
        RuleFor(x => x.Customer).NotEmpty().WithMessage("Unesi kupca.")
            .MaximumLength(WorkOrder.CustomerMax).WithMessage($"Kupac može imati najviše {WorkOrder.CustomerMax} znakova.");
        RuleFor(x => x.ProductDescription).NotEmpty().WithMessage("Unesi proizvod / opis.")
            .MaximumLength(WorkOrder.ProductDescriptionMax).WithMessage($"Opis može imati najviše {WorkOrder.ProductDescriptionMax} znakova.");
        RuleFor(x => x.OrderedQty).GreaterThan(0).WithMessage("Naručena količina mora biti veća od 0.");
        RuleFor(x => x.DueDate).NotEmpty().WithMessage("Unesi rok isporuke.");
        RuleFor(x => x.Priority).IsInEnum().WithMessage("Odaberi prioritet.");
        RuleFor(x => x.DrawingNumber).MaximumLength(WorkOrder.DrawingNumberMax).WithMessage($"Broj crteža može imati najviše {WorkOrder.DrawingNumberMax} znakova.");
        RuleFor(x => x.DrawingRevision).MaximumLength(WorkOrder.DrawingRevisionMax).WithMessage($"Revizija može imati najviše {WorkOrder.DrawingRevisionMax} znakova.");
        RuleFor(x => x.Notes).MaximumLength(WorkOrder.NotesMax).WithMessage($"Napomena može imati najviše {WorkOrder.NotesMax} znakova.");
    }
}

public static class WorkOrderEndpoints
{
    public static void MapWorkOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup("/api/work-orders").RequireAuthorization();
        read.MapGet("/", List);
        read.MapGet("/options", Options);
        read.MapGet("/{id:guid}", Get);

        var write = app.MapGroup("/api/work-orders").RequireAuthorization(Policies.KontrolorOrAdmin);
        write.MapPost("/", Create).Validate<SaveWorkOrderRequest>();
        write.MapPut("/{id:guid}", Update).Validate<SaveWorkOrderRequest>();
        write.MapPost("/{id:guid}/quality-approval", SetQualityApproval);
    }

    private static readonly SortMap<WorkOrderRow> Sort = new SortMap<WorkOrderRow>()
        .Add("number", x => x.Number)
        .Add("customer", x => x.Customer)
        .Add("productDescription", x => x.ProductDescription)
        .Add("dueDate", x => x.DueDate)
        .Add("status", x => x.Status)
        .Add("priority", x => x.Priority)
        .Add("createdAt", x => x.CreatedAt)
        .Default("dueDate");

    /// <param name="active">true = status ∉ {Zavrseno, Isporuceno, Otkazano} (UI §3 "Aktivni nalozi").</param>
    /// <param name="overdue">true = IsOverdue (DOMAIN §3).</param>
    /// <param name="dueWithinDays">active orders with DueDate ≤ today + N (UI §3 "Rok u 7 dana").</param>
    private static async Task<PagedResult<WorkOrderDto>> List(
        AppDbContext db, IClock clock, [AsParameters] ListQuery list,
        WorkOrderStatus[]? status, Priority[]? priority, bool? active, bool? overdue, int? dueWithinDays, CancellationToken ct)
    {
        var today = clock.Today;
        var q = WorkOrderQueries.Rows(db);
        if (list.Search is { } s)
        {
            var like = PagingExtensions.Like(s);
            q = q.Where(x => EF.Functions.ILike(x.Number, like) || EF.Functions.ILike(x.Customer, like)
                             || EF.Functions.ILike(x.ProductDescription, like)
                             || (x.DrawingNumber != null && EF.Functions.ILike(x.DrawingNumber, like)));
        }
        if (status is { Length: > 0 })
            q = q.Where(x => status.Contains(x.Status));
        if (priority is { Length: > 0 })
            q = q.Where(x => priority.Contains(x.Priority));
        if (active == true || overdue == true || dueWithinDays is not null)
            q = q.Where(x => !WorkOrder.FinalStatuses.Contains(x.Status));
        if (overdue == true)
            q = q.Where(x => x.DueDate < today);
        if (dueWithinDays is { } days)
            q = q.Where(x => x.DueDate <= today.AddDays(days));

        var page = await q.ToPagedAsync(list, Sort, x => x, ct);
        return new PagedResult<WorkOrderDto>(page.Items.Select(r => WorkOrderDto.From(r, today)).ToList(), page.Total, page.Page, page.PageSize);
    }

    public sealed record WorkOrderOption(Guid Id, string Number, string Customer, string ProductDescription, WorkOrderStatus Status);

    /// <summary>Active orders for pickers (production entry, quick entry).</summary>
    private static async Task<List<WorkOrderOption>> Options(AppDbContext db, CancellationToken ct) =>
        await db.WorkOrders.Where(w => !WorkOrder.FinalStatuses.Contains(w.Status)).OrderBy(w => w.DueDate)
            .Select(w => new WorkOrderOption(w.Id, w.Number, w.Customer, w.ProductDescription, w.Status)).ToListAsync(ct);

    public static async Task<WorkOrderDto?> GetDtoAsync(AppDbContext db, Guid id, DateOnly today, CancellationToken ct) =>
        await WorkOrderQueries.Rows(db).SingleOrDefaultAsync(x => x.Id == id, ct) is { } row ? WorkOrderDto.From(row, today) : null;

    private static async Task<IResult> Get(Guid id, AppDbContext db, IClock clock, CancellationToken ct) =>
        await GetDtoAsync(db, id, clock.Today, ct) is { } dto ? TypedResults.Ok(dto) : ApiResults.NotFound("Nalog ne postoji.");

    private static async Task<IResult> Create(SaveWorkOrderRequest req, AppDbContext db, DocumentNumberService numbers, IClock clock, CancellationToken ct)
    {
        if (await CheckRefs(req, db, ct) is { } problem)
            return problem;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var wo = new WorkOrder
        {
            Number = await numbers.NextAsync(DocumentNumberKind.WorkOrder, clock.Today, ct),
            Customer = req.Customer.Trim(),
            ProductDescription = req.ProductDescription.Trim(),
        };
        Apply(wo, req);
        db.WorkOrders.Add(wo);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return TypedResults.Created($"/api/work-orders/{wo.Id}", await GetDtoAsync(db, wo.Id, clock.Today, ct));
    }

    private static async Task<IResult> Update(Guid id, SaveWorkOrderRequest req, AppDbContext db, IClock clock, CancellationToken ct)
    {
        var wo = await db.WorkOrders.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (wo is null)
            return ApiResults.NotFound("Nalog ne postoji.");
        if (await CheckRefs(req, db, ct) is { } problem)
            return problem;

        db.Entry(wo).Property(x => x.Version).OriginalValue = req.Version;
        if (wo.IsReadOnly)
        {
            // DOMAIN §4: Isporuceno / Otkazano are read-only except Notes.
            var changed = wo.Customer != req.Customer.Trim() || wo.ProductDescription != req.ProductDescription.Trim()
                || wo.OrderedQty != req.OrderedQty || wo.DueDate != req.DueDate || wo.ReleaseDate != req.ReleaseDate
                || wo.Priority != req.Priority || wo.DrawingNumber != Clean(req.DrawingNumber)
                || wo.DrawingRevision != Clean(req.DrawingRevision) || wo.DrawingFileId != req.DrawingFileId
                || wo.ResponsibleEmployeeId != req.ResponsibleEmployeeId;
            if (changed)
                return ApiResults.Conflict($"Nalog {wo.Number} ima status {DisplayNames.Of(wo.Status)}; može se mijenjati samo napomena.");
            wo.Notes = Clean(req.Notes);
        }
        else
        {
            wo.Customer = req.Customer.Trim();
            wo.ProductDescription = req.ProductDescription.Trim();
            Apply(wo, req);
        }
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await GetDtoAsync(db, id, clock.Today, ct));
    }

    /// <summary>Odobrenje kvaliteta: only Kontrolor/Admin, never through the order form (DATA_MODEL §2).</summary>
    private static async Task<IResult> SetQualityApproval(Guid id, SetQualityApprovalRequest req, AppDbContext db, IClock clock, CancellationToken ct)
    {
        var wo = await db.WorkOrders.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (wo is null)
            return ApiResults.NotFound("Nalog ne postoji.");
        if (!Enum.IsDefined(req.QualityApproval))
            return ApiResults.Validation("qualityApproval", "Odaberi odobrenje kvaliteta.");
        if (wo.IsReadOnly)
            return ApiResults.Conflict($"Nalog {wo.Number} ima status {DisplayNames.Of(wo.Status)}; odobrenje kvaliteta se više ne mijenja.");
        db.Entry(wo).Property(x => x.Version).OriginalValue = req.Version;
        wo.QualityApproval = req.QualityApproval;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await GetDtoAsync(db, id, clock.Today, ct));
    }

    private static async Task<IResult?> CheckRefs(SaveWorkOrderRequest req, AppDbContext db, CancellationToken ct)
    {
        if (req.DrawingFileId is { } f && !await db.FileAttachments.AnyAsync(x => x.Id == f, ct))
            return ApiResults.Validation("drawingFileId", "Fajl crteža ne postoji.");
        if (req.ResponsibleEmployeeId is { } e && !await db.Employees.AnyAsync(x => x.Id == e, ct))
            return ApiResults.Validation("responsibleEmployeeId", "Odgovorna osoba ne postoji.");
        return null;
    }

    private static void Apply(WorkOrder wo, SaveWorkOrderRequest req)
    {
        wo.OrderedQty = req.OrderedQty;
        wo.DueDate = req.DueDate;
        wo.ReleaseDate = req.ReleaseDate;
        wo.Priority = req.Priority;
        wo.DrawingNumber = Clean(req.DrawingNumber);
        wo.DrawingRevision = Clean(req.DrawingRevision);
        wo.DrawingFileId = req.DrawingFileId;
        wo.ResponsibleEmployeeId = req.ResponsibleEmployeeId;
        wo.Notes = Clean(req.Notes);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
