namespace Tds.Api.Domain;

/// <summary>
/// DATA_MODEL §13 (Notion "Notes"). Text never changes and notes are never deleted;
/// done notes disappear from the list 7 days after DoneAt (UI §3).
/// </summary>
public class Note : Entity
{
    public const int TextMax = 300;
    public static readonly TimeSpan VisibleAfterDone = TimeSpan.FromDays(7);

    public required string Text { get; init; }
    public bool IsDone { get; private set; }
    public DateTimeOffset? DoneAt { get; private set; }

    public void SetDone(bool done, DateTimeOffset now)
    {
        if (done == IsDone)
            return;
        IsDone = done;
        DoneAt = done ? now : null;
    }

    public bool IsVisible(DateTimeOffset now) => !IsDone || DoneAt > now - VisibleAfterDone;
}
