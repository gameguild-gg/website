using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using GameGuild.API.Eventing;
using GameGuild.Identity.Authorization;

namespace GameGuild.API.Database;

/// <summary>
///     Universal automatic entity-change audit capture. Runs inside
///     <see cref="ApplicationDbContext.SaveChangesAsync"/> before the version bump and
///     commit: it detects every Added/Modified/Deleted ChangeTracker entry and stages
///     insert-only <see cref="EntityChangeAuditRecord"/> rows in the same SaveChanges
///     call, so a failed or rolled-back transaction leaves no audit rows and bulk
///     updates of N entities produce N audit rows. The pass is O(entries) over tracked
///     state with no extra database queries; audit records are insert-only additions and
///     never touch business concurrency tokens.
/// </summary>
internal static class EntityChangeAuditCapture
{
    /// <summary>Replacement value stored for properties marked with <see cref="AuditSensitiveAttribute"/>.</summary>
    internal const string RedactedValue = "[REDACTED]";

    private const string AuditStoreNamespacePrefix = "GameGuild.Compliance.Audit";

    private static readonly Type[] ExcludedTypes =
    [
        typeof(EntityChangeAuditRecord),
        typeof(OutboxMessage),
        typeof(InboxReceipt),
        typeof(DataProtectionKey),
        typeof(PermissionAuditLog),
        typeof(PermissionEvaluationLogEntry)
    ];

    // Auditing the audit stores themselves would recurse; durable transport and data-protection
    // keys are system-internal write-ahead records, not business entities.
    private static readonly ConcurrentDictionary<Type, bool> AuditStoreTypeCache = new();
    private static readonly ConcurrentDictionary<(Type EntityType, string PropertyName), bool> SensitivePropertyCache = new();

    /// <summary>
    ///     Captures audit records for all tracked entity changes and adds them to the context.
    ///     Returns the number of audit records staged (used to keep SaveChanges row counts stable).
    ///     Snapshot failures are logged per entry and never prevent the authorized change.
    /// </summary>
    internal static int Capture(DbContext context, UseCaseOperationContext? operation, ILogger? logger,
        Guid? mutationActorId = null, Guid? mutationTenantId = null, bool useEntityTenantScope = false)
    {
        var records = new List<EntityChangeAuditRecord>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)
                || IsAuditStoreEntity(entry.Metadata.ClrType))
            {
                continue;
            }

            try
            {
                var isDeleted = entry.State == EntityState.Deleted;
                records.Add(new EntityChangeAuditRecord
                {
                    Id = Guid.NewGuid(),
                    EntityName = entry.Metadata.ClrType.Name,
                    KeyId = ReadPrimaryKey(entry, useOriginalValues: isDeleted) ?? "unknown",
                    ChangeKind = entry.State.ToString(),
                    BeforeValues = entry.State == EntityState.Added ? null : SerializeValues(entry, useOriginalValues: true),
                    AfterValues = isDeleted ? null : SerializeValues(entry, useOriginalValues: false),
                    // A verified mutation actor also selects the exact authorized tenant,
                    // including null for global state; it never inherits the caller's tenant.
                    TenantId = ResolveCapturedTenant(entry, operation, isDeleted, mutationActorId, mutationTenantId, useEntityTenantScope),
                    ActorId = mutationActorId ?? ResolveActorId(entry, operation, isDeleted),
                    CorrelationId = operation?.CorrelationId.ToString(),
                    OccurredAtUtc = DateTimeOffset.UtcNow
                });
            }
            catch (Exception exception)
            {
                // Snapshot serialization must never prevent an authorized entity change.
                logger?.LogWarning(
                    exception,
                    "Could not capture an entity change audit record for {EntityType}",
                    entry.Metadata.ClrType.Name);
            }
        }

        if (records.Count > 0)
        {
            context.Set<EntityChangeAuditRecord>().AddRange(records);
        }

        return records.Count;
    }

    private static bool IsAuditStoreEntity(Type clrType) =>
        AuditStoreTypeCache.GetOrAdd(clrType, static type =>
            ExcludedTypes.Contains(type)
            || (type.Namespace?.StartsWith(AuditStoreNamespacePrefix, StringComparison.Ordinal) ?? false)
            || type.Name.Contains("AuditLog", StringComparison.OrdinalIgnoreCase));

    /// <summary>Modified entries record only changed properties; Added/Deleted record the full state.</summary>
    private static string? SerializeValues(EntityEntry entry, bool useOriginalValues)
    {
        var changedOnly = entry.State == EntityState.Modified;
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var property in entry.Properties)
        {
            if (changedOnly && !property.IsModified)
            {
                continue;
            }

            values[property.Metadata.Name] = IsSensitiveProperty(entry.Metadata.ClrType, property.Metadata.Name)
                ? RedactedValue
                : (useOriginalValues ? property.OriginalValue : property.CurrentValue);
        }

        return values.Count == 0 ? null : SafeSerialize(values);
    }

    private static bool IsSensitiveProperty(Type entityType, string propertyName) =>
        SensitivePropertyCache.GetOrAdd((entityType, propertyName), static key =>
            key.EntityType.GetProperty(key.PropertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                is { } property
            && property.IsDefined(typeof(AuditSensitiveAttribute), inherit: true));

    private static string SafeSerialize(Dictionary<string, object?> values)
    {
        try
        {
            return JsonSerializer.Serialize(values);
        }
        catch (Exception)
        {
            // Exotic property values (unserializable value objects) degrade to strings instead of losing the row.
            var fallback = values.ToDictionary(
                pair => pair.Key,
                pair => pair.Value?.ToString() ?? (object?)"null",
                StringComparer.Ordinal);
            return JsonSerializer.Serialize(fallback);
        }
    }

    private static Guid? ResolveCapturedTenant(EntityEntry entry, UseCaseOperationContext? operation, bool isDeleted,
        Guid? mutationActorId, Guid? mutationTenantId, bool useEntityTenantScope)
    {
        if (!mutationActorId.HasValue) { return ResolveTenantId(entry, operation, isDeleted); }
        return useEntityTenantScope ? ReadMutationTenant(entry, isDeleted) : mutationTenantId;
    }

    private static Guid? ReadMutationTenant(EntityEntry entry, bool isDeleted)
    {
        var tenant = ReadGuidProperty(entry, "TenantId", useOriginalValues: isDeleted);
        return tenant == Guid.Empty ? null : tenant;
    }

    private static Guid? ResolveTenantId(EntityEntry entry, UseCaseOperationContext? operation, bool isDeleted) =>
        ReadGuidProperty(entry, "TenantId", useOriginalValues: isDeleted)
            ?? (operation?.TenantId is { } operationTenantId && operationTenantId != DurableIntegrationEventTenants.Platform
                ? operationTenantId
                : (Guid?)null);

    private static Guid? ResolveActorId(EntityEntry entry, UseCaseOperationContext? operation, bool isDeleted) =>
        (operation?.ActorId is { } actorId && actorId != Guid.Empty ? actorId : (Guid?)null)
            ?? ReadGuidProperty(entry, "UpdatedByUserId", useOriginalValues: isDeleted)
            ?? ReadGuidProperty(entry, "CreatedByUserId", useOriginalValues: isDeleted);

    private static string? ReadPrimaryKey(EntityEntry entry, bool useOriginalValues)
    {
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null)
        {
            return null;
        }

        var values = primaryKey.Properties
            .Select(key => useOriginalValues ? entry.Property(key.Name).OriginalValue : entry.Property(key.Name).CurrentValue)
            .Select(value => value?.ToString())
            .ToArray();

        return values.Any(string.IsNullOrWhiteSpace) ? null : string.Join(":", values);
    }

    private static Guid? ReadGuidProperty(EntityEntry entry, string propertyName, bool useOriginalValues)
    {
        var property = entry.Properties.FirstOrDefault(candidate => candidate.Metadata.Name == propertyName);
        if (property is null)
        {
            return null;
        }

        var value = useOriginalValues ? property.OriginalValue : property.CurrentValue;
        if (value is Guid guid)
        {
            return guid;
        }

        if (value is string text && Guid.TryParse(text, out var parsed))
        {
            return parsed;
        }

        return value?.GetType().GetProperty("Value")?.GetValue(value) is Guid nestedGuid ? nestedGuid : null;
    }
}
