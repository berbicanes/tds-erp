using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;

namespace Tds.Api.Tests.UnitTests;

/// <summary>UI §6: every audited field of a record shown in the unified history has a Bosnian name in web/src/lib/history.ts.</summary>
public class HistoryFieldLabelsTests
{
    private static readonly Type[] HistoryRecords =
    [
        typeof(WorkOrder), typeof(QualityCheck), typeof(NonConformity), typeof(ProductionEntry), typeof(WorkOrderMaterial),
        typeof(MaterialLot), typeof(StockMovement), typeof(ControlledDocument),
    ];

    private static readonly string[] Technical =
        [nameof(Entity.CreatedAt), nameof(Entity.CreatedBy), nameof(Entity.UpdatedAt), nameof(Entity.UpdatedBy), nameof(Entity.Version),
         nameof(VoidableEntity.VoidedAt), nameof(VoidableEntity.VoidedBy)]; // set by the interceptor, never a field change

    [Fact]
    public void Every_audited_field_has_a_label()
    {
        var file = File.ReadAllText(FindRepoFile("web/src/lib/history.ts"));
        var block = Regex.Match(file, @"export const historyFields: Record<string, FieldDef> = \{(.*?)\n\};", RegexOptions.Singleline).Groups[1].Value;
        var labelled = Regex.Matches(block, @"^\s+(\w+): f\(", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToHashSet();
        Assert.NotEmpty(labelled);

        // Model only (no database): same configuration as the app.
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=unused").UseSnakeCaseNamingConvention().Options;
        using var db = new AppDbContext(options);
        var missing = HistoryRecords
            .SelectMany(t => db.Model.FindEntityType(t)!.GetProperties()
                .Where(p => !p.IsPrimaryKey() && !p.IsShadowProperty() && !Technical.Contains(p.Name))
                .Select(p => $"{t.Name}.{p.Name}"))
            .Concat(["ProductionEntry.EquipmentIds", "QualityCheck.EvidenceFileIds", "NonConformity.EvidenceFileIds"])
            .Where(n => !labelled.Contains(n.Split('.')[1]))
            .ToList();
        Assert.Empty(missing);
    }

    private static string FindRepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, relative)))
                return Path.Combine(dir.FullName, relative);
        throw new FileNotFoundException(relative);
    }
}
