namespace Tds.Api.Domain;

/// <summary>DATA_MODEL §3. Daily production record. Voided, never deleted; voiding also voids its Izdavanje.</summary>
public class ProductionEntry : VoidableEntity
{
    public const int NumberMax = 24;
    public const int NoteMax = 1000;
    public const decimal MaxLaborHours = 24m;

    public required string Number { get; init; }
    public DateOnly Date { get; set; }
    public Guid WorkOrderId { get; init; }
    public Guid OperatorEmployeeId { get; set; }
    public Guid? MaterialLotId { get; set; }
    public int LaserCutQty { get; set; }
    public int PressedQty { get; set; }
    public int GoodQty { get; set; }
    public int ScrapQty { get; set; }
    public decimal LaborHours { get; set; }
    public decimal? MaterialConsumed { get; set; }
    public string? Note { get; set; }

    public List<ProductionEntryEquipment> Equipment { get; init; } = [];
}

/// <summary>
/// Join row production_entry_equipment (DATA_MODEL §3). Technical link without audit fields; changes are
/// written to the parent entry's AuditLog as "EquipmentIds" (see <see cref="IAuditedLink"/>).
/// </summary>
public class ProductionEntryEquipment : IAuditedLink
{
    public Guid ProductionEntryId { get; init; }
    public Guid EquipmentId { get; init; }

    Guid IAuditedLink.ParentId => ProductionEntryId;
    string IAuditedLink.ParentType => nameof(ProductionEntry);
    string IAuditedLink.Field => "EquipmentIds";
    Guid IAuditedLink.ValueId => EquipmentId;
}

/// <summary>An M:N row whose changes are audited on its parent entity as one set field.</summary>
public interface IAuditedLink
{
    Guid ParentId { get; }
    string ParentType { get; }
    string Field { get; }
    Guid ValueId { get; }
}

/// <summary>Everything the rules in DOMAIN §5 need, loaded by the endpoint.</summary>
public sealed record ProductionEntryInput(
    DateOnly Date, int LaserCutQty, int PressedQty, int GoodQty, int ScrapQty, decimal LaborHours, decimal? MaterialConsumed,
    int MachineCount, bool AllMachinesActive);

public sealed record LotUse(string LotNumber, ReceiptDecision Decision, bool IsVoided, decimal OnHand, Unit Unit);

public sealed record OrderFacts(string Number, WorkOrderStatus Status, bool FinalInspectionPassed, int OrderedQty, int GoodQtyOfOtherEntries);

/// <summary>DOMAIN §5 — pure checks. Throws <see cref="DomainException"/> with a Bosnian message; returns warnings (§5.5).</summary>
public static class ProductionEntryRules
{
    public static readonly WorkOrderStatus[] ProductionStatuses =
        [WorkOrderStatus.Spreman, WorkOrderStatus.Laser, WorkOrderStatus.Presanje, WorkOrderStatus.Kontrola, WorkOrderStatus.Doraditi];

    /// <param name="lot">The chosen lot, or null.</param>
    /// <param name="onHandAvailableForThisEntry">Lot OnHand plus what this entry already consumed (when editing).</param>
    public static IReadOnlyList<string> Check(
        ProductionEntryInput e, OrderFacts order, LotUse? lot, decimal onHandAvailableForThisEntry,
        DateOnly today, bool isAdmin)
    {
        // 5.1
        if (!ProductionStatuses.Contains(order.Status))
            throw new DomainException($"Na nalogu {order.Number} sa statusom {DisplayNames.Of(order.Status)} se ne može evidentirati proizvodnja.");

        if (e.LaserCutQty < 0 || e.PressedQty < 0 || e.GoodQty < 0 || e.ScrapQty < 0)
            throw new DomainException("Količine ne mogu biti negativne.");
        if (e.LaserCutQty + e.PressedQty + e.GoodQty + e.ScrapQty == 0)
            throw new DomainException("Unesi barem jednu količinu: laser, presa, gotovo ili škart.");
        if (e.LaborHours < 0 || e.LaborHours > ProductionEntry.MaxLaborHours)
            throw new DomainException("Radni sati moraju biti između 0 i 24.");
        if (e.MaterialConsumed is <= 0)
            throw new DomainException("Utrošak materijala mora biti veći od 0 ili prazan.");

        // Equipment: at least one active machine (DATA_MODEL §3).
        if (e.MachineCount == 0)
            throw new DomainException("Odaberi barem jednu mašinu.");
        if (!e.AllMachinesActive)
            throw new DomainException("Odabrana oprema mora biti aktivna mašina.");

        // 5.4
        if (e.Date > today)
            throw new DomainException("Datum ne može biti u budućnosti.");
        if (e.Date < today.AddDays(-7) && !isAdmin)
            throw new DomainException("Datum ne smije biti stariji od 7 dana. Starije zapise unosi Admin.");

        // 5.2 + golden rule 3 + 2.5
        var needsLot = e.LaserCutQty > 0 || e.MaterialConsumed is > 0;
        if (needsLot && lot is null)
            throw new DomainException("Odaberi lot materijala — obavezan je kad se reže ili troši materijal.");
        if (lot is not null)
        {
            if (lot.IsVoided)
                throw new DomainException($"Lot {lot.LotNumber} je poništen.");
            if (lot.Decision != ReceiptDecision.Prihvacen)
                throw new DomainException($"Lot {lot.LotNumber} nije prihvaćen ({DisplayNames.Of(lot.Decision)}) i ne smije se koristiti.");
            if (e.MaterialConsumed is { } c && c > onHandAvailableForThisEntry)
                throw new DomainException($"Na lotu ima samo {Stock.Format(onHandAvailableForThisEntry)} {lot.Unit}.");
        }

        // 5.3 — client rule, literal from Notion.
        if (e.GoodQty > 0 && !order.FinalInspectionPassed)
            throw new DomainException("Gotovi ispravni komadi se upisuju tek nakon prihvaćene završne kontrole.");

        // 5.5 — warning only.
        var totalGood = order.GoodQtyOfOtherEntries + e.GoodQty;
        return totalGood > order.OrderedQty
            ? [$"Ukupno gotovih ispravnih komada ({totalGood}) je veće od naručene količine ({order.OrderedQty})."]
            : [];
    }

    /// <summary>5.6: author on the day of creation (Europe/Sarajevo), Kontrolor/Admin always.</summary>
    public static bool CanChange(Guid? createdBy, DateOnly createdOnLocal, Guid? userId, bool isKontrolorOrAdmin, DateOnly today) =>
        isKontrolorOrAdmin || (userId is not null && createdBy == userId && createdOnLocal == today);

    /// <summary>The Izdavanje movement for a consumption (DOMAIN §2.2).</summary>
    public static StockMovement Issue(ProductionEntry entry, Guid lotId, decimal consumed) => new()
    {
        MaterialLotId = lotId,
        Type = StockMovementType.Izdavanje,
        Quantity = -consumed,
        Date = entry.Date,
        ProductionEntryId = entry.Id,
        WorkOrderId = entry.WorkOrderId,
    };
}
