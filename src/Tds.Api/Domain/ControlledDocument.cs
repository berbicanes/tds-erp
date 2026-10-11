namespace Tds.Api.Domain;

/// <summary>DATA_MODEL §12. Every version is its own record sharing the Code. Voided, never deleted.</summary>
public class ControlledDocument : VoidableEntity
{
    public const int TitleMax = 200;
    public const int CodeMax = 40;
    public const int DocVersionMax = 10;
    public const int ChangeDescriptionMax = 1000;

    public required string Title { get; set; }
    public required string Code { get; set; }
    public DocumentKind Kind { get; set; }
    /// <summary>DATA_MODEL "Version" (e.g. "02"); named DocVersion in code because Version is the concurrency token.</summary>
    public required string DocVersion { get; set; }
    public DocumentStatus Status { get; private set; } = DocumentStatus.Nacrt;
    public Guid? OwnerEmployeeId { get; set; }
    public DateOnly? ApprovalDate { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? NextReviewDate { get; set; }
    public string? ChangeDescription { get; set; }
    public Guid? FileId { get; set; }

    /// <summary>DOMAIN §7.4: Nacrt and NaPregledu are edited freely; Odobren only NextReviewDate; Povucen nothing.</summary>
    public bool IsEditable => Status is DocumentStatus.Nacrt or DocumentStatus.NaPregledu;

    public void MoveTo(DocumentStatus to, DateOnly today)
    {
        var check = DocumentTransitions.Check(this, to, today);
        if (!check.Allowed)
            throw new DomainException(check.Reason!);
        Status = to;
    }

    /// <summary>Automatic withdrawal when another version of the same Code is approved (DOMAIN §7.4).</summary>
    public void WithdrawFor(ControlledDocument approved)
    {
        if (Status != DocumentStatus.Odobren || approved.Code != Code || approved.Id == Id)
            throw new InvalidOperationException("Only the previous approved version of the same code is withdrawn.");
        Status = DocumentStatus.Povucen;
    }

    /// <summary>New version = new record with the same Code, data copied, status Nacrt (DOMAIN §7.4).</summary>
    public ControlledDocument NewVersion(string docVersion) => new()
    {
        Title = Title, Code = Code, Kind = Kind, DocVersion = docVersion.Trim(), OwnerEmployeeId = OwnerEmployeeId, FileId = FileId,
    };
}

public sealed record DocumentTransitionCheck(DocumentStatus To, bool Allowed, string? Reason);

/// <summary>DOMAIN §7.4 transition table.</summary>
public static class DocumentTransitions
{
    private static readonly Dictionary<DocumentStatus, DocumentStatus[]> Allowed = new()
    {
        [DocumentStatus.Nacrt] = [DocumentStatus.NaPregledu],
        [DocumentStatus.NaPregledu] = [DocumentStatus.Odobren, DocumentStatus.Nacrt],
        [DocumentStatus.Odobren] = [DocumentStatus.Povucen],
        [DocumentStatus.Povucen] = [],
    };

    public static string Display(DocumentStatus s) => s switch
    {
        DocumentStatus.Nacrt => "Nacrt",
        DocumentStatus.NaPregledu => "Na pregledu",
        DocumentStatus.Odobren => "Odobren",
        DocumentStatus.Povucen => "Povučen",
        _ => s.ToString(),
    };

    public static DocumentTransitionCheck Check(ControlledDocument d, DocumentStatus to, DateOnly today)
    {
        if (d.IsVoided)
            return new(to, false, $"Dokument {d.Code} v{d.DocVersion} je poništen.");
        if (!Allowed[d.Status].Contains(to))
            return new(to, false, d.Status == DocumentStatus.Povucen
                ? "Povučen dokument je konačan. Za izmjenu napravi novu verziju."
                : $"Prelaz iz \"{Display(d.Status)}\" u \"{Display(to)}\" nije dozvoljen.");
        if (to == DocumentStatus.Odobren)
        {
            if (d.ApprovalDate is null)
                return new(to, false, "Za odobravanje upiši datum odobrenja.");
            if (d.ApprovalDate > today)
                return new(to, false, "Datum odobrenja ne može biti u budućnosti.");
            if (d.FileId is null)
                return new(to, false, "Za odobravanje priloži fajl dokumenta.");
        }
        return new(to, true, null);
    }

    public static IReadOnlyList<DocumentTransitionCheck> Options(ControlledDocument d, DateOnly today) =>
        Allowed[d.Status].Select(to => Check(d, to, today)).ToList();
}
