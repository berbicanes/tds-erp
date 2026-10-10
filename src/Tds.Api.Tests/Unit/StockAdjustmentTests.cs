using Tds.Api.Domain;

namespace Tds.Api.Tests.UnitTests;

/// <summary>DOMAIN §2.2 — manual Korekcija and Otpis.</summary>
public class StockAdjustmentTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);

    private static MaterialLot Lot() =>
        MaterialLot.Receive("H-1", Guid.CreateVersion7(), Guid.CreateVersion7(), SupplierApprovalStatus.Odobren, Today, 10, Today).Lot;

    [Theory]
    [InlineData(2.5)]
    [InlineData(-4)]
    [InlineData(-10)]
    public void Correction_in_both_directions_down_to_zero(decimal qty)
    {
        var m = Stock.Correction(Lot(), qty, " Popis 10/2026 ", Today, onHand: 10m, Today);
        Assert.Equal(StockMovementType.Korekcija, m.Type);
        Assert.Equal(qty, m.Quantity);
        Assert.Equal("Popis 10/2026", m.Reason);
    }

    [Fact]
    public void Correction_below_zero_is_rejected() =>
        Assert.Equal("Korekcija bi spustila stanje lota H-1 ispod 0 (na stanju je 3,5).",
            Assert.Throws<DomainException>(() => Stock.Correction(Lot(), -3.501m, "Popis", Today, 3.5m, Today)).Message);

    [Fact]
    public void Zero_correction_is_rejected() =>
        Assert.Throws<DomainException>(() => Stock.Correction(Lot(), 0, "Popis", Today, 10, Today));

    [Fact]
    public void WriteOff_is_negative_and_at_most_onhand()
    {
        var m = Stock.WriteOff(Lot(), 10m, "Ostatak neupotrebljiv", Today.AddDays(-2), onHand: 10m, Today);
        Assert.Equal(StockMovementType.Otpis, m.Type);
        Assert.Equal(-10m, m.Quantity);
        Assert.Equal(Today.AddDays(-2), m.Date);
    }

    [Fact]
    public void WriteOff_above_onhand_is_rejected_with_quantity_in_message() =>
        Assert.Equal("Na lotu H-1 ima samo 1.234,5.",
            Assert.Throws<DomainException>(() => Stock.WriteOff(Lot(), 1234.6m, "Otpis", Today, 1234.5m, Today)).Message);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WriteOff_must_be_positive(decimal qty) =>
        Assert.Throws<DomainException>(() => Stock.WriteOff(Lot(), qty, "Otpis", Today, 10, Today));

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Reason_is_required(string? reason)
    {
        Assert.Equal("Razlog je obavezan.", Assert.Throws<DomainException>(() => Stock.Correction(Lot(), 1, reason, Today, 10, Today)).Message);
        Assert.Equal("Razlog je obavezan.", Assert.Throws<DomainException>(() => Stock.WriteOff(Lot(), 1, reason, Today, 10, Today)).Message);
    }

    [Fact]
    public void Future_date_is_rejected() =>
        Assert.Equal("Datum pokreta ne može biti u budućnosti.",
            Assert.Throws<DomainException>(() => Stock.WriteOff(Lot(), 1, "Otpis", Today.AddDays(1), 10, Today)).Message);

    [Fact]
    public void Voided_lot_is_rejected()
    {
        var lot = Lot();
        lot.Void("Greška");
        Assert.Throws<DomainException>(() => Stock.Correction(lot, 1, "Popis", Today, 10, Today));
    }
}
