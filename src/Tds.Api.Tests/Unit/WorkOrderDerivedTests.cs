using Tds.Api.Domain;

namespace Tds.Api.Tests.UnitTests;

/// <summary>DOMAIN §3.</summary>
public class WorkOrderDerivedTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);

    private static WorkOrderFacts Facts(
        int ordered = 100, int completed = 0, WorkOrderStatus status = WorkOrderStatus.Laser,
        QualityApproval qa = QualityApproval.Ceka, bool final = false, DateOnly? due = null) =>
        new(ordered, due ?? Today.AddDays(5), status, qa, completed, false, false, final);

    [Fact]
    public void Ready_for_shipment_needs_final_inspection_quality_approval_and_full_quantity()
    {
        Assert.True(WorkOrderDerived.Compute(Facts(completed: 100, qa: QualityApproval.Prihvaceno, final: true), Today).ReadyForShipment);
        Assert.True(WorkOrderDerived.Compute(Facts(completed: 120, qa: QualityApproval.Prihvaceno, final: true), Today).ReadyForShipment);
    }

    [Theory]
    [InlineData(99, QualityApproval.Prihvaceno, true)]
    [InlineData(100, QualityApproval.Ceka, true)]
    [InlineData(100, QualityApproval.Blokirano, true)]
    [InlineData(100, QualityApproval.Prihvaceno, false)]
    public void Not_ready_when_any_condition_is_missing(int completed, QualityApproval qa, bool final) =>
        Assert.False(WorkOrderDerived.Compute(Facts(completed: completed, qa: qa, final: final), Today).ReadyForShipment);

    [Theory]
    [InlineData(100, 0, 0)]
    [InlineData(100, 33, 33)]
    [InlineData(3, 2, 66)]
    [InlineData(100, 100, 100)]
    [InlineData(100, 150, 100)]
    public void Progress_is_whole_percent_capped_at_100(int ordered, int completed, int expected) =>
        Assert.Equal(expected, WorkOrderDerived.Compute(Facts(ordered: ordered, completed: completed), Today).ProgressPercent);

    [Fact]
    public void Overdue_when_due_date_passed_and_not_finished()
    {
        Assert.True(WorkOrderDerived.Compute(Facts(due: Today.AddDays(-1)), Today).IsOverdue);
        Assert.False(WorkOrderDerived.Compute(Facts(due: Today), Today).IsOverdue);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Zavrseno)]
    [InlineData(WorkOrderStatus.Isporuceno)]
    [InlineData(WorkOrderStatus.Otkazano)]
    public void Finished_orders_are_never_overdue(WorkOrderStatus status) =>
        Assert.False(WorkOrderDerived.Compute(Facts(status: status, due: Today.AddDays(-30)), Today).IsOverdue);
}
