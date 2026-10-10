using Tds.Api.Domain;
using S = Tds.Api.Domain.WorkOrderStatus;

namespace Tds.Api.Tests.Unit;

/// <summary>DOMAIN §4 — one test per row of the transition table, forbidden transitions, every gate (pass + reject).</summary>
public class WorkOrderTransitionTests
{
    /// <summary>All gates pass, quality Prihvaceno.</summary>
    private static readonly GateFacts AllOk = new(QualityApproval.Prihvaceno, true, true, true, true);
    private static readonly GateFacts NothingDone = new(QualityApproval.Ceka, false, false, false, false);

    private static WorkOrder Wo(S status) => new() { Number = "RN-2026-0001", Customer = "Metalbau AG", ProductDescription = "Nosač", Status = status };

    // ---- Every allowed row of the table (25) ----
    public static TheoryData<S, S> AllowedRows => new()
    {
        { S.Zaprimljen, S.CekaMaterijal }, { S.Zaprimljen, S.Spreman }, { S.Zaprimljen, S.Otkazano },
        { S.CekaMaterijal, S.Spreman }, { S.CekaMaterijal, S.Otkazano },
        { S.Spreman, S.CekaMaterijal }, { S.Spreman, S.Laser }, { S.Spreman, S.Presanje }, { S.Spreman, S.Otkazano },
        { S.Laser, S.Presanje }, { S.Laser, S.Kontrola }, { S.Laser, S.Doraditi }, { S.Laser, S.Otkazano },
        { S.Presanje, S.Kontrola }, { S.Presanje, S.Doraditi }, { S.Presanje, S.Otkazano },
        { S.Kontrola, S.Zavrseno }, { S.Kontrola, S.Doraditi }, { S.Kontrola, S.Otkazano },
        { S.Doraditi, S.Laser }, { S.Doraditi, S.Presanje }, { S.Doraditi, S.Kontrola }, { S.Doraditi, S.Otkazano },
        { S.Zavrseno, S.Isporuceno }, { S.Zavrseno, S.Kontrola },
    };

    [Theory]
    [MemberData(nameof(AllowedRows))]
    public void Allowed_row_with_all_gates_passing(S from, S to)
    {
        var wo = Wo(from);
        WorkOrderTransitions.Apply(wo, to, AllOk, isAdmin: false, reason: null);
        Assert.Equal(to, wo.Status);
    }

    [Fact]
    public void Table_has_exactly_the_25_rows_of_DOMAIN_plus_admin_reopen()
    {
        var pairs = WorkOrderTransitions.Table.SelectMany(kv => kv.Value.Select(to => (kv.Key, to))).ToList();
        Assert.Equal(26, pairs.Count);
        Assert.Contains((S.Otkazano, S.Zaprimljen), pairs);
        Assert.Empty(WorkOrderTransitions.Table[S.Isporuceno]);
    }

    // ---- Forbidden transitions (409 with a message) ----
    public static TheoryData<S, S> ForbiddenRows => new()
    {
        { S.Zaprimljen, S.Laser },
        { S.Zaprimljen, S.Zavrseno },
        { S.CekaMaterijal, S.Laser },
        { S.Spreman, S.Kontrola },
        { S.Laser, S.Spreman },
        { S.Presanje, S.Laser },
        { S.Kontrola, S.Isporuceno },
        { S.Doraditi, S.Zavrseno },
        { S.Zavrseno, S.Otkazano },
        { S.Isporuceno, S.Otkazano },
        { S.Isporuceno, S.Kontrola },
        { S.Otkazano, S.Spreman },
        { S.Laser, S.Laser },
    };

    [Theory]
    [MemberData(nameof(ForbiddenRows))]
    public void Forbidden_transition_is_rejected_even_for_admin(S from, S to)
    {
        var wo = Wo(from);
        var ex = Assert.Throws<DomainException>(() => WorkOrderTransitions.Apply(wo, to, AllOk, isAdmin: true, reason: "razlog"));
        Assert.Equal($"Prelaz iz \"{DisplayNames.Of(from)}\" u \"{DisplayNames.Of(to)}\" nije dozvoljen.", ex.Message);
        Assert.Equal(from, wo.Status);
    }

    // ---- Gates ----
    [Fact]
    public void Laser_to_presanje_needs_laser_first_piece()
    {
        var f = AllOk with { LaserFirstPieceApproved = false };
        Assert.Equal("Prvi komad sa lasera nije potvrđen.",
            Assert.Throws<DomainException>(() => WorkOrderTransitions.Apply(Wo(S.Laser), S.Presanje, f, false, null)).Message);
        var wo = Wo(S.Laser);
        WorkOrderTransitions.Apply(wo, S.Presanje, AllOk with { LaserFirstPieceApproved = true, PressFirstPieceApproved = false, FinalInspectionPassed = false }, false, null);
        Assert.Equal(S.Presanje, wo.Status);
    }

    [Fact]
    public void Presanje_to_kontrola_needs_press_first_piece()
    {
        var f = AllOk with { PressFirstPieceApproved = false };
        Assert.Equal("Prvi komad sa prese nije potvrđen.",
            Assert.Throws<DomainException>(() => WorkOrderTransitions.Apply(Wo(S.Presanje), S.Kontrola, f, false, null)).Message);
    }

    [Fact]
    public void Gates_apply_only_on_the_named_edges()
    {
        // Laser → Kontrola and Doraditi → Kontrola/Presanje have no first-piece gate.
        foreach (var (from, to) in new[] { (S.Laser, S.Kontrola), (S.Doraditi, S.Kontrola), (S.Doraditi, S.Presanje), (S.Spreman, S.Presanje) })
        {
            var wo = Wo(from);
            WorkOrderTransitions.Apply(wo, to, NothingDone, false, null);
            Assert.Equal(to, wo.Status);
        }
    }

    [Theory]
    [InlineData(false, QualityApproval.Prihvaceno, "Završna kontrola nije prihvaćena.")]
    [InlineData(true, QualityApproval.Ceka, "Odobrenje kvaliteta nije Prihvaćeno.")]
    public void Zavrseno_needs_final_inspection_and_quality_approval(bool final, QualityApproval qa, string message) =>
        Assert.Equal(message, Assert.Throws<DomainException>(() =>
            WorkOrderTransitions.Apply(Wo(S.Kontrola), S.Zavrseno, AllOk with { FinalInspectionPassed = final, QualityApproval = qa }, true, "admin razlog")).Message);

    [Fact]
    public void Isporuceno_needs_ready_for_shipment()
    {
        var ex = Assert.Throws<DomainException>(() =>
            WorkOrderTransitions.Apply(Wo(S.Zavrseno), S.Isporuceno, AllOk with { ReadyForShipment = false }, true, "razlog"));
        Assert.Equal("Nalog nije spreman za isporuku: gotovih ispravnih komada je manje od naručene količine.", ex.Message);
    }

    // ---- Blokirano ----
    public static TheoryData<S, S, bool> BlockedCases => new()
    {
        { S.Zaprimljen, S.Spreman, false },
        { S.Spreman, S.Laser, false },
        { S.Laser, S.Kontrola, false },
        { S.Doraditi, S.Kontrola, false },
        { S.Kontrola, S.Zavrseno, false },
        { S.Laser, S.Doraditi, true },
        { S.Kontrola, S.Otkazano, true },
        { S.Spreman, S.CekaMaterijal, true },   // backward
        { S.Zavrseno, S.Kontrola, true },       // backward (povrat)
    };

    [Theory]
    [MemberData(nameof(BlockedCases))]
    public void Blokirano_blocks_all_forward_moves_except_doraditi_and_otkazano(S from, S to, bool allowed)
    {
        var f = AllOk with { QualityApproval = QualityApproval.Blokirano };
        var check = WorkOrderTransitions.Check(from, to, f, isAdmin: true);
        Assert.Equal(allowed, check.Allowed);
        if (!allowed)
        {
            Assert.Equal("Kvalitet je blokiran. Dozvoljen je samo prelaz u Doraditi ili Otkazano.", check.Reason);
            Assert.False(check.CanOverride);
        }
    }

    // ---- Override (Admin, reason, first-piece gates only) ----
    [Fact]
    public void Admin_can_override_first_piece_gate_with_reason()
    {
        var wo = Wo(S.Laser);
        WorkOrderTransitions.Apply(wo, S.Presanje, NothingDone, isAdmin: true, reason: "Nalog ide samo kroz plazmu");
        Assert.Equal(S.Presanje, wo.Status);
    }

    [Fact]
    public void Override_without_reason_is_rejected() =>
        Assert.Equal("Prvi komad sa lasera nije potvrđen. Za preskakanje uslova upiši razlog.",
            Assert.Throws<DomainException>(() => WorkOrderTransitions.Apply(Wo(S.Laser), S.Presanje, NothingDone, true, " ")).Message);

    [Fact]
    public void Kontrolor_cannot_override() =>
        Assert.Throws<DomainException>(() => WorkOrderTransitions.Apply(Wo(S.Presanje), S.Kontrola, NothingDone, isAdmin: false, reason: "razlog"));

    [Fact]
    public void Final_inspection_and_shipment_gates_cannot_be_overridden()
    {
        Assert.False(WorkOrderTransitions.Check(S.Kontrola, S.Zavrseno, NothingDone, true).CanOverride);
        Assert.False(WorkOrderTransitions.Check(S.Zavrseno, S.Isporuceno, NothingDone, true).CanOverride);
        Assert.Throws<DomainException>(() => WorkOrderTransitions.Apply(Wo(S.Kontrola), S.Zavrseno, NothingDone, true, "razlog"));
    }

    // ---- Reopen ----
    [Fact]
    public void Only_admin_reopens_cancelled_order_with_reason()
    {
        Assert.Equal("Otkazani nalog može ponovo otvoriti samo Admin.",
            Assert.Throws<DomainException>(() => WorkOrderTransitions.Apply(Wo(S.Otkazano), S.Zaprimljen, AllOk, false, "razlog")).Message);
        Assert.Equal("Razlog ponovnog otvaranja je obavezan.",
            Assert.Throws<DomainException>(() => WorkOrderTransitions.Apply(Wo(S.Otkazano), S.Zaprimljen, AllOk, true, null)).Message);
        var wo = Wo(S.Otkazano);
        WorkOrderTransitions.Apply(wo, S.Zaprimljen, AllOk, true, "Kupac ponovo naručio");
        Assert.Equal(S.Zaprimljen, wo.Status);
    }

    [Fact]
    public void Options_list_table_targets_with_gate_results()
    {
        var options = WorkOrderTransitions.Options(S.Laser, NothingDone, isAdmin: false);
        Assert.Equal([S.Presanje, S.Kontrola, S.Doraditi, S.Otkazano], options.Select(o => o.To));
        var presanje = options.Single(o => o.To == S.Presanje);
        Assert.False(presanje.Allowed);
        Assert.Equal("Prvi komad sa lasera nije potvrđen.", presanje.Reason);
        Assert.Empty(WorkOrderTransitions.Options(S.Otkazano, AllOk, isAdmin: false));
        Assert.Single(WorkOrderTransitions.Options(S.Otkazano, AllOk, isAdmin: true));
    }
}
