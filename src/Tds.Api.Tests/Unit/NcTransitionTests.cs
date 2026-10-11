using Tds.Api.Domain;
using S = Tds.Api.Domain.NcStatus;

namespace Tds.Api.Tests.UnitTests;

/// <summary>DOMAIN §7.3 — every NC transition, forward and back, and every rejecting condition.</summary>
public class NcTransitionTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);

    /// <summary>An NC in the given status with all fields filled, so only the rule under test can fail.</summary>
    private static NonConformity Nc(S status)
    {
        var nc = new NonConformity
        {
            Number = "NC-2026-007", Description = "Rupe Ø12 izvan tolerancije", ProductDecision = ProductDecision.Dorada,
            RootCause = "Istrošena mlaznica lasera", CorrectiveAction = "Zamjena mlaznice, kontrola svakih 500 kom",
            EffectivenessCheckDate = Today.AddDays(-1),
        };
        foreach (var s in new[] { S.RadnjaUToku, S.ProvjeraEfektivnosti, S.Zatvorena })
        {
            if (nc.Status == status) break;
            nc.MoveTo(s, Today);
        }
        Assert.Equal(status, nc.Status);
        return nc;
    }

    public static TheoryData<S, S> Forward => new()
    {
        { S.Otvorena, S.RadnjaUToku },
        { S.RadnjaUToku, S.ProvjeraEfektivnosti },
        { S.ProvjeraEfektivnosti, S.Zatvorena },
    };

    public static TheoryData<S, S> Back => new()
    {
        { S.RadnjaUToku, S.Otvorena },
        { S.ProvjeraEfektivnosti, S.RadnjaUToku },
        { S.Zatvorena, S.ProvjeraEfektivnosti },   // reopening
    };

    public static TheoryData<S, S> Skips => new()
    {
        { S.Otvorena, S.ProvjeraEfektivnosti },
        { S.Otvorena, S.Zatvorena },
        { S.RadnjaUToku, S.Zatvorena },
        { S.Zatvorena, S.Otvorena },
        { S.Zatvorena, S.RadnjaUToku },
        { S.ProvjeraEfektivnosti, S.Otvorena },
        { S.Otvorena, S.Otvorena },
    };

    [Theory]
    [MemberData(nameof(Forward))]
    public void Every_forward_step_with_its_condition_met(S from, S to)
    {
        var nc = Nc(from);
        nc.MoveTo(to, Today);
        Assert.Equal(to, nc.Status);
    }

    [Theory]
    [MemberData(nameof(Back))]
    public void Every_step_back_is_allowed_without_conditions(S from, S to)
    {
        var nc = Nc(from);
        nc.ProductDecision = ProductDecision.CekaOdluku;   // conditions do not matter going back
        nc.RootCause = null;
        nc.EffectivenessCheckDate = null;
        nc.MoveTo(to, Today);
        Assert.Equal(to, nc.Status);
    }

    [Theory]
    [MemberData(nameof(Skips))]
    public void Skipping_steps_is_rejected(S from, S to)
    {
        var nc = Nc(from);
        var ex = Assert.Throws<DomainException>(() => nc.MoveTo(to, Today));
        Assert.EndsWith("nije dozvoljen; ide se korak po korak.", ex.Message);
        Assert.Equal(from, nc.Status);
    }

    [Fact]
    public void RadnjaUToku_needs_a_product_decision()
    {
        var nc = Nc(S.Otvorena);
        nc.ProductDecision = ProductDecision.CekaOdluku;
        Assert.Equal("Prije početka radnje odaberi odluku o proizvodu.", Assert.Throws<DomainException>(() => nc.MoveTo(S.RadnjaUToku, Today)).Message);
    }

    [Theory]
    [InlineData(null, "Zamjena mlaznice")]
    [InlineData("Uzrok", "  ")]
    [InlineData(" ", null)]
    public void ProvjeraEfektivnosti_needs_root_cause_and_corrective_action(string? cause, string? action)
    {
        var nc = Nc(S.RadnjaUToku);
        nc.RootCause = cause;
        nc.CorrectiveAction = action;
        Assert.Equal("Za provjeru efektivnosti popuni analizu uzroka i korektivnu radnju.",
            Assert.Throws<DomainException>(() => nc.MoveTo(S.ProvjeraEfektivnosti, Today)).Message);
    }

    [Fact]
    public void Zatvorena_needs_an_effectiveness_check_date()
    {
        var nc = Nc(S.ProvjeraEfektivnosti);
        nc.EffectivenessCheckDate = null;
        Assert.Equal("Za zatvaranje upiši datum provjere efektivnosti.", Assert.Throws<DomainException>(() => nc.MoveTo(S.Zatvorena, Today)).Message);
    }

    [Fact]
    public void Zatvorena_rejects_a_future_effectiveness_check_date_and_accepts_today()
    {
        var nc = Nc(S.ProvjeraEfektivnosti);
        nc.EffectivenessCheckDate = Today.AddDays(1);
        Assert.Equal("Datum provjere efektivnosti ne može biti u budućnosti.", Assert.Throws<DomainException>(() => nc.MoveTo(S.Zatvorena, Today)).Message);
        nc.EffectivenessCheckDate = Today;
        nc.MoveTo(S.Zatvorena, Today);
        Assert.True(nc.IsReadOnly);
    }

    [Fact]
    public void Voided_nc_cannot_move()
    {
        var nc = Nc(S.Otvorena);
        nc.Void("Dupla prijava");
        Assert.Throws<DomainException>(() => nc.MoveTo(S.RadnjaUToku, Today));
    }

    [Fact]
    public void Options_are_the_neighbours_with_their_checks()
    {
        var nc = Nc(S.RadnjaUToku);
        nc.RootCause = null;
        var options = NcTransitions.Options(nc, Today);
        Assert.Equal([S.ProvjeraEfektivnosti, S.Otvorena], options.Select(o => o.To));
        Assert.False(options[0].Allowed);
        Assert.True(options[1].Allowed);
        Assert.Equal([S.RadnjaUToku], NcTransitions.Options(Nc(S.Otvorena), Today).Select(o => o.To));
        Assert.Equal([S.ProvjeraEfektivnosti], NcTransitions.Options(Nc(S.Zatvorena), Today).Select(o => o.To));
    }

    [Theory]
    [InlineData(InspectionPhase.UlazniMaterijal, NcSource.UlazniMaterijal)]
    [InlineData(InspectionPhase.LaserskoRezanje, NcSource.Laser)]
    [InlineData(InspectionPhase.HidraulicnoPresanje, NcSource.Presanje)]
    [InlineData(InspectionPhase.ZavrsnaKontrola, NcSource.ZavrsnaKontrola)]
    public void Source_proposed_from_check_phase(InspectionPhase phase, NcSource source) =>
        Assert.Equal(source, QualityCheck.SourceFor(phase));
}
