using Tds.Api.Domain;

namespace Tds.Api.Tests.Unit;

public class VoidableEntityTests
{
    private sealed class Thing : VoidableEntity;

    [Fact]
    public void Void_sets_flag_and_trimmed_reason()
    {
        var t = new Thing();
        t.Void("  Pogrešan unos ");
        Assert.True(t.IsVoided);
        Assert.Equal("Pogrešan unos", t.VoidedReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Void_without_reason_is_rejected(string reason)
    {
        var ex = Assert.Throws<DomainException>(() => new Thing().Void(reason));
        Assert.Equal("Razlog poništavanja je obavezan.", ex.Message);
    }

    [Fact]
    public void Void_twice_is_rejected()
    {
        var t = new Thing();
        t.Void("Pogrešan unos");
        Assert.Throws<DomainException>(() => t.Void("Opet"));
    }
}
