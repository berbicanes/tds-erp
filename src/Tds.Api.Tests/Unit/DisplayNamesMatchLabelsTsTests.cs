using System.Text.RegularExpressions;
using Tds.Api.Domain;

namespace Tds.Api.Tests.Unit;

/// <summary>Server message names must equal web/src/lib/labels.ts (the single UI source).</summary>
public partial class DisplayNamesMatchLabelsTsTests
{
    private static readonly string LabelsTs = File.ReadAllText(FindRepoFile("web/src/lib/labels.ts"));

    private static string FindRepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException(relative);
    }

    /// <summary>Text of <c>Value: l("Text"…)</c> inside <c>export const {map} = { … }</c>.</summary>
    private static string LabelText(string map, string value)
    {
        var block = Regex.Match(LabelsTs, $@"export const {map} = \{{(.*?)\}} as const", RegexOptions.Singleline).Groups[1].Value;
        var m = Regex.Match(block, $@"\b{value}: l\(""([^""]+)""");
        Assert.True(m.Success, $"{map}.{value} not found in labels.ts");
        return m.Groups[1].Value;
    }

    [Fact]
    public void Work_order_statuses() =>
        Assert.All(Enum.GetValues<WorkOrderStatus>(), v => Assert.Equal(LabelText("workOrderStatus", v.ToString()), DisplayNames.Of(v)));

    [Fact]
    public void Receipt_decisions() =>
        Assert.All(Enum.GetValues<ReceiptDecision>(), v => Assert.Equal(LabelText("receiptDecision", v.ToString()), DisplayNames.Of(v)));

    [Fact]
    public void Supplier_statuses() =>
        Assert.All(Enum.GetValues<SupplierApprovalStatus>(), v => Assert.Equal(LabelText("supplierApprovalStatus", v.ToString()), DisplayNames.Of(v)));

    [Fact]
    public void Inspection_phases() =>
        Assert.All(Enum.GetValues<InspectionPhase>(), v => Assert.Equal(LabelText("inspectionPhase", v.ToString()), DisplayNames.Of(v)));
}
