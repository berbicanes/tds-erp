using Tds.Api.Domain;

namespace Tds.Api.Tests.UnitTests;

/// <summary>DOMAIN §5 — every rule with its rejecting case.</summary>
public class ProductionEntryRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);

    private static ProductionEntryInput Input(
        int laser = 0, int pressed = 10, int good = 0, int scrap = 0, decimal hours = 7.5m, decimal? consumed = null,
        DateOnly? date = null, int machines = 1, bool active = true) =>
        new(date ?? Today, laser, pressed, good, scrap, hours, consumed, machines, active);

    private static OrderFacts Order(WorkOrderStatus status = WorkOrderStatus.Presanje, bool final = false, int ordered = 100, int otherGood = 0) =>
        new("RN-2026-0042", status, final, ordered, otherGood);

    private static readonly LotUse AcceptedLot = new("H-4471", ReceiptDecision.Prihvacen, false, 10m, Unit.tabla);

    [Fact]
    public void Consumption_from_a_lot_with_unknown_stock_is_rejected_cutting_without_consumption_is_not()
    {
        var unknown = AcceptedLot with { LotNumber = "AL-77", OnHand = 0m, StockKnown = false };
        Assert.Equal("Stanje lota AL-77 nije poznato. Prvo upiši stvarno stanje korekcijom.", Reject(Input(laser: 5, consumed: 1m), lot: unknown, onHand: 0m));
        Assert.Empty(ProductionEntryRules.Check(Input(laser: 5), Order(), unknown, 0m, Today, false));
    }

    private static string Reject(ProductionEntryInput e, OrderFacts? o = null, LotUse? lot = null, decimal onHand = 10m, bool admin = false) =>
        Assert.Throws<DomainException>(() => ProductionEntryRules.Check(e, o ?? Order(), lot, onHand, Today, admin)).Message;

    [Fact]
    public void Valid_press_entry_without_lot_passes() =>
        Assert.Empty(ProductionEntryRules.Check(Input(), Order(), null, 0, Today, false));

    // 5.1
    [Theory]
    [InlineData(WorkOrderStatus.Spreman)]
    [InlineData(WorkOrderStatus.Laser)]
    [InlineData(WorkOrderStatus.Presanje)]
    [InlineData(WorkOrderStatus.Kontrola)]
    [InlineData(WorkOrderStatus.Doraditi)]
    public void Allowed_order_statuses(WorkOrderStatus s) =>
        Assert.Empty(ProductionEntryRules.Check(Input(), Order(s), null, 0, Today, false));

    [Theory]
    [InlineData(WorkOrderStatus.Zaprimljen, "Zaprimljen")]
    [InlineData(WorkOrderStatus.CekaMaterijal, "Čeka materijal")]
    [InlineData(WorkOrderStatus.Zavrseno, "Završeno")]
    [InlineData(WorkOrderStatus.Isporuceno, "Isporučeno")]
    [InlineData(WorkOrderStatus.Otkazano, "Otkazano")]
    public void Other_statuses_are_rejected_with_message(WorkOrderStatus s, string display) =>
        Assert.Equal($"Na nalogu RN-2026-0042 sa statusom {display} se ne može evidentirati proizvodnja.", Reject(Input(), Order(s)));

    // Quantities, hours, machines
    [Fact]
    public void At_least_one_quantity_is_required() =>
        Assert.Equal("Unesi barem jednu količinu: laser, presa, gotovo ili škart.", Reject(Input(pressed: 0)));

    [Fact]
    public void Negative_quantity_is_rejected() => Reject(Input(scrap: -1));

    [Theory]
    [InlineData(-0.5)]
    [InlineData(24.25)]
    public void Labor_hours_between_0_and_24(double hours) =>
        Assert.Equal("Radni sati moraju biti između 0 i 24.", Reject(Input(hours: (decimal)hours)));

    [Fact]
    public void At_least_one_machine() => Assert.Equal("Odaberi barem jednu mašinu.", Reject(Input(machines: 0)));

    [Fact]
    public void Inactive_or_non_machine_equipment_is_rejected() =>
        Assert.Equal("Odabrana oprema mora biti aktivna mašina.", Reject(Input(active: false)));

    // 5.2 + 2.5
    [Fact]
    public void Laser_cut_requires_a_lot() =>
        Assert.Equal("Odaberi lot materijala — obavezan je kad se reže ili troši materijal.", Reject(Input(laser: 5, pressed: 0)));

    [Fact]
    public void Consumption_requires_a_lot() => Reject(Input(consumed: 1.5m));

    [Theory]
    [InlineData(ReceiptDecision.NaKontroli, "Na kontroli")]
    [InlineData(ReceiptDecision.Karantin, "Karantin")]
    [InlineData(ReceiptDecision.Odbijen, "Odbijen")]
    public void Only_accepted_lot_may_be_used(ReceiptDecision d, string display) =>
        Assert.Equal($"Lot H-4471 nije prihvaćen ({display}) i ne smije se koristiti.",
            Reject(Input(laser: 5, pressed: 0), lot: AcceptedLot with { Decision = d }));

    [Fact]
    public void Consumption_above_onhand_is_rejected() =>
        Assert.Equal("Na lotu ima samo 2,5 tabla.", Reject(Input(laser: 5, consumed: 3m), lot: AcceptedLot, onHand: 2.5m));

    [Fact]
    public void Consumption_equal_to_onhand_passes() =>
        Assert.Empty(ProductionEntryRules.Check(Input(laser: 5, consumed: 2.5m), Order(), AcceptedLot, 2.5m, Today, false));

    [Fact]
    public void Voided_lot_is_rejected() => Reject(Input(laser: 1), lot: AcceptedLot with { IsVoided = true });

    // 5.3
    [Fact]
    public void Good_pieces_need_accepted_final_inspection() =>
        Assert.Equal("Gotovi ispravni komadi se upisuju tek nakon prihvaćene završne kontrole.", Reject(Input(good: 5), Order(final: false)));

    [Fact]
    public void Good_pieces_allowed_after_final_inspection() =>
        Assert.Empty(ProductionEntryRules.Check(Input(good: 5), Order(WorkOrderStatus.Kontrola, final: true), null, 0, Today, false));

    // 5.4
    [Fact]
    public void Future_date_is_rejected_even_for_admin() =>
        Assert.Equal("Datum ne može biti u budućnosti.", Reject(Input(date: Today.AddDays(1)), admin: true));

    [Fact]
    public void Older_than_7_days_is_rejected_except_admin()
    {
        Assert.Empty(ProductionEntryRules.Check(Input(date: Today.AddDays(-7)), Order(), null, 0, Today, false));
        Assert.Equal("Datum ne smije biti stariji od 7 dana. Starije zapise unosi Admin.", Reject(Input(date: Today.AddDays(-8))));
        Assert.Empty(ProductionEntryRules.Check(Input(date: Today.AddDays(-30)), Order(), null, 0, Today, true));
    }

    // 5.5
    [Fact]
    public void Warning_when_good_total_exceeds_ordered()
    {
        var w = ProductionEntryRules.Check(Input(good: 30), Order(WorkOrderStatus.Kontrola, final: true, ordered: 100, otherGood: 80), null, 0, Today, false);
        Assert.Equal(["Ukupno gotovih ispravnih komada (110) je veće od naručene količine (100)."], w);
        Assert.Empty(ProductionEntryRules.Check(Input(good: 20), Order(WorkOrderStatus.Kontrola, final: true, ordered: 100, otherGood: 80), null, 0, Today, false));
    }

    // 5.6
    [Fact]
    public void Author_may_change_on_creation_day_only_kontrolor_always()
    {
        var author = Guid.CreateVersion7();
        Assert.True(ProductionEntryRules.CanChange(author, Today, author, false, Today));
        Assert.False(ProductionEntryRules.CanChange(author, Today.AddDays(-1), author, false, Today));
        Assert.False(ProductionEntryRules.CanChange(author, Today, Guid.CreateVersion7(), false, Today));
        Assert.True(ProductionEntryRules.CanChange(author, Today.AddDays(-30), Guid.CreateVersion7(), true, Today));
        Assert.False(ProductionEntryRules.CanChange(null, Today, null, false, Today));
    }

    [Fact]
    public void Issue_movement_is_negative_and_traced_to_entry_and_order()
    {
        var entry = new ProductionEntry { Number = "DP-20261012-01", Date = Today, WorkOrderId = Guid.CreateVersion7() };
        var lot = Guid.CreateVersion7();
        var m = ProductionEntryRules.Issue(entry, lot, 2.5m);
        Assert.Equal((StockMovementType.Izdavanje, -2.5m, Today, entry.Id, entry.WorkOrderId, lot),
            (m.Type, m.Quantity, m.Date, m.ProductionEntryId!.Value, m.WorkOrderId!.Value, m.MaterialLotId));
    }
}
