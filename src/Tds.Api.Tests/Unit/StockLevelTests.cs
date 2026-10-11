using Tds.Api.Domain;

namespace Tds.Api.Tests.UnitTests;

/// <summary>DOMAIN §2.3 and §2.4.</summary>
public class StockLevelTests
{
    [Theory]
    [InlineData(0.0, 10.0, StockLevel.Nema)]
    [InlineData(-3.0, 10.0, StockLevel.Nema)]
    [InlineData(0.0, null, StockLevel.Nema)]
    [InlineData(5.0, null, StockLevel.NijeDefinisano)]
    [InlineData(9.999, 10.0, StockLevel.IspodMinimuma)]
    [InlineData(10.0, 10.0, StockLevel.Nisko)]
    [InlineData(14.999, 10.0, StockLevel.Nisko)]
    [InlineData(15.0, 10.0, StockLevel.Ok)]
    [InlineData(100.0, 10.0, StockLevel.Ok)]
    [InlineData(1.0, 0.0, StockLevel.Ok)]
    public void Level_follows_DOMAIN_2_4(double available, double? minimum, StockLevel expected) =>
        Assert.Equal(expected, StockLevels.Level((decimal)available, minimum is null ? null : (decimal)minimum));

    [Fact]
    public void Zero_stock_is_Nema_even_without_minimum_and_NijeDefinisano_needs_stock()
    {
        Assert.Equal(StockLevel.Nema, StockLevels.Level(0m, null));
        Assert.Equal(StockLevel.NijeDefinisano, StockLevels.Level(0.001m, null));
    }

    [Fact]
    public void Missing_minimum_is_never_reported_as_ok() =>
        Assert.NotEqual(StockLevel.Ok, StockLevels.Level(1_000_000m, null));

    [Theory]
    [InlineData(10, 0, 10)]
    [InlineData(10, 4, 6)]
    [InlineData(10, 10, 0)]
    [InlineData(10, 12, 0)]
    [InlineData(0, 3, 0)]
    public void Effective_reservation_is_reduced_by_issued_and_never_negative(decimal reserved, decimal issued, decimal expected) =>
        Assert.Equal(expected, StockLevels.EffectiveReservation(reserved, issued));

    [Theory]
    [InlineData(20.0, 10.0, StockLevel.Ok)]               // known lots already OK: an unknown lot can only add stock
    [InlineData(5.0, null, StockLevel.NijeDefinisano)] // no minimum: does not depend on the amount
    [InlineData(0.0, 10.0, null)]                        // would be Nema
    [InlineData(5.0, 10.0, null)]                        // would be Ispod minimuma
    [InlineData(12.0, 10.0, null)]                       // would be Nisko
    public void With_unknown_lots_only_ok_and_undefined_are_known(double available, double? minimum, StockLevel? expected) =>
        Assert.Equal(expected, StockLevels.Level((decimal)available, (decimal?)minimum, unknownLots: 1));

    [Fact]
    public void Without_unknown_lots_the_level_is_unchanged() =>
        Assert.Equal(StockLevel.IspodMinimuma, StockLevels.Level(5m, 10m, unknownLots: 0));
}
