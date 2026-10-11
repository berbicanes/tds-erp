using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Materials;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Documents;

public sealed record DocumentDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Code { get; init; }
    public required DocumentKind Kind { get; init; }
    public required string DocVersion { get; init; }
    public required DocumentStatus Status { get; init; }
    public Guid? OwnerEmployeeId { get; init; }
    public string? OwnerName { get; init; }
    public DateOnly? ApprovalDate { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public DateOnly? NextReviewDate { get; init; }
    public string? ChangeDescription { get; init; }
    public Guid? FileId { get; init; }
    public string? FileName { get; init; }
    public required bool IsVoided { get; init; }
    public string? VoidedReason { get; init; }
    public required uint Version { get; init; }
}

public sealed record DocumentDetailDto(DocumentDto Document, IReadOnlyList<DocumentTransitionCheck> Transitions, IReadOnlyList<DocumentDto> Versions);

public sealed record SaveDocumentRequest(
    string Title, string Code, DocumentKind? Kind, string DocVersion, Guid? OwnerEmployeeId, DateOnly? ApprovalDate,
    DateOnly? EffectiveDate, DateOnly? NextReviewDate, string? ChangeDescription, Guid? FileId, uint? Version);

public sealed record DocumentStatusRequest(DocumentStatus? Status, DateOnly? ApprovalDate, uint? Version);
public sealed record NextReviewRequest(DateOnly? NextReviewDate, uint? Version);

public sealed class DocumentStatusValidator : AbstractValidator<DocumentStatusRequest>
{
    public DocumentStatusValidator()
    {
        RuleFor(x => x.Status).Required("Odaberi novi status.").IsInEnum().WithMessage("Odaberi novi status.");
        RuleFor(x => x.Version).Required(ValidationExtensions.VersionMissing);
    }
}

/// <summary>NextReviewDate null = no review date; the version is always required.</summary>
public sealed class NextReviewValidator : AbstractValidator<NextReviewRequest>
{
    public NextReviewValidator() => RuleFor(x => x.Version).Required(ValidationExtensions.VersionMissing);
}
public sealed record NewVersionRequest(string DocVersion);

public sealed class SaveDocumentValidator : AbstractValidator<SaveDocumentRequest>
{
    public SaveDocumentValidator(IHttpContextAccessor http)
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Unesi naziv dokumenta.")
            .MaximumLength(ControlledDocument.TitleMax).WithMessage($"Najviše {ControlledDocument.TitleMax} znakova.");
        RuleFor(x => x.Code).NotEmpty().WithMessage("Unesi oznaku dokumenta, npr. PR-01.")
            .MaximumLength(ControlledDocument.CodeMax).WithMessage($"Najviše {ControlledDocument.CodeMax} znakova.");
        RuleFor(x => x.Kind).Required("Odaberi vrstu dokumenta.").IsInEnum().WithMessage("Odaberi vrstu dokumenta.");
        RuleFor(x => x.DocVersion).NotEmpty().WithMessage("Unesi verziju, npr. 01.")
            .MaximumLength(ControlledDocument.DocVersionMax).WithMessage($"Najviše {ControlledDocument.DocVersionMax} znakova.");
        RuleFor(x => x.ChangeDescription).MaximumLength(ControlledDocument.ChangeDescriptionMax).WithMessage($"Najviše {ControlledDocument.ChangeDescriptionMax} znakova.");
        this.VersionOnUpdate(http, x => x.Version);
    }
}

public sealed class NewVersionValidator : AbstractValidator<NewVersionRequest>
{
    public NewVersionValidator() =>
        RuleFor(x => x.DocVersion).NotEmpty().WithMessage("Unesi oznaku nove verzije, npr. 02.")
            .MaximumLength(ControlledDocument.DocVersionMax).WithMessage($"Najviše {ControlledDocument.DocVersionMax} znakova.");
}

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup("/api/documents").RequireAuthorization();
        read.MapGet("/", List);
        read.MapGet("/{id:guid}", Get);

        var write = app.MapGroup("/api/documents").RequireAuthorization(Policies.KontrolorOrAdmin);
        write.MapPost("/", Create).Validate<SaveDocumentRequest>();
        write.MapPut("/{id:guid}", Update).Validate<SaveDocumentRequest>();
        write.MapPost("/{id:guid}/status", ChangeStatus).Validate<DocumentStatusRequest>();
        write.MapPost("/{id:guid}/next-review", SetNextReview).Validate<NextReviewRequest>();
        write.MapPost("/{id:guid}/new-version", NewVersion).Validate<NewVersionRequest>();

        app.MapPost("/api/documents/{id:guid}/void", Void).Validate<VoidRequest>().RequireAuthorization(Policies.Admin);
    }

    public static IQueryable<DocumentDto> Project(AppDbContext db) =>
        from d in db.ControlledDocuments
        join e in db.Employees on d.OwnerEmployeeId equals e.Id into es
        from e in es.DefaultIfEmpty()
        join f in db.FileAttachments on d.FileId equals f.Id into fs
        from f in fs.DefaultIfEmpty()
        select new DocumentDto
        {
            Id = d.Id, Title = d.Title, Code = d.Code, Kind = d.Kind, DocVersion = d.DocVersion, Status = d.Status,
            OwnerEmployeeId = d.OwnerEmployeeId, OwnerName = e.FullName, ApprovalDate = d.ApprovalDate, EffectiveDate = d.EffectiveDate,
            NextReviewDate = d.NextReviewDate, ChangeDescription = d.ChangeDescription, FileId = d.FileId, FileName = f.OriginalName,
            IsVoided = d.IsVoided, VoidedReason = d.VoidedReason, Version = d.Version,
        };

    private static async Task<PagedResult<DocumentDto>> List(
        AppDbContext db, [AsParameters] ListQuery list, DocumentStatus[]? status, DocumentKind[]? kind, bool? includeVoided, CancellationToken ct)
    {
        var q = Project(db);
        if (includeVoided != true)
            q = q.Where(x => !x.IsVoided);
        if (status is { Length: > 0 })
            q = q.Where(x => status.Contains(x.Status));
        if (kind is { Length: > 0 })
            q = q.Where(x => kind.Contains(x.Kind));
        if (list.Search is { } s)
        {
            var like = PagingExtensions.Like(s);
            q = q.Where(x => EF.Functions.ILike(x.Code, like) || EF.Functions.ILike(x.Title, like));
        }
        var sort = new SortMap<DocumentDto>()
            .Add("code", x => x.Code).Add("title", x => x.Title).Add("status", x => x.Status).Add("nextReviewDate", x => x.NextReviewDate)
            .Default("code");
        return await q.ToPagedAsync(list, sort, x => x, ct);
    }

    private static async Task<IResult> Get(Guid id, AppDbContext db, IClock clock, CancellationToken ct)
    {
        var doc = await db.ControlledDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (doc is null)
            return ApiResults.NotFound("Dokument ne postoji.");
        var dto = await Project(db).SingleAsync(x => x.Id == id, ct);
        var versions = await Project(db).Where(x => x.Code == doc.Code).OrderByDescending(x => x.DocVersion).ToListAsync(ct);
        return TypedResults.Ok(new DocumentDetailDto(dto, doc.IsVoided ? [] : DocumentTransitions.Options(doc, clock.Today), versions));
    }

    private static async Task<IResult?> CheckReferences(SaveDocumentRequest req, Guid? id, AppDbContext db, CancellationToken ct)
    {
        var code = req.Code.Trim();
        var version = req.DocVersion.Trim();
        if (await db.ControlledDocuments.AnyAsync(x => x.Code == code && x.DocVersion == version && !x.IsVoided && x.Id != id, ct))
            return ApiResults.Validation("docVersion", $"Verzija {version} dokumenta {code} već postoji.");
        if (req.OwnerEmployeeId is { } e && !await db.Employees.AnyAsync(x => x.Id == e, ct))
            return ApiResults.Validation("ownerEmployeeId", "Zaposlenik ne postoji.");
        if (req.FileId is { } f && !await db.FileAttachments.AnyAsync(x => x.Id == f, ct))
            return ApiResults.Validation("fileId", "Fajl ne postoji.");
        return null;
    }

    private static async Task<IResult> Create(SaveDocumentRequest req, AppDbContext db, CancellationToken ct)
    {
        if (await CheckReferences(req, null, db, ct) is { } error)
            return error;
        var doc = new ControlledDocument { Title = req.Title.Trim(), Code = req.Code.Trim(), DocVersion = req.DocVersion.Trim() };
        Apply(doc, req);
        db.ControlledDocuments.Add(doc);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/documents/{doc.Id}", await Project(db).SingleAsync(x => x.Id == doc.Id, ct));
    }

    private static async Task<IResult> Update(Guid id, SaveDocumentRequest req, AppDbContext db, CancellationToken ct)
    {
        var doc = await db.ControlledDocuments.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (doc is null)
            return ApiResults.NotFound("Dokument ne postoji.");
        if (doc.IsVoided)
            return ApiResults.Conflict("Dokument je poništen i ne može se mijenjati.");
        if (!doc.IsEditable)
            return ApiResults.Conflict(doc.Status == DocumentStatus.Odobren
                ? "Odobren dokument je zaključan; mijenja se samo datum sljedeće provjere. Za izmjenu napravi novu verziju."
                : "Povučen dokument je konačan. Za izmjenu napravi novu verziju.");
        if (await CheckReferences(req, id, db, ct) is { } error)
            return error;
        db.Entry(doc).Property(x => x.Version).OriginalValue = req.Version!.Value;
        doc.Title = req.Title.Trim();
        doc.Code = req.Code.Trim();
        doc.DocVersion = req.DocVersion.Trim();
        Apply(doc, req);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Project(db).SingleAsync(x => x.Id == id, ct));
    }

    /// <summary>Approving withdraws the previous approved version of the same Code in the same transaction (DOMAIN §7.4).</summary>
    private static async Task<IResult> ChangeStatus(Guid id, DocumentStatusRequest req, AppDbContext db, IClock clock, AuditContext audit, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var doc = await db.ControlledDocuments.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (doc is null)
            return ApiResults.NotFound("Dokument ne postoji.");
        db.Entry(doc).Property(x => x.Version).OriginalValue = req.Version!.Value;
        if (req.Status == DocumentStatus.Odobren && req.ApprovalDate is { } approvalDate && doc.IsEditable)
            doc.ApprovalDate = approvalDate;
        doc.MoveTo(req.Status!.Value, clock.Today);

        if (req.Status == DocumentStatus.Odobren)
        {
            var previous = await db.ControlledDocuments
                .FromSql($"SELECT *, xmin FROM controlled_document WHERE code = {doc.Code} AND status = 'Odobren' AND NOT is_voided AND id <> {doc.Id} FOR UPDATE")
                .ToListAsync(ct);
            foreach (var p in previous)
                p.WithdrawFor(doc);
            if (previous.Count > 0)
                audit.Reason = $"Odobrena verzija {doc.DocVersion}; prethodna povučena automatski.";
        }
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ApiResults.Conflict($"Druga verzija dokumenta {doc.Code} je upravo odobrena. Osvježi i pokušaj ponovo.");
        }
        await tx.CommitAsync(ct);
        return TypedResults.Ok(await Project(db).SingleAsync(x => x.Id == id, ct));
    }

    private static async Task<IResult> SetNextReview(Guid id, NextReviewRequest req, AppDbContext db, CancellationToken ct)
    {
        var doc = await db.ControlledDocuments.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (doc is null)
            return ApiResults.NotFound("Dokument ne postoji.");
        if (doc.IsVoided || doc.Status == DocumentStatus.Povucen)
            return ApiResults.Conflict("Povučen ili poništen dokument se ne mijenja.");
        db.Entry(doc).Property(x => x.Version).OriginalValue = req.Version!.Value;
        doc.NextReviewDate = req.NextReviewDate;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Project(db).SingleAsync(x => x.Id == id, ct));
    }

    private static async Task<IResult> NewVersion(Guid id, NewVersionRequest req, AppDbContext db, CancellationToken ct)
    {
        var doc = await db.ControlledDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (doc is null)
            return ApiResults.NotFound("Dokument ne postoji.");
        var version = req.DocVersion.Trim();
        if (await db.ControlledDocuments.AnyAsync(x => x.Code == doc.Code && x.DocVersion == version && !x.IsVoided, ct))
            return ApiResults.Validation("docVersion", $"Verzija {version} dokumenta {doc.Code} već postoji.");
        var next = doc.NewVersion(version);
        db.ControlledDocuments.Add(next);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/documents/{next.Id}", await Project(db).SingleAsync(x => x.Id == next.Id, ct));
    }

    private static async Task<IResult> Void(Guid id, VoidRequest req, AppDbContext db, CancellationToken ct)
    {
        var doc = await db.ControlledDocuments.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (doc is null)
            return ApiResults.NotFound("Dokument ne postoji.");
        db.Entry(doc).Property(x => x.Version).OriginalValue = req.Version!.Value;
        doc.Void(req.Reason);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await Project(db).SingleAsync(x => x.Id == id, ct));
    }

    private static void Apply(ControlledDocument d, SaveDocumentRequest req)
    {
        d.Kind = req.Kind!.Value;
        d.OwnerEmployeeId = req.OwnerEmployeeId;
        d.ApprovalDate = req.ApprovalDate;
        d.EffectiveDate = req.EffectiveDate;
        d.NextReviewDate = req.NextReviewDate;
        d.ChangeDescription = string.IsNullOrWhiteSpace(req.ChangeDescription) ? null : req.ChangeDescription.Trim();
        d.FileId = req.FileId;
    }
}
