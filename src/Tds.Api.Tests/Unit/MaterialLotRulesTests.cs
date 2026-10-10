using Tds.Api.Domain;

namespace Tds.Api.Tests.UnitTests;

/// <summary>DOMAIN §2.2, §2.5a, §7.2.</summary>
public class MaterialLotRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);
    private static readonly Guid Inspector = Guid.CreateVersion7();

    private static MaterialLot NewLot(decimal qty = 10m) =>
        MaterialLot.Receive("H-4471", Guid.CreateVersion7(), Guid.CreateVersion7(), SupplierApprovalStatus.Odobren, Today, qty, Today).Lot;

    [Fact]
    public void Receive_creates_lot_on_NaKontroli_and_a_Prijem_movement_for_the_full_quantity()
    {
        var (lot, receipt) = MaterialLot.Receive(" H-4471 ", Guid.CreateVersion7(), Guid.CreateVersion7(),
            SupplierApprovalStatus.Odobren, Today.AddDays(-1), 12.5m, Today);

        Assert.Equal("H-4471", lot.LotNumber);
        Assert.Equal(ReceiptDecision.NaKontroli, lot.ReceiptDecision);
        Assert.Equal(StockMovementType.Prijem, receipt.Type);
        Assert.Equal(12.5m, receipt.Quantity);
        Assert.Equal(lot.Id, receipt.MaterialLotId);
        Assert.Equal(Today.AddDays(-1), receipt.Date);
    }

    [Fact]
    public void Blocked_supplier_is_rejected() =>
        Assert.Equal("Dobavljač je blokiran.", Assert.Throws<DomainException>(() =>
            MaterialLot.Receive("H-1", Guid.CreateVersion7(), Guid.CreateVersion7(), SupplierApprovalStatus.Blokiran, Today, 1, Today)).Message);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_quantity_is_rejected(decimal qty) =>
        Assert.Throws<DomainException>(() =>
            MaterialLot.Receive("H-1", Guid.CreateVersion7(), Guid.CreateVersion7(), SupplierApprovalStatus.Odobren, Today, qty, Today));

    [Fact]
    public void Future_receipt_date_is_rejected() =>
        Assert.Throws<DomainException>(() =>
            MaterialLot.Receive("H-1", Guid.CreateVersion7(), Guid.CreateVersion7(), SupplierApprovalStatus.Odobren, Today.AddDays(1), 1, Today));

    public static TheoryData<ReceiptDecision, ReceiptDecision> Allowed => new()
    {
        { ReceiptDecision.NaKontroli, ReceiptDecision.Prihvacen },
        { ReceiptDecision.NaKontroli, ReceiptDecision.Karantin },
        { ReceiptDecision.NaKontroli, ReceiptDecision.Odbijen },
        { ReceiptDecision.Karantin, ReceiptDecision.Prihvacen },
        { ReceiptDecision.Karantin, ReceiptDecision.Odbijen },
        { ReceiptDecision.Prihvacen, ReceiptDecision.Karantin },
    };

    public static TheoryData<ReceiptDecision, ReceiptDecision> Forbidden => new()
    {
        { ReceiptDecision.Prihvacen, ReceiptDecision.Odbijen },
        { ReceiptDecision.Prihvacen, ReceiptDecision.NaKontroli },
        { ReceiptDecision.Karantin, ReceiptDecision.NaKontroli },
        { ReceiptDecision.Odbijen, ReceiptDecision.Prihvacen },
        { ReceiptDecision.Odbijen, ReceiptDecision.Karantin },
        { ReceiptDecision.Odbijen, ReceiptDecision.NaKontroli },
    };

    private static MaterialLot LotWithDecision(ReceiptDecision d)
    {
        var lot = NewLot();
        if (d == ReceiptDecision.Karantin || d == ReceiptDecision.Odbijen || d == ReceiptDecision.Prihvacen)
            lot.Decide(d, Inspector, "razlog", 10m, Today);
        return lot;
    }

    [Theory]
    [MemberData(nameof(Allowed))]
    public void Allowed_decision_changes(ReceiptDecision from, ReceiptDecision to)
    {
        var lot = LotWithDecision(from);
        lot.Decide(to, Inspector, "Razlog", 10m, Today);
        Assert.Equal(to, lot.ReceiptDecision);
        Assert.Equal(Inspector, lot.InspectedByEmployeeId);
    }

    [Theory]
    [MemberData(nameof(Forbidden))]
    public void Forbidden_decision_changes_are_rejected(ReceiptDecision from, ReceiptDecision to)
    {
        var lot = LotWithDecision(from);
        Assert.Throws<DomainException>(() => lot.Decide(to, Inspector, "Razlog", 10m, Today));
        Assert.Equal(from, lot.ReceiptDecision);
    }

    [Fact]
    public void Forbidden_message_uses_display_names() =>
        Assert.Equal("Odluka prijema se ne može promijeniti iz \"Prihvaćen\" u \"Na kontroli\".",
            Assert.Throws<DomainException>(() => LotWithDecision(ReceiptDecision.Prihvacen).Decide(ReceiptDecision.NaKontroli, Inspector, null, 10, Today)).Message);

    [Theory]
    [InlineData(ReceiptDecision.Prihvacen)]
    [InlineData(ReceiptDecision.Karantin)]
    [InlineData(ReceiptDecision.Odbijen)]
    public void Decision_requires_inspector(ReceiptDecision to) =>
        Assert.Equal("Odaberi ko je kontrolisao lot.",
            Assert.Throws<DomainException>(() => NewLot().Decide(to, null, "Razlog", 10, Today)).Message);

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Odbijen_requires_reason(string? reason) =>
        Assert.Equal("Razlog odbijanja je obavezan.",
            Assert.Throws<DomainException>(() => NewLot().Decide(ReceiptDecision.Odbijen, Inspector, reason, 10, Today)).Message);

    [Fact]
    public void Odbijen_writes_off_the_whole_remaining_stock()
    {
        var lot = NewLot(10m);
        var otpis = lot.Decide(ReceiptDecision.Odbijen, Inspector, " Pogrešan kvalitet na certifikatu ", onHand: 7.25m, Today);

        Assert.NotNull(otpis);
        Assert.Equal(StockMovementType.Otpis, otpis.Type);
        Assert.Equal(-7.25m, otpis.Quantity);
        Assert.Equal("Pogrešan kvalitet na certifikatu", otpis.Reason);
        Assert.Equal(Today, otpis.Date);
        Assert.Equal(lot.Id, otpis.MaterialLotId);
    }

    [Fact]
    public void Odbijen_with_nothing_left_writes_nothing() =>
        Assert.Null(NewLot().Decide(ReceiptDecision.Odbijen, Inspector, "Razlog", onHand: 0m, Today));

    [Theory]
    [InlineData(ReceiptDecision.Prihvacen)]
    [InlineData(ReceiptDecision.Karantin)]
    public void Other_decisions_write_no_movement(ReceiptDecision to) =>
        Assert.Null(NewLot().Decide(to, Inspector, null, 10, Today));

    [Fact]
    public void Same_decision_again_is_rejected()
    {
        var lot = NewLot();
        lot.Decide(ReceiptDecision.Karantin, Inspector, null, 10, Today);
        Assert.Throws<DomainException>(() => lot.Decide(ReceiptDecision.Karantin, Inspector, null, 10, Today));
    }

    [Fact]
    public void Voided_lot_cannot_be_decided()
    {
        var lot = NewLot();
        lot.Void("Pogrešan unos");
        Assert.Throws<DomainException>(() => lot.Decide(ReceiptDecision.Prihvacen, Inspector, null, 10, Today));
    }

    [Fact]
    public void Warnings_for_unapproved_supplier_and_missing_certificate()
    {
        var lot = NewLot();
        lot.Decide(ReceiptDecision.Prihvacen, Inspector, null, 10, Today);

        Assert.Equal(
            ["Dobavljač Metalia nije potpuno odobren (Uslovno odobren).", "Lot H-4471 je prihvaćen bez certifikata materijala."],
            lot.Warnings("Metalia", SupplierApprovalStatus.UslovnoOdobren));

        lot.CertificateFileId = Guid.CreateVersion7();
        Assert.Empty(lot.Warnings("Metalia", SupplierApprovalStatus.Odobren));
    }

    [Fact]
    public void OnHand_sums_only_non_voided_movements()
    {
        var a = new StockMovement { Quantity = 10m };
        var b = new StockMovement { Quantity = -2.5m };
        var c = new StockMovement { Quantity = -4m };
        c.Void("Greška");
        Assert.Equal(7.5m, Stock.OnHand([a, b, c]));
    }
}
