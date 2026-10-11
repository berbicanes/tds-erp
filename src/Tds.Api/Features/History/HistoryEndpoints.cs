using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.History;

/// <summary>One changed field. Values are JSON: references are already names, enums are value names (labels in the UI).</summary>
public sealed record HistoryChange(string Field, JsonNode? Old, JsonNode? New);

public sealed record HistoryEntryDto(
    Guid Id, DateTimeOffset At, string? UserName, AuditAction Action, string EntityType, Guid EntityId, string RecordLabel,
    string? Reason, IReadOnlyList<HistoryChange> Changes);

/// <summary>
/// UI §6: one readable history for a record and the records that belong to it (order: its checks, NCs,
/// production entries and lot links; lot: its checks, NCs, movements and reservations; document: all versions).
/// </summary>
public static class HistoryEndpoints
{
    public const int Limit = 500;

    public static void MapHistoryEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/history/{scope}/{id:guid}", Get).RequireAuthorization();

    /// <summary>Link-set fields written by IAuditedLink and the record type of their ids.</summary>
    private static readonly Dictionary<string, Type> LinkFields = new()
    {
        ["EquipmentIds"] = typeof(Domain.Equipment),
        ["EvidenceFileIds"] = typeof(FileAttachment),
        ["CertificateFileIds"] = typeof(FileAttachment),
    };

    private sealed record Rec(string Type, Guid Id, string Label);

    private static async Task<IResult> Get(string scope, Guid id, AppDbContext db, CancellationToken ct)
    {
        var records = scope switch
        {
            "work-order" => await WorkOrderScope(db, id, ct),
            "lot" => await LotScope(db, id, ct),
            "non-conformity" => await db.NonConformities.Where(n => n.Id == id)
                .Select(n => new Rec(nameof(NonConformity), n.Id, "NC " + n.Number)).ToListAsync(ct),
            "document" => await DocumentScope(db, id, ct),
            _ => null,
        };
        if (records is null)
            return ApiResults.NotFound("Nepoznata vrsta zapisa.");
        if (records.Count == 0)
            return ApiResults.NotFound("Zapis ne postoji.");

        var ids = records.Select(r => r.Id).ToList();
        var types = records.Select(r => r.Type).Distinct().ToList();
        var logs = await db.AuditLogs.AsNoTracking()
            .Where(a => types.Contains(a.EntityType) && ids.Contains(a.EntityId))
            .OrderByDescending(a => a.At).ThenByDescending(a => a.Id).Take(Limit).ToListAsync(ct);

        var parsed = logs.Select(l => (Log: l, Fields: JsonNode.Parse(l.ChangesJson)!.AsObject())).ToList();
        var names = await ResolveAsync(db, parsed.SelectMany(p => References(p.Log.EntityType, p.Fields, db.Model)), ct);
        var userIds = logs.Where(l => l.UserId != null).Select(l => l.UserId!.Value).Distinct().ToList();
        var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName, ct);
        var labels = records.ToDictionary(r => (r.Type, r.Id), r => r.Label);

        return TypedResults.Ok(parsed.Select(p =>
        {
            string? reason = null;
            var changes = new List<HistoryChange>();
            foreach (var (field, value) in p.Fields)
            {
                var pair = value!.AsArray();
                if (field == "reason")
                {
                    reason = pair[1]?.GetValue<string>();
                    continue;
                }
                var target = TargetType(p.Log.EntityType, field, db.Model);
                changes.Add(new HistoryChange(field, Readable(pair[0], target, names, EnumType(p.Log.EntityType, field, db.Model)),
                    Readable(pair[1], target, names, EnumType(p.Log.EntityType, field, db.Model))));
            }
            if (p.Log.Action == AuditAction.Create)
                changes.RemoveAll(c => c.New is null || c.Field == nameof(VoidableEntity.IsVoided));
            if (p.Log.Action == AuditAction.Void)
            {
                // The void reason is the reason of the entry, not a field change.
                reason ??= changes.FirstOrDefault(c => c.Field == nameof(VoidableEntity.VoidedReason))?.New?.GetValue<string>();
                changes.RemoveAll(c => c.Field is nameof(VoidableEntity.IsVoided) or nameof(VoidableEntity.VoidedReason));
            }
            return new HistoryEntryDto(p.Log.Id, p.Log.At, p.Log.UserId is { } u ? users.GetValueOrDefault(u) : null, p.Log.Action,
                p.Log.EntityType, p.Log.EntityId, labels.GetValueOrDefault((p.Log.EntityType, p.Log.EntityId)) ?? p.Log.EntityType, reason, changes);
        }).ToList());
    }

    private static async Task<List<Rec>> WorkOrderScope(AppDbContext db, Guid id, CancellationToken ct)
    {
        var wo = await db.WorkOrders.Where(w => w.Id == id).Select(w => new Rec(nameof(WorkOrder), w.Id, "Nalog " + w.Number)).ToListAsync(ct);
        if (wo.Count == 0)
            return wo;
        wo.AddRange(await db.QualityChecks.Where(q => q.WorkOrderId == id).Select(q => new Rec(nameof(QualityCheck), q.Id, "Kontrola " + q.Number)).ToListAsync(ct));
        wo.AddRange(await db.NonConformities.Where(n => n.WorkOrderId == id).Select(n => new Rec(nameof(NonConformity), n.Id, "NC " + n.Number)).ToListAsync(ct));
        wo.AddRange(await db.ProductionEntries.Where(e => e.WorkOrderId == id).Select(e => new Rec(nameof(ProductionEntry), e.Id, "Dnevni zapis " + e.Number)).ToListAsync(ct));
        wo.AddRange(await (from m in db.WorkOrderMaterials where m.WorkOrderId == id
                           join l in db.MaterialLots on m.MaterialLotId equals l.Id
                           select new Rec(nameof(WorkOrderMaterial), m.Id, "Lot " + l.LotNumber + " na nalogu")).ToListAsync(ct));
        return wo;
    }

    private static async Task<List<Rec>> LotScope(AppDbContext db, Guid id, CancellationToken ct)
    {
        var lot = await db.MaterialLots.Where(l => l.Id == id).Select(l => new Rec(nameof(MaterialLot), l.Id, "Lot " + l.LotNumber)).ToListAsync(ct);
        if (lot.Count == 0)
            return lot;
        lot.AddRange(await db.QualityChecks.Where(q => q.MaterialLotId == id).Select(q => new Rec(nameof(QualityCheck), q.Id, "Kontrola " + q.Number)).ToListAsync(ct));
        lot.AddRange(await db.NonConformities.Where(n => n.MaterialLotId == id).Select(n => new Rec(nameof(NonConformity), n.Id, "NC " + n.Number)).ToListAsync(ct));
        lot.AddRange(await db.StockMovements.Where(m => m.MaterialLotId == id).Select(m => new Rec(nameof(StockMovement), m.Id, "Pokret zalihe")).ToListAsync(ct));
        lot.AddRange(await (from m in db.WorkOrderMaterials where m.MaterialLotId == id
                            join w in db.WorkOrders on m.WorkOrderId equals w.Id
                            select new Rec(nameof(WorkOrderMaterial), m.Id, "Rezervacija za " + w.Number)).ToListAsync(ct));
        return lot;
    }

    private static async Task<List<Rec>> DocumentScope(AppDbContext db, Guid id, CancellationToken ct)
    {
        var code = await db.ControlledDocuments.Where(d => d.Id == id).Select(d => d.Code).SingleOrDefaultAsync(ct);
        return code is null ? [] : await db.ControlledDocuments.Where(d => d.Code == code)
            .Select(d => new Rec(nameof(ControlledDocument), d.Id, d.Code + " v" + d.DocVersion)).ToListAsync(ct);
    }

    private static IEntityType? EntityTypeOf(string name, IModel model) =>
        model.GetEntityTypes().FirstOrDefault(t => t.ClrType.Name == name);

    /// <summary>Record type a Guid field points to: link-set fields, or the EF foreign key of the property.</summary>
    private static Type? TargetType(string entityType, string field, IModel model) =>
        LinkFields.TryGetValue(field, out var t) ? t
        : EntityTypeOf(entityType, model)?.FindProperty(field)?.GetContainingForeignKeys().FirstOrDefault()?.PrincipalEntityType.ClrType;

    private static Type? EnumType(string entityType, string field, IModel model)
    {
        var clr = EntityTypeOf(entityType, model)?.FindProperty(field)?.ClrType;
        clr = clr is null ? null : Nullable.GetUnderlyingType(clr) ?? clr;
        return clr is { IsEnum: true } ? clr : null;
    }

    private static IEnumerable<(Type Type, Guid Id)> References(string entityType, JsonObject fields, IModel model)
    {
        foreach (var (field, value) in fields)
        {
            if (TargetType(entityType, field, model) is not { } target)
                continue;
            foreach (var side in value!.AsArray())
                foreach (var g in Guids(side))
                    yield return (target, g);
        }
    }

    private static IEnumerable<Guid> Guids(JsonNode? node) => node switch
    {
        JsonArray a => a.SelectMany(Guids),
        JsonValue v when v.TryGetValue<string>(out var s) && Guid.TryParse(s, out var g) => [g],
        _ => [],
    };

    private static JsonNode? Readable(JsonNode? node, Type? target, Dictionary<(Type, Guid), string> names, Type? enumType)
    {
        if (node is null)
            return null;
        if (target is not null)
            return node is JsonArray a
                ? new JsonArray(a.Select(x => (JsonNode?)JsonValue.Create(Name(x, target, names))).ToArray())
                : JsonValue.Create(Name(node, target, names));
        // Rows written before enums were stored by name hold numbers (DATA_MODEL §15).
        if (enumType is not null && node is JsonValue n && n.TryGetValue<int>(out var number))
            return JsonValue.Create(Enum.GetName(enumType, number) ?? number.ToString());
        return node.DeepClone();
    }

    private static string Name(JsonNode? node, Type target, Dictionary<(Type, Guid), string> names) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) && Guid.TryParse(s, out var g)
            ? names.GetValueOrDefault((target, g)) ?? "(nepoznato)"
            : node?.ToJsonString() ?? "";

    private static async Task<Dictionary<(Type, Guid), string>> ResolveAsync(AppDbContext db, IEnumerable<(Type Type, Guid Id)> refs, CancellationToken ct)
    {
        var result = new Dictionary<(Type, Guid), string>();
        foreach (var group in refs.Distinct().GroupBy(r => r.Type))
        {
            var ids = group.Select(r => r.Id).ToList();
            var rows = group.Key switch
            {
                var t when t == typeof(Employee) => await db.Employees.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, Name = x.FullName }).ToListAsync(ct),
                var t when t == typeof(Domain.Equipment) => await db.Equipment.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, Name = x.Code + " " + x.Name }).ToListAsync(ct),
                var t when t == typeof(Supplier) => await db.Suppliers.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct),
                var t when t == typeof(MaterialLot) => await db.MaterialLots.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, Name = x.LotNumber }).ToListAsync(ct),
                var t when t == typeof(MaterialItem) => await db.MaterialItems.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct),
                var t when t == typeof(WorkOrder) => await db.WorkOrders.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, Name = x.Number }).ToListAsync(ct),
                var t when t == typeof(QualityCheck) => await db.QualityChecks.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, Name = x.Number }).ToListAsync(ct),
                var t when t == typeof(NonConformity) => await db.NonConformities.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, Name = x.Number }).ToListAsync(ct),
                var t when t == typeof(ProductionEntry) => await db.ProductionEntries.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, Name = x.Number }).ToListAsync(ct),
                var t when t == typeof(FileAttachment) => await db.FileAttachments.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, Name = x.OriginalName }).ToListAsync(ct),
                _ => [],
            };
            foreach (var r in rows)
                result[(group.Key, r.Id)] = r.Name;
        }
        return result;
    }
}
