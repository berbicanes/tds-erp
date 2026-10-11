using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Materials;
using Tds.Api.Features.WorkOrders;
using Tds.Api.Infrastructure;
using System.Security.Claims;

namespace Tds.Api.Features.ProductionEntries;

public sealed record ProductionEntryDto
{
    public required Guid Id { get; init; }
    public required string Number { get; init; }
    public required DateOnly Date { get; init; }
    public required Guid WorkOrderId { get; init; }
    public required string WorkOrderNumber { get; init; }
    public required string Customer { get; init; }
    public required Guid OperatorEmployeeId { get; init; }
    public required string OperatorName { get; init; }
    public Guid? MaterialLotId { get; init; }
    public string? LotNumber { get; init; }
    public Unit? LotUnit { get; init; }
    public required int LaserCutQty { get; init; }
    public required int PressedQty { get; init; }
    public required int GoodQty { get; init; }
    public required int ScrapQty { get; init; }
    public required decimal LaborHours { get; init; }
    public decimal? MaterialConsumed { get; init; }
    public string? Note { get; init; }
    public required List<Guid> EquipmentIds { get; init; }
    public required List<string> EquipmentCodes { get; init; }
    public Guid? CreatedBy { get; init; }
    public string? CreatedByName { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required bool IsVoided { get; init; }
    public string? VoidedReason { get; init; }
    public required uint Version { get; init; }
}

public sealed record SaveProductionEntryRequest(
    Guid? WorkOrderId, DateOnly? Date, Guid? OperatorEmployeeId, Guid? MaterialLotId, List<Guid>? EquipmentIds,
    int? LaserCutQty, int? PressedQty, int? GoodQty, int? ScrapQty, decimal? LaborHours, decimal? MaterialConsumed,
    string? Note, uint? Version);

public sealed record ProductionTotals(int LaserCutQty, int PressedQty, int GoodQty, int ScrapQty, decimal LaborHours, int Entries);

public sealed record WorkOrderProduction(List<ProductionEntryDto> Items, ProductionTotals Totals);

public sealed class SaveProductionEntryValidator : AbstractValidator<SaveProductionEntryRequest>
{
    public SaveProductionEntryValidator(IHttpContextAccessor http)
    {
        RuleFor(x => x.WorkOrderId).NotEmpty().WithMessage("Odaberi nalog.");
        RuleFor(x => x.OperatorEmployeeId).NotEmpty().WithMessage("Odaberi operatera.");
        RuleFor(x => x.Date).NotEmpty().WithMessage("Unesi datum.");
        RuleFor(x => x.LaserCutQty).Required("Unesi broj komada (0 ako ih nema).").GreaterThanOrEqualTo(0).WithMessage("Ne može biti negativno.");
        RuleFor(x => x.PressedQty).Required("Unesi broj komada (0 ako ih nema).").GreaterThanOrEqualTo(0).WithMessage("Ne može biti negativno.");
        RuleFor(x => x.GoodQty).Required("Unesi broj komada (0 ako ih nema).").GreaterThanOrEqualTo(0).WithMessage("Ne može biti negativno.");
        RuleFor(x => x.ScrapQty).Required("Unesi broj komada (0 ako ih nema).").GreaterThanOrEqualTo(0).WithMessage("Ne može biti negativno.");
        RuleFor(x => x.LaborHours).Required("Unesi radne sate.").InclusiveBetween(0, ProductionEntry.MaxLaborHours).WithMessage("Radni sati moraju biti između 0 i 24.")
            .Must(h => h is null || decimal.Round(h.Value, 2) == h).WithMessage("Radni sati mogu imati najviše 2 decimale.");
        RuleFor(x => x.MaterialConsumed).GreaterThan(0).When(x => x.MaterialConsumed is not null).WithMessage("Utrošak mora biti veći od 0 ili prazan.")
            .Must(c => c is null || decimal.Round(c.Value, 3) == c).WithMessage("Utrošak može imati najviše 3 decimale.");
        RuleFor(x => x.Note).MaximumLength(ProductionEntry.NoteMax).WithMessage($"Napomena može imati najviše {ProductionEntry.NoteMax} znakova.");
        this.VersionOnUpdate(http, x => x.Version);
    }
}

public static class ProductionEntryEndpoints
{
    public static void MapProductionEntryEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/production-entries").RequireAuthorization();
        g.MapGet("/", List);
        g.MapGet("/{id:guid}", Get);
        g.MapPost("/", Create).Validate<SaveProductionEntryRequest>();
        g.MapPut("/{id:guid}", Update).Validate<SaveProductionEntryRequest>();
        g.MapPost("/{id:guid}/void", Void).Validate<VoidRequest>();
        app.MapGet("/api/work-orders/{id:guid}/production-entries", ForWorkOrder).RequireAuthorization();
    }

    private sealed record Row
    {
        public required ProductionEntry E { get; init; }
        public required string WorkOrderNumber { get; init; }
        public required string Customer { get; init; }
        public required string OperatorName { get; init; }
        public string? LotNumber { get; init; }
        public Unit? LotUnit { get; init; }
        public string? CreatedByName { get; init; }
    }

    private static IQueryable<Row> Rows(AppDbContext db) =>
        from e in db.ProductionEntries
        join w in db.WorkOrders on e.WorkOrderId equals w.Id
        join o in db.Employees on e.OperatorEmployeeId equals o.Id
        join l in db.MaterialLots on e.MaterialLotId equals l.Id into ls
        from l in ls.DefaultIfEmpty()
        join i in db.MaterialItems on l.MaterialItemId equals i.Id into iss
        from i in iss.DefaultIfEmpty()
        join u in db.Users on e.CreatedBy equals u.Id into us
        from u in us.DefaultIfEmpty()
        select new Row
        {
            E = e, WorkOrderNumber = w.Number, Customer = w.Customer, OperatorName = o.FullName,
            LotNumber = l.LotNumber, LotUnit = (Unit?)i.Unit, CreatedByName = u.UserName,
        };

    private static async Task<List<ProductionEntryDto>> ToDtos(AppDbContext db, List<Row> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.E.Id).ToList();
        var equipment = await (from pe in db.Set<ProductionEntryEquipment>()
                               where ids.Contains(pe.ProductionEntryId)
                               join eq in db.Equipment on pe.EquipmentId equals eq.Id
                               orderby eq.Code
                               select new { pe.ProductionEntryId, eq.Id, eq.Code }).ToListAsync(ct);
        return rows.Select(r => new ProductionEntryDto
        {
            Id = r.E.Id, Number = r.E.Number, Date = r.E.Date, WorkOrderId = r.E.WorkOrderId, WorkOrderNumber = r.WorkOrderNumber,
            Customer = r.Customer, OperatorEmployeeId = r.E.OperatorEmployeeId, OperatorName = r.OperatorName,
            MaterialLotId = r.E.MaterialLotId, LotNumber = r.LotNumber, LotUnit = r.LotUnit,
            LaserCutQty = r.E.LaserCutQty, PressedQty = r.E.PressedQty, GoodQty = r.E.GoodQty, ScrapQty = r.E.ScrapQty,
            LaborHours = r.E.LaborHours, MaterialConsumed = r.E.MaterialConsumed, Note = r.E.Note,
            EquipmentIds = equipment.Where(x => x.ProductionEntryId == r.E.Id).Select(x => x.Id).ToList(),
            EquipmentCodes = equipment.Where(x => x.ProductionEntryId == r.E.Id).Select(x => x.Code).ToList(),
            CreatedBy = r.E.CreatedBy, CreatedByName = r.CreatedByName, CreatedAt = r.E.CreatedAt,
            IsVoided = r.E.IsVoided, VoidedReason = r.E.VoidedReason, Version = r.E.Version,
        }).ToList();
    }

    private static async Task<ProductionEntryDto> GetDto(AppDbContext db, Guid id, CancellationToken ct) =>
        (await ToDtos(db, await Rows(db).Where(r => r.E.Id == id).ToListAsync(ct), ct)).Single();

    private static async Task<PagedResult<ProductionEntryDto>> List(
        AppDbContext db, [AsParameters] ListQuery list, DateOnly? from, DateOnly? to, Guid? workOrderId, Guid? operatorEmployeeId,
        bool? includeVoided, CancellationToken ct)
    {
        var q = Rows(db);
        if (includeVoided != true)
            q = q.Where(r => !r.E.IsVoided);
        if (from is { } f) q = q.Where(r => r.E.Date >= f);
        if (to is { } t) q = q.Where(r => r.E.Date <= t);
        if (workOrderId is { } w) q = q.Where(r => r.E.WorkOrderId == w);
        if (operatorEmployeeId is { } o) q = q.Where(r => r.E.OperatorEmployeeId == o);
        if (list.Search is { } s)
        {
            var like = PagingExtensions.Like(s);
            q = q.Where(r => EF.Functions.ILike(r.E.Number, like) || EF.Functions.ILike(r.WorkOrderNumber, like)
                             || EF.Functions.ILike(r.Customer, like) || EF.Functions.ILike(r.OperatorName, like)
                             || (r.LotNumber != null && EF.Functions.ILike(r.LotNumber, like)));
        }
        var sort = new SortMap<Row>()
            .Add("date", r => r.E.Date).Add("number", r => r.E.Number).Add("workOrderNumber", r => r.WorkOrderNumber)
            .Add("operatorName", r => r.OperatorName).Add("goodQty", r => r.E.GoodQty)
            .Default("number", descending: true);
        var total = await q.CountAsync(ct);
        var rows = await sort.Apply(q, list.Sort).Skip((list.SafePage - 1) * list.SafePageSize).Take(list.SafePageSize).ToListAsync(ct);
        return new PagedResult<ProductionEntryDto>(await ToDtos(db, rows, ct), total, list.SafePage, list.SafePageSize);
    }

    private static async Task<IResult> Get(Guid id, AppDbContext db, CancellationToken ct) =>
        await db.ProductionEntries.AnyAsync(e => e.Id == id, ct) ? TypedResults.Ok(await GetDto(db, id, ct)) : ApiResults.NotFound("Dnevni zapis ne postoji.");

    /// <summary>UI §6 "Proizvodnja": all entries of the order with totals (non-voided only in totals).</summary>
    private static async Task<IResult> ForWorkOrder(Guid id, AppDbContext db, CancellationToken ct)
    {
        if (!await db.WorkOrders.AnyAsync(w => w.Id == id, ct))
            return ApiResults.NotFound("Nalog ne postoji.");
        var rows = await Rows(db).Where(r => r.E.WorkOrderId == id).OrderByDescending(r => r.E.Date).ThenByDescending(r => r.E.Number).ToListAsync(ct);
        var valid = rows.Where(r => !r.E.IsVoided).Select(r => r.E).ToList();
        var totals = new ProductionTotals(valid.Sum(e => e.LaserCutQty), valid.Sum(e => e.PressedQty), valid.Sum(e => e.GoodQty),
            valid.Sum(e => e.ScrapQty), valid.Sum(e => e.LaborHours), valid.Count);
        return TypedResults.Ok(new WorkOrderProduction(await ToDtos(db, rows, ct), totals));
    }

    private static async Task<IResult> Create(
        SaveProductionEntryRequest req, AppDbContext db, IClock clock, DocumentNumberService numbers, ClaimsPrincipal user, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var entry = new ProductionEntry
        {
            Number = await numbers.NextAsync(DocumentNumberKind.ProductionEntry, req.Date!.Value, ct),
            WorkOrderId = req.WorkOrderId!.Value,
        };
        var result = await Apply(entry, req, null, db, clock, user, ct);
        if (result.Problem is not null)
            return result.Problem;
        db.ProductionEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return TypedResults.Created($"/api/production-entries/{entry.Id}", new WithWarnings<ProductionEntryDto>(await GetDto(db, entry.Id, ct), result.Warnings));
    }

    private static async Task<IResult> Update(
        Guid id, SaveProductionEntryRequest req, AppDbContext db, IClock clock, ICurrentUser current, ClaimsPrincipal user, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var entry = await db.ProductionEntries.Include(e => e.Equipment).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null)
            return ApiResults.NotFound("Dnevni zapis ne postoji.");
        if (entry.IsVoided)
            return ApiResults.Conflict($"Zapis {entry.Number} je poništen i ne može se mijenjati.");
        if (CheckRights(entry, current, user, clock) is { } denied)
            return denied;
        if (req.WorkOrderId != entry.WorkOrderId)
            return ApiResults.Conflict("Nalog se na postojećem zapisu ne mijenja. Poništi zapis i unesi novi.");

        db.Entry(entry).Property(x => x.Version).OriginalValue = req.Version!.Value;
        var result = await Apply(entry, req, entry, db, clock, user, ct);
        if (result.Problem is not null)
            return result.Problem;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return TypedResults.Ok(new WithWarnings<ProductionEntryDto>(await GetDto(db, id, ct), result.Warnings));
    }

    private static async Task<IResult> Void(Guid id, VoidRequest req, AppDbContext db, IClock clock, ICurrentUser current, ClaimsPrincipal user, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var entry = await db.ProductionEntries.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null)
            return ApiResults.NotFound("Dnevni zapis ne postoji.");
        if (CheckRights(entry, current, user, clock) is { } denied)
            return denied;
        db.Entry(entry).Property(x => x.Version).OriginalValue = req.Version!.Value;
        entry.Void(req.Reason);
        // DOMAIN §5.6: voiding the entry voids its Izdavanje too (same transaction).
        foreach (var m in await db.StockMovements.Where(m => m.ProductionEntryId == id && !m.IsVoided).ToListAsync(ct))
            m.Void(req.Reason);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return TypedResults.Ok(await GetDto(db, id, ct));
    }

    /// <summary>DOMAIN §5.6 / §8: author on the creation day, Kontrolor/Admin always.</summary>
    private static IResult? CheckRights(ProductionEntry entry, ICurrentUser current, ClaimsPrincipal user, IClock clock)
    {
        var createdOn = DateOnly.FromDateTime(clock.ToLocal(entry.CreatedAt).DateTime);
        var privileged = user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Kontrolor);
        return ProductionEntryRules.CanChange(entry.CreatedBy, createdOn, current.UserId, privileged, clock.Today)
            ? null
            : TypedResults.Problem(detail: "Svoj zapis možeš mijenjati ili poništiti samo isti dan. Za starije zapise javi se kontroloru.",
                statusCode: StatusCodes.Status403Forbidden, title: "Zabranjeno");
    }

    private sealed record ApplyResult(IResult? Problem, IReadOnlyList<string> Warnings);

    /// <summary>Validates against DOMAIN §5 and writes fields, equipment, lot link and the Izdavanje movement (same transaction).</summary>
    private static async Task<ApplyResult> Apply(
        ProductionEntry entry, SaveProductionEntryRequest req, ProductionEntry? existing, AppDbContext db, IClock clock, ClaimsPrincipal user, CancellationToken ct)
    {
        var wo = await WorkOrderEndpoints.GetDtoAsync(db, req.WorkOrderId!.Value, clock.Today, ct);
        if (wo is null)
            return new(ApiResults.Validation("workOrderId", "Nalog ne postoji."), []);
        if (!await db.Employees.AnyAsync(e => e.Id == req.OperatorEmployeeId, ct))
            return new(ApiResults.Validation("operatorEmployeeId", "Operater ne postoji."), []);

        var equipmentIds = (req.EquipmentIds ?? []).Distinct().ToList();
        var machines = await db.Equipment.Where(e => equipmentIds.Contains(e.Id)).ToListAsync(ct);
        if (machines.Count != equipmentIds.Count)
            return new(ApiResults.Validation("equipmentIds", "Odabrana oprema ne postoji."), []);

        LotUse? lotUse = null;
        decimal onHandForEntry = 0m;
        if (req.MaterialLotId is { } lotId)
        {
            var lot = await db.MaterialLots.SingleOrDefaultAsync(l => l.Id == lotId, ct);
            if (lot is null)
                return new(ApiResults.Validation("materialLotId", "Lot ne postoji."), []);
            await db.LockLotAsync(lotId, ct);
            var unit = await db.MaterialItems.Where(i => i.Id == lot.MaterialItemId).Select(i => i.Unit).SingleAsync(ct);
            var alreadyConsumed = existing?.MaterialLotId == lotId ? existing.MaterialConsumed ?? 0m : 0m;
            onHandForEntry = await db.OnHandAsync(lotId, ct) + alreadyConsumed;
            lotUse = new LotUse(lot.LotNumber, lot.ReceiptDecision, lot.IsVoided, onHandForEntry, unit);
        }

        var otherGood = await db.ProductionEntries.Where(p => p.WorkOrderId == req.WorkOrderId && !p.IsVoided && p.Id != entry.Id).SumAsync(p => (int?)p.GoodQty, ct) ?? 0;
        var warnings = ProductionEntryRules.Check(
            new ProductionEntryInput(req.Date!.Value, req.LaserCutQty!.Value, req.PressedQty!.Value, req.GoodQty!.Value, req.ScrapQty!.Value, req.LaborHours!.Value, req.MaterialConsumed,
                machines.Count, machines.All(m => m.IsActive && m.Kind == EquipmentKind.Masina)),
            new OrderFacts(wo.Number, wo.Status, wo.FinalInspectionPassed, wo.OrderedQty, otherGood),
            lotUse, onHandForEntry, clock.Today, user.IsInRole(Roles.Admin));

        var stockChanged = existing is null || existing.MaterialLotId != req.MaterialLotId
            || existing.MaterialConsumed != req.MaterialConsumed || existing.Date != req.Date;

        entry.Date = req.Date!.Value;
        entry.OperatorEmployeeId = req.OperatorEmployeeId!.Value;
        entry.MaterialLotId = req.MaterialLotId;
        entry.LaserCutQty = req.LaserCutQty!.Value;
        entry.PressedQty = req.PressedQty!.Value;
        entry.GoodQty = req.GoodQty!.Value;
        entry.ScrapQty = req.ScrapQty!.Value;
        entry.LaborHours = req.LaborHours!.Value;
        entry.MaterialConsumed = req.MaterialConsumed;
        entry.Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();

        entry.Equipment.RemoveAll(x => !equipmentIds.Contains(x.EquipmentId));
        foreach (var eqId in equipmentIds.Where(x => entry.Equipment.All(e => e.EquipmentId != x)))
            entry.Equipment.Add(new ProductionEntryEquipment { ProductionEntryId = entry.Id, EquipmentId = eqId });

        // DOMAIN §5.2: a used lot must be linked to the order; if not, link it with 0 reservation.
        if (req.MaterialLotId is { } usedLot && !await db.WorkOrderMaterials.AnyAsync(x => x.WorkOrderId == req.WorkOrderId && x.MaterialLotId == usedLot, ct)
            && !db.WorkOrderMaterials.Local.Any(x => x.WorkOrderId == req.WorkOrderId && x.MaterialLotId == usedLot))
            db.WorkOrderMaterials.Add(WorkOrderMaterial.TraceOnly(req.WorkOrderId!.Value, usedLot));

        // DOMAIN §2.2: a changed consumption voids the old Izdavanje and creates a new one.
        if (stockChanged)
        {
            if (existing is not null)
                foreach (var m in await db.StockMovements.Where(m => m.ProductionEntryId == entry.Id && !m.IsVoided).ToListAsync(ct))
                    m.Void("Izmjena dnevnog zapisa " + entry.Number);
            if (req.MaterialLotId is { } l && req.MaterialConsumed is { } c)
                db.StockMovements.Add(ProductionEntryRules.Issue(entry, l, c));
        }
        return new(null, warnings);
    }
}
