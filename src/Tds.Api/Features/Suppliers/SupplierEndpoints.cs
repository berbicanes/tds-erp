using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.QualityChecks;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Suppliers;

public sealed record SupplierDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? MaterialOrService { get; init; }
    public required SupplierApprovalStatus ApprovalStatus { get; init; }
    public DateOnly? InitialEvaluationDate { get; init; }
    public DateOnly? NextEvaluationDate { get; init; }
    public string? EvaluationCriteria { get; init; }
    public Guid? ResponsibleEmployeeId { get; init; }
    public string? ResponsibleEmployeeName { get; init; }
    public string? Notes { get; init; }
    public required uint Version { get; init; }
}

public sealed record SupplierOption(Guid Id, string Name, SupplierApprovalStatus ApprovalStatus);

public sealed record SaveSupplierRequest(
    string Name, string? MaterialOrService, SupplierApprovalStatus ApprovalStatus,
    DateOnly? InitialEvaluationDate, DateOnly? NextEvaluationDate, string? EvaluationCriteria,
    Guid? ResponsibleEmployeeId, string? Notes, uint Version);

public sealed class SaveSupplierValidator : AbstractValidator<SaveSupplierRequest>
{
    public SaveSupplierValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Unesi naziv dobavljača.")
            .MaximumLength(Supplier.NameMax).WithMessage($"Naziv može imati najviše {Supplier.NameMax} znakova.");
        RuleFor(x => x.MaterialOrService).MaximumLength(Supplier.MaterialOrServiceMax)
            .WithMessage($"Materijal / usluga može imati najviše {Supplier.MaterialOrServiceMax} znakova.");
        RuleFor(x => x.ApprovalStatus).IsInEnum().WithMessage("Odaberi status odobrenja.");
        RuleFor(x => x.EvaluationCriteria).MaximumLength(Supplier.TextMax).WithMessage($"Najviše {Supplier.TextMax} znakova.");
        RuleFor(x => x.Notes).MaximumLength(Supplier.TextMax).WithMessage($"Najviše {Supplier.TextMax} znakova.");
        RuleFor(x => x.NextEvaluationDate)
            .GreaterThanOrEqualTo(x => x.InitialEvaluationDate).When(x => x.InitialEvaluationDate is not null && x.NextEvaluationDate is not null)
            .WithMessage("Sljedeća ocjena ne može biti prije početne ocjene.");
    }
}

public static class SupplierEndpoints
{
    public static void MapSupplierEndpoints(this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup("/api/suppliers").RequireAuthorization();
        read.MapGet("/", List);
        read.MapGet("/options", Options);
        read.MapGet("/{id:guid}", Get);

        var write = app.MapGroup("/api/suppliers").RequireAuthorization(Policies.KontrolorOrAdmin);
        write.MapPost("/", Create).Validate<SaveSupplierRequest>();
        write.MapPut("/{id:guid}", Update).Validate<SaveSupplierRequest>();
        read.MapGet("/{id:guid}/certificates", Certificates);
        write.MapPost("/{id:guid}/certificates", AddCertificate);
        // DOMAIN §7.7: only Admin removes the link; the file itself stays.
        app.MapPost("/api/suppliers/{id:guid}/certificates/{fileId:guid}/remove", RemoveCertificate).RequireAuthorization(Policies.Admin);
    }

    private static readonly SortMap<SupplierDto> Sort = new SortMap<SupplierDto>()
        .Add("name", x => x.Name)
        .Add("approvalStatus", x => x.ApprovalStatus)
        .Add("nextEvaluationDate", x => x.NextEvaluationDate)
        .Default("name");

    private static IQueryable<SupplierDto> Project(AppDbContext db) =>
        from s in db.Suppliers
        join e in db.Employees on s.ResponsibleEmployeeId equals e.Id into es
        from e in es.DefaultIfEmpty()
        select new SupplierDto
        {
            Id = s.Id, Name = s.Name, MaterialOrService = s.MaterialOrService, ApprovalStatus = s.ApprovalStatus,
            InitialEvaluationDate = s.InitialEvaluationDate, NextEvaluationDate = s.NextEvaluationDate,
            EvaluationCriteria = s.EvaluationCriteria, ResponsibleEmployeeId = s.ResponsibleEmployeeId,
            ResponsibleEmployeeName = e.FullName, Notes = s.Notes, Version = s.Version,
        };

    private static async Task<PagedResult<SupplierDto>> List(
        AppDbContext db, [AsParameters] ListQuery list, SupplierApprovalStatus[]? approvalStatus, CancellationToken ct)
    {
        var q = Project(db);
        if (list.Search is { } s)
            q = q.Where(x => EF.Functions.ILike(x.Name, PagingExtensions.Like(s))
                             || (x.MaterialOrService != null && EF.Functions.ILike(x.MaterialOrService, PagingExtensions.Like(s))));
        if (approvalStatus is { Length: > 0 })
            q = q.Where(x => approvalStatus.Contains(x.ApprovalStatus));
        return await q.ToPagedAsync(list, Sort, x => x, ct);
    }

    /// <summary>All suppliers incl. Blokiran (the lot form shows the status and the server rejects Blokiran).</summary>
    private static async Task<List<SupplierOption>> Options(AppDbContext db, CancellationToken ct) =>
        await db.Suppliers.OrderBy(s => s.Name).Select(s => new SupplierOption(s.Id, s.Name, s.ApprovalStatus)).ToListAsync(ct);

    private static async Task<IResult> Get(Guid id, AppDbContext db, CancellationToken ct) =>
        await Project(db).SingleOrDefaultAsync(x => x.Id == id, ct) is { } dto
            ? TypedResults.Ok(dto)
            : ApiResults.NotFound("Dobavljač ne postoji.");

    private static async Task<IResult> Create(SaveSupplierRequest req, AppDbContext db, CancellationToken ct)
    {
        if (await Check(req, null, db, ct) is { } problem)
            return problem;
        var s = new Supplier { Name = req.Name.Trim() };
        Apply(s, req);
        db.Suppliers.Add(s);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/suppliers/{s.Id}", await Project(db).SingleAsync(x => x.Id == s.Id, ct));
    }

    private static async Task<IResult> Update(Guid id, SaveSupplierRequest req, AppDbContext db, CancellationToken ct)
    {
        var s = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (s is null)
            return ApiResults.NotFound("Dobavljač ne postoji.");
        if (await Check(req, id, db, ct) is { } problem)
            return problem;
        db.Entry(s).Property(x => x.Version).OriginalValue = req.Version;
        s.Name = req.Name.Trim();
        Apply(s, req);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Project(db).SingleAsync(x => x.Id == id, ct));
    }

    private static async Task<IResult?> Check(SaveSupplierRequest req, Guid? id, AppDbContext db, CancellationToken ct)
    {
        if (await db.Suppliers.AnyAsync(x => x.Name == req.Name.Trim() && x.Id != id, ct))
            return ApiResults.Validation("name", $"Dobavljač {req.Name.Trim()} već postoji.");
        if (req.ResponsibleEmployeeId is { } emp && !await db.Employees.AnyAsync(e => e.Id == emp, ct))
            return ApiResults.Validation("responsibleEmployeeId", "Odgovorna osoba ne postoji.");
        return null;
    }

    private static void Apply(Supplier s, SaveSupplierRequest req)
    {
        s.MaterialOrService = Clean(req.MaterialOrService);
        s.ApprovalStatus = req.ApprovalStatus;
        s.InitialEvaluationDate = req.InitialEvaluationDate;
        s.NextEvaluationDate = req.NextEvaluationDate;
        s.EvaluationCriteria = Clean(req.EvaluationCriteria);
        s.ResponsibleEmployeeId = req.ResponsibleEmployeeId;
        s.Notes = Clean(req.Notes);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static async Task<IResult> Certificates(Guid id, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Suppliers.AnyAsync(s => s.Id == id, ct))
            return ApiResults.NotFound("Dobavljač ne postoji.");
        return TypedResults.Ok(await (from c in db.Set<SupplierCertificate>()
                                      where c.SupplierId == id
                                      join f in db.FileAttachments on c.FileId equals f.Id
                                      orderby f.CreatedAt descending
                                      select new EvidenceFileDto(f.Id, f.OriginalName, f.ContentType, f.SizeBytes)).ToListAsync(ct));
    }

    private static async Task<IResult> AddCertificate(Guid id, FileIdRequest req, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Suppliers.AnyAsync(s => s.Id == id, ct))
            return ApiResults.NotFound("Dobavljač ne postoji.");
        if (!await db.FileAttachments.AnyAsync(f => f.Id == req.FileId, ct))
            return ApiResults.Validation("fileId", "Fajl ne postoji.");
        // Load the whole set so the AuditLog shows the full before/after list (IAuditedLink).
        var existing = await db.Set<SupplierCertificate>().Where(c => c.SupplierId == id).ToListAsync(ct);
        if (existing.All(c => c.FileId != req.FileId))
        {
            db.Add(new SupplierCertificate { SupplierId = id, FileId = req.FileId });
            await db.SaveChangesAsync(ct);
        }
        return TypedResults.NoContent();
    }

    private static async Task<IResult> RemoveCertificate(Guid id, Guid fileId, AppDbContext db, CancellationToken ct)
    {
        var existing = await db.Set<SupplierCertificate>().Where(c => c.SupplierId == id).ToListAsync(ct);
        var link = existing.SingleOrDefault(c => c.FileId == fileId);
        if (link is null)
            return ApiResults.NotFound("Certifikat nije vezan za ovog dobavljača.");
        db.Remove(link);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
