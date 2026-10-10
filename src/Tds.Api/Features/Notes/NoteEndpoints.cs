using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Notes;

public sealed record NoteDto(Guid Id, string Text, bool IsDone, DateTimeOffset? DoneAt, DateTimeOffset CreatedAt, uint Version);
public sealed record CreateNoteRequest(string Text);
public sealed record SetNoteDoneRequest(bool IsDone, uint Version);

public sealed class CreateNoteValidator : AbstractValidator<CreateNoteRequest>
{
    public CreateNoteValidator() =>
        RuleFor(x => x.Text).NotEmpty().WithMessage("Upiši bilješku.")
            .MaximumLength(Note.TextMax).WithMessage($"Bilješka može imati najviše {Note.TextMax} znakova.");
}

/// <summary>All roles (DOMAIN §8): add, tick, untick. No text edit, no delete.</summary>
public static class NoteEndpoints
{
    public static void MapNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/notes").RequireAuthorization();
        g.MapGet("/", List);
        g.MapPost("/", Create).Validate<CreateNoteRequest>();
        g.MapPost("/{id:guid}/done", SetDone);
    }

    /// <summary>Open notes first (newest first), then done notes of the last 7 days.</summary>
    public static Task<List<NoteDto>> VisibleAsync(AppDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var hideBefore = now - Note.VisibleAfterDone;
        return db.Notes.Where(n => !n.IsDone || n.DoneAt > hideBefore)
            .OrderBy(n => n.IsDone).ThenByDescending(n => n.CreatedAt)
            .Select(n => new NoteDto(n.Id, n.Text, n.IsDone, n.DoneAt, n.CreatedAt, n.Version))
            .ToListAsync(ct);
    }

    private static Task<List<NoteDto>> List(AppDbContext db, IClock clock, CancellationToken ct) => VisibleAsync(db, clock.UtcNow, ct);

    private static async Task<IResult> Create(CreateNoteRequest req, AppDbContext db, CancellationToken ct)
    {
        var note = new Note { Text = req.Text.Trim() };
        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/notes/{note.Id}", new NoteDto(note.Id, note.Text, note.IsDone, note.DoneAt, note.CreatedAt, note.Version));
    }

    private static async Task<IResult> SetDone(Guid id, SetNoteDoneRequest req, AppDbContext db, IClock clock, CancellationToken ct)
    {
        var note = await db.Notes.SingleOrDefaultAsync(n => n.Id == id, ct);
        if (note is null)
            return ApiResults.NotFound("Bilješka ne postoji.");
        db.Entry(note).Property(x => x.Version).OriginalValue = req.Version;
        note.SetDone(req.IsDone, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new NoteDto(note.Id, note.Text, note.IsDone, note.DoneAt, note.CreatedAt, note.Version));
    }
}
