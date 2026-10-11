namespace Tds.Api.Domain;

/// <summary>DATA_MODEL §11. Non-conformity and corrective action. Voided, never deleted.</summary>
public class NonConformity : VoidableEntity
{
    public const int NumberMax = 20;
    public const int TextMax = 2000;

    public required string Number { get; init; }
    public Guid? WorkOrderId { get; set; }
    public Guid? MaterialLotId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? QualityCheckId { get; init; }
    public NcSource Source { get; set; }
    public required string Description { get; set; }
    public string? Containment { get; set; }
    public ProductDecision ProductDecision { get; set; } = ProductDecision.CekaOdluku;
    public string? RootCause { get; set; }
    public string? CorrectiveAction { get; set; }
    public Guid? ActionOwnerEmployeeId { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? EffectivenessCheckDate { get; set; }
    public NcStatus Status { get; private set; } = NcStatus.Otvorena;

    public List<NonConformityEvidence> Evidence { get; init; } = [];

    /// <summary>DOMAIN §7.3: editable in every status except Zatvorena.</summary>
    public bool IsReadOnly => Status == NcStatus.Zatvorena;

    /// <summary>Changes the status or throws with the reason (DOMAIN §7.3).</summary>
    public void MoveTo(NcStatus to, DateOnly today)
    {
        var check = NcTransitions.Check(this, to, today);
        if (!check.Allowed)
            throw new DomainException(check.Reason!);
        Status = to;
    }
}

public sealed record NcTransitionCheck(NcStatus To, bool Allowed, string? Reason);

/// <summary>DOMAIN §7.3 — Otvorena → RadnjaUToku → ProvjeraEfektivnosti → Zatvorena, one step forward or back.</summary>
public static class NcTransitions
{
    private static readonly NcStatus[] Order = [NcStatus.Otvorena, NcStatus.RadnjaUToku, NcStatus.ProvjeraEfektivnosti, NcStatus.Zatvorena];

    public static string Display(NcStatus s) => s switch
    {
        NcStatus.Otvorena => "Otvorena",
        NcStatus.RadnjaUToku => "Radnja u toku",
        NcStatus.ProvjeraEfektivnosti => "Provjera efektivnosti",
        NcStatus.Zatvorena => "Zatvorena",
        _ => s.ToString(),
    };

    public static NcTransitionCheck Check(NonConformity nc, NcStatus to, DateOnly today)
    {
        if (nc.IsVoided)
            return new(to, false, $"NC {nc.Number} je poništena.");
        var from = Array.IndexOf(Order, nc.Status);
        var target = Array.IndexOf(Order, to);
        if (Math.Abs(target - from) != 1)
            return new(to, false, $"Prelaz iz \"{Display(nc.Status)}\" u \"{Display(to)}\" nije dozvoljen; ide se korak po korak.");
        if (target < from)
            return new(to, true, null); // one step back is always allowed (Kontrolor/Admin)

        return to switch
        {
            NcStatus.RadnjaUToku when nc.ProductDecision == ProductDecision.CekaOdluku =>
                new(to, false, "Prije početka radnje odaberi odluku o proizvodu."),
            NcStatus.ProvjeraEfektivnosti when string.IsNullOrWhiteSpace(nc.RootCause) || string.IsNullOrWhiteSpace(nc.CorrectiveAction) =>
                new(to, false, "Za provjeru efektivnosti popuni analizu uzroka i korektivnu radnju."),
            NcStatus.Zatvorena when nc.EffectivenessCheckDate is null =>
                new(to, false, "Za zatvaranje upiši datum provjere efektivnosti."),
            NcStatus.Zatvorena when nc.EffectivenessCheckDate > today =>
                new(to, false, "Datum provjere efektivnosti ne može biti u budućnosti."),
            _ => new(to, true, null),
        };
    }

    /// <summary>Neighbouring statuses with their check (UI "Promijeni status").</summary>
    public static IReadOnlyList<NcTransitionCheck> Options(NonConformity nc, DateOnly today)
    {
        var i = Array.IndexOf(Order, nc.Status);
        return new[] { i + 1, i - 1 }.Where(j => j >= 0 && j < Order.Length).Select(j => Check(nc, Order[j], today)).ToList();
    }
}
