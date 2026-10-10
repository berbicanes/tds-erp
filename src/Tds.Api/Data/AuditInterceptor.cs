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
public sealed class AuditInterceptor(IClock clock, ICurrentUser currentUser) : SaveChangesInterceptor
{
    private static readonly HashSet<string> AuditFieldNames =
    [
        nameof(Entity.CreatedAt), nameof(Entity.CreatedBy),
        nameof(Entity.UpdatedAt), nameof(Entity.UpdatedBy),
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

        foreach (var entry in context.ChangeTracker.Entries<Entity>().ToList())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    logs.Add(CreateLog(entry, AuditAction.Create, CreateChanges(entry), userId, now));
                    break;

                case EntityState.Modified:
                    var changes = UpdateChanges(entry);
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
                    logs.Add(CreateLog(entry, action, changes, userId, now));
                    break;

                case EntityState.Deleted:
                    throw new InvalidOperationException(
                        $"Hard delete of {entry.Metadata.ClrType.Name} is not allowed. Audited records are voided, not deleted.");
            }
        }

        context.Set<AuditLog>().AddRange(logs);
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
            ChangesJson = JsonSerializer.Serialize(changes),
            UserId = userId,
            At = at,
        };
}
