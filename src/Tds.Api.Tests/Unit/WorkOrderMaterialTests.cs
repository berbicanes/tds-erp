using Tds.Api.Domain;

namespace Tds.Api.Tests.UnitTests;

/// <summary>DOMAIN §2.3 — reservations.</summary>
public class WorkOrderMaterialTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);

    private static MaterialLot Lot(ReceiptDecision decision)
    {
        var lot = MaterialLot.Receive("H-1", Guid.CreateVersion7(), Guid.CreateVersion7(), SupplierApprovalStatus.Odobren, Today, 10, Today).Lot;
        if (decision != ReceiptDecision.NaKontroli)
            lot.Decide(decision, Guid.CreateVersion7(), "Razlog", 10, Today);
        return lot;
    }

    [Theory]
    [InlineData(ReceiptDecision.NaKontroli)]
    [InlineData(ReceiptDecision.Prihvacen)]
    public void Reservation_is_allowed_on_NaKontroli_and_Prihvacen(ReceiptDecision d) =>
        Assert.Equal(4m, WorkOrderMaterial.Link(Guid.CreateVersion7(), Lot(d), 4m).ReservedQty);

    [Theory]
    [InlineData(ReceiptDecision.Karantin)]
    [InlineData(ReceiptDecision.Odbijen)]
    public void Reservation_is_rejected_on_Karantin_and_Odbijen(ReceiptDecision d) =>
        Assert.Throws<DomainException>(() => WorkOrderMaterial.Link(Guid.CreateVersion7(), Lot(d), 4m));

    [Fact]
    public void Reservation_can_be_reduced_to_zero_even_after_lot_went_to_quarantine()
    {
        var lot = Lot(ReceiptDecision.Prihvacen);
        var link = WorkOrderMaterial.Link(Guid.CreateVersion7(), lot, 5m);
        lot.Decide(ReceiptDecision.Karantin, Guid.CreateVersion7(), null, 10, Today);

        Assert.Throws<DomainException>(() => link.SetReservation(lot, 6m));
        link.SetReservation(lot, 0m);
        Assert.Equal(0m, link.ReservedQty);
    }

    [Fact]
    public void Negative_reservation_is_rejected() =>
        Assert.Equal("Rezervacija ne može biti negativna.",
            Assert.Throws<DomainException>(() => WorkOrderMaterial.Link(Guid.CreateVersion7(), Lot(ReceiptDecision.Prihvacen), -1)).Message);

    [Fact]
    public void Voided_lot_cannot_be_reserved()
    {
        var lot = Lot(ReceiptDecision.NaKontroli);
        lot.Void("Greška");
        Assert.Throws<DomainException>(() => WorkOrderMaterial.Link(Guid.CreateVersion7(), lot, 1m));
    }
}
