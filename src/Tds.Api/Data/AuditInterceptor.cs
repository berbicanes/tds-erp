using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Data;

/// <summary>
/// Fills audit fields on every <see cref="Entity"/> and writes one <see cref="AuditLog"/> row per change,
/// in the same SaveChanges (DATA_MODEL §15). Hard deletes of audited entities are rejected:
/// AuditLog has no Delete action and QMS records are only voided (golden rule 4).
/// </summary>
public sealed class AuditInterceptor(IClock clock, ICurrentUser currentUser, AuditContext? auditContext = null) : SaveChangesInterceptor
{
    /// <summary>Enums are written by name ("Odobren"), so the history stays readable without the CLR type.</summary>
    private static readonly JsonSerializerOptions Json = new() { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    private static readonly HashSet<string> AuditFieldNames =
    [
        nameof(Entity.CreatedAt), nameof(Entity.CreatedBy),
        nameof(Entity.UpdatedAt), nameof(Entity.UpdatedBy), nameof(Entity.Version),
        nameof(IVoidable.VoidedAt), nameof(IVoidable.VoidedBy),
    ];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            Audit(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            Audit(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Audit(DbContext context)
    {
        var now = clock.UtcNow;
        var userId = currentUser.UserId;
        var logs = new List<AuditLog>();
        var linkChanges = LinkChanges(context);

        foreach (var entry in context.ChangeTracker.Entries<Entity>().ToList())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    logs.Add(CreateLog(entry, AuditAction.Create, Merge(CreateChanges(entry), linkChanges, entry.Entity.Id), userId, now));
                    break;

                case EntityState.Modified:
                    var changes = Merge(UpdateChanges(entry), linkChanges, entry.Entity.Id);
                    if (changes.Count == 0)
                        break;

                    var action = AuditAction.Update;
                    if (entry.Entity is IVoidable voidable && IsBeingVoided(entry))
                    {
                        voidable.VoidedAt = now;
                        voidable.VoidedBy = userId;
                        action = AuditAction.Void;
                    }

                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    if (!string.IsNullOrWhiteSpace(auditContext?.Reason))
                        changes["reason"] = [null, auditContext.Reason];
                    logs.Add(CreateLog(entry, action, changes, userId, now));
                    break;

                case EntityState.Deleted:
                    throw new InvalidOperationException(
                        $"Hard delete of {entry.Metadata.ClrType.Name} is not allowed. Audited records are voided, not deleted.");
            }
        }

        // Link-only changes (e.g. only the machines of a production entry changed).
        foreach (var ((parentType, parentId), fields) in linkChanges)
            logs.Add(new AuditLog
            {
                EntityType = parentType, EntityId = parentId, Action = AuditAction.Update,
                ChangesJson = JsonSerializer.Serialize(fields, Json), UserId = userId, At = now,
            });

        context.Set<AuditLog>().AddRange(logs);
    }

    /// <summary>
    /// M:N rows (<see cref="IAuditedLink"/>) are audited on their parent as one field: {"EquipmentIds": [old set, new set]}.
    /// Only parents whose links were added or removed are returned.
    /// </summary>
    private static Dictionary<(string Type, Guid Id), Dictionary<string, object?[]>> LinkChanges(DbContext context)
    {
        var result = new Dictionary<(string, Guid), Dictionary<string, object?[]>>();
        var links = context.ChangeTracker.Entries<IAuditedLink>()
            .Where(e => e.State is EntityState.Added or EntityState.Deleted or EntityState.Unchanged)
            .GroupBy(e => (e.Entity.ParentType, e.Entity.ParentId, e.Entity.Field));
        foreach (var g in links)
        {
            if (!g.Any(e => e.State is EntityState.Added or EntityState.Deleted))
                continue;
            var old = g.Where(e => e.State != EntityState.Added).Select(e => e.Entity.ValueId).Order().ToList();
            var @new = g.Where(e => e.State != EntityState.Deleted).Select(e => e.Entity.ValueId).Order().ToList();
            var key = (g.Key.ParentType, g.Key.ParentId);
            if (!result.TryGetValue(key, out var fields))
                result[key] = fields = [];
            fields[g.Key.Field] = [old.Count == 0 ? null : old, @new];
        }
        return result;
    }

    private static Dictionary<string, object?[]> Merge(
        Dictionary<string, object?[]> changes, Dictionary<(string Type, Guid Id), Dictionary<string, object?[]>> linkChanges, Guid id)
    {
        var key = linkChanges.Keys.FirstOrDefault(k => k.Id == id);
        if (key == default)
            return changes;
        foreach (var (field, value) in linkChanges[key])
            changes[field] = value;
        linkChanges.Remove(key);
        return changes;
    }

    private static bool IsBeingVoided(EntityEntry<Entity> entry)
    {
        var prop = entry.Property(nameof(IVoidable.IsVoided));
        return prop.IsModified && prop.OriginalValue is false && prop.CurrentValue is true;
    }

    private static Dictionary<string, object?[]> CreateChanges(EntityEntry entry) =>
        entry.Properties
            .Where(p => !IsExcluded(p))
            .ToDictionary(p => p.Metadata.Name, p => new[] { null, p.CurrentValue });

    private static Dictionary<string, object?[]> UpdateChanges(EntityEntry entry) =>
        entry.Properties
            .Where(p => p.IsModified && !IsExcluded(p) && !Equals(p.OriginalValue, p.CurrentValue))
            .ToDictionary(p => p.Metadata.Name, p => new[] { p.OriginalValue, p.CurrentValue });

    private static bool IsExcluded(PropertyEntry p) =>
        p.Metadata.IsPrimaryKey() || p.Metadata.IsShadowProperty() || AuditFieldNames.Contains(p.Metadata.Name);

    private static AuditLog CreateLog(
        EntityEntry<Entity> entry, AuditAction action, Dictionary<string, object?[]> changes, Guid? userId, DateTimeOffset at) =>
        new()
        {
            EntityType = entry.Metadata.ClrType.Name,
            EntityId = entry.Entity.Id,
            Action = action,
            ChangesJson = JsonSerializer.Serialize(changes, Json),
            UserId = userId,
            At = at,
        };
}
