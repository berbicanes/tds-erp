namespace Tds.Api.Domain;

/// <summary>Facts the gates need (all derived from source records, DOMAIN §3).</summary>
public sealed record GateFacts(
    QualityApproval QualityApproval, bool LaserFirstPieceApproved, bool PressFirstPieceApproved,
    bool FinalInspectionPassed, bool ReadyForShipment);

/// <summary>Result of checking one transition. <see cref="CanOverride"/>: an Admin may pass the failed gate with a reason.</summary>
public sealed record TransitionCheck(WorkOrderStatus To, bool Allowed, string? Reason, bool CanOverride);

/// <summary>DOMAIN §4 — status table and gates. Pure, no I/O. Every row has a unit test.</summary>
public static class WorkOrderTransitions
{
    private static readonly WorkOrderStatus Z = WorkOrderStatus.Zaprimljen, CM = WorkOrderStatus.CekaMaterijal, S = WorkOrderStatus.Spreman,
        L = WorkOrderStatus.Laser, P = WorkOrderStatus.Presanje, K = WorkOrderStatus.Kontrola, D = WorkOrderStatus.Doraditi,
        ZV = WorkOrderStatus.Zavrseno, I = WorkOrderStatus.Isporuceno, O = WorkOrderStatus.Otkazano;

    /// <summary>The table in DOMAIN §4 (Otkazano → Zaprimljen is Admin-only reopening, see <see cref="Check"/>).</summary>
    public static readonly IReadOnlyDictionary<WorkOrderStatus, WorkOrderStatus[]> Table = new Dictionary<WorkOrderStatus, WorkOrderStatus[]>
    {
        [Z] = [CM, S, O],
        [CM] = [S, O],
        [S] = [CM, L, P, O],
        [L] = [P, K, D, O],
        [P] = [K, D, O],
        [K] = [ZV, D, O],
        [D] = [L, P, K, O],
        [ZV] = [I, K],
        [I] = [],
        [O] = [Z],
    };

    /// <summary>"Unazad" (DOMAIN §4): only these two; everything else is "naprijed".</summary>
    public static bool IsBackward(WorkOrderStatus from, WorkOrderStatus to) => (from, to) == (S, CM) || (from, to) == (ZV, K);

    /// <summary>Checks a transition for a user. Admin-only rules: reopening Otkazano and gate overrides.</summary>
    public static TransitionCheck Check(WorkOrderStatus from, WorkOrderStatus to, GateFacts f, bool isAdmin)
    {
        if (from == to || !Table[from].Contains(to))
            return new(to, false, $"Prelaz iz \"{DisplayNames.Of(from)}\" u \"{DisplayNames.Of(to)}\" nije dozvoljen.", false);

        if (from == O)
            return isAdmin
                ? new(to, true, null, false)
                : new(to, false, "Otkazani nalog može ponovo otvoriti samo Admin.", false);

        if (f.QualityApproval == QualityApproval.Blokirano && !IsBackward(from, to) && to is not (WorkOrderStatus.Doraditi or WorkOrderStatus.Otkazano))
            return new(to, false, "Kvalitet je blokiran. Dozvoljen je samo prelaz u Doraditi ili Otkazano.", false);

        if (from == L && to == P && !f.LaserFirstPieceApproved)
            return new(to, false, "Prvi komad sa lasera nije potvrđen.", isAdmin);
        if (from == P && to == K && !f.PressFirstPieceApproved)
            return new(to, false, "Prvi komad sa prese nije potvrđen.", isAdmin);

        if (to == ZV)
        {
            if (!f.FinalInspectionPassed)
                return new(to, false, "Završna kontrola nije prihvaćena.", false);
            if (f.QualityApproval != QualityApproval.Prihvaceno)
                return new(to, false, "Odobrenje kvaliteta nije Prihvaćeno.", false);
        }

        if (to == I && !f.ReadyForShipment)
            return new(to, false, f.FinalInspectionPassed && f.QualityApproval == QualityApproval.Prihvaceno
                ? "Nalog nije spreman za isporuku: gotovih ispravnih komada je manje od naručene količine."
                : "Nalog nije spreman za isporuku: završna kontrola i odobrenje kvaliteta moraju biti prihvaćeni.", false);

        return new(to, true, null, false);
    }

    /// <summary>All targets from the table with their gate result (UI "Promijeni status").</summary>
    public static IReadOnlyList<TransitionCheck> Options(WorkOrderStatus from, GateFacts f, bool isAdmin) =>
        Table[from].Where(to => from != O || isAdmin).Select(to => Check(from, to, f, isAdmin)).ToList();

    /// <summary>
    /// Changes the status or throws. An Admin passes a failed first-piece gate only with <paramref name="reason"/>;
    /// reopening a cancelled order also needs a reason. The reason goes to the AuditLog (DOMAIN §4).
    /// </summary>
    public static void Apply(WorkOrder wo, WorkOrderStatus to, GateFacts f, bool isAdmin, string? reason)
    {
        var check = Check(wo.Status, to, f, isAdmin);
        if (!check.Allowed)
        {
            if (!check.CanOverride)
                throw new DomainException(check.Reason!);
            if (string.IsNullOrWhiteSpace(reason))
                throw new DomainException($"{check.Reason} Za preskakanje uslova upiši razlog.");
        }
        if (wo.Status == O && string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Razlog ponovnog otvaranja je obavezan.");
        wo.Status = to;
    }
}
