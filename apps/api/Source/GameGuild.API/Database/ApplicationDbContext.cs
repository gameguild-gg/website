using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using GameGuild.API.Eventing;
using GameGuild.API.Setup;
using GameGuild.Compliance.Audit;
using GameGuild.CQRS;

namespace GameGuild.API.Database;

/// <summary>
///     Thin-shell database context that delegates module-specific configuration
///     to <see cref="IModelConfiguration"/> implementations discovered via assembly scanning.
/// </summary>
public class ApplicationDbContext : DbContext, IApplicationDbContext, IDataProtectionKeyContext
{
    private readonly IUseCaseOperationContextAccessor? _useCaseOperationContextAccessor;
    private readonly IAuditService? _auditService;
    private readonly ILogger<ApplicationDbContext>? _logger;
    private readonly PermissionAuditOptions _permissionAuditOptions = new();
    private readonly IReadOnlyList<IPermissionAuditHook> _permissionAuditHooks = [];
    private readonly List<PermissionAuditChange> _pendingPermissionAuditChanges = [];

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IPublisher? publisher) : this(options)
    {
        _ = publisher;
    }

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IPublisher? publisher,
        IUseCaseOperationContextAccessor useCaseOperationContextAccessor,
        IAuditService? auditService = null,
        ILogger<ApplicationDbContext>? logger = null,
        IOptions<PermissionAuditOptions>? permissionAuditOptions = null,
        IEnumerable<IPermissionAuditHook>? permissionAuditHooks = null) : this(options, publisher)
    {
        _useCaseOperationContextAccessor = useCaseOperationContextAccessor;
        _auditService = auditService;
        _logger = logger;
        _permissionAuditOptions = permissionAuditOptions?.Value ?? new PermissionAuditOptions();
        _permissionAuditHooks = permissionAuditHooks?.ToArray() ?? [];
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var permissionChanges = CapturePermissionAuditChanges();
        var trackedEventEntities = ChangeTracker.Entries()
            .Select(entry => entry.Entity)
            .OfType<object>()
            .Distinct()
            .ToList();

        var domainEventEntities = trackedEventEntities
            .OfType<IHasDomainEvents>()
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();
        var integrationEventEntities = trackedEventEntities
            .OfType<IHasIntegrationEvents>()
            .Where(entity => entity.IntegrationEvents.Count > 0)
            .ToList();
        var integrationEvents = integrationEventEntities
            .SelectMany(entity => entity.IntegrationEvents)
            .DistinctBy(integrationEvent => integrationEvent.EventId)
            .ToList();

        var capturedOutboxCount = 0;

        foreach (var integrationEvent in integrationEvents)
        {
            DurableIntegrationEventValidator.Validate(integrationEvent);
            var trackedOutbox = ChangeTracker.Entries<OutboxMessage>()
                .FirstOrDefault(entry => entry.Entity.EventId == integrationEvent.EventId);
            if (trackedOutbox is not null)
            {
                if (trackedOutbox.State == EntityState.Added)
                {
                    capturedOutboxCount++;
                }

                continue;
            }

            Set<OutboxMessage>().Add(new OutboxMessage
            {
                EventId = integrationEvent.EventId,
                TenantId = integrationEvent.TenantId,
                ActorId = integrationEvent.ActorId,
                EventName = integrationEvent.EventName,
                EventType = DurableIntegrationEventValidator.GetStableTypeName(integrationEvent.GetType()),
                SourceModule = integrationEvent.SourceModule,
                AggregateType = integrationEvent.AggregateType,
                AggregateId = integrationEvent.AggregateId,
                CorrelationId = integrationEvent.CorrelationId,
                CausationId = integrationEvent.CausationId,
                OccurredAtUtc = integrationEvent.OccurredAt,
                SchemaVersion = integrationEvent.SchemaVersion,
                Payload = DurableEventSerializer.Serialize(integrationEvent),
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            capturedOutboxCount++;
        }

        var operationContext = _useCaseOperationContextAccessor?.Current;
        var aggregateEntry = ChangeTracker.Entries()
            .FirstOrDefault(entry =>
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && entry.Entity is not OutboxMessage
                && entry.Entity is not InboxReceipt);
        if (operationContext is not null && aggregateEntry is not null)
        {
            operationContext.MarkBusinessMutationObserved();
            operationContext.RecordProducedEvents(integrationEvents);
        }

        var operationEventPending = false;
        if (operationContext is { OperationEventCaptured: false } && aggregateEntry is not null)
        {
            var aggregateId = aggregateEntry.Entity is EntityBase<Guid> entity
                ? entity.Id.ToString()
                : aggregateEntry.Properties.FirstOrDefault(property => property.Metadata.IsPrimaryKey())?.CurrentValue?.ToString()
                  ?? "unknown";
            var operationEvent = operationContext.GetOrCreateOperationEvent(
                aggregateEntry.Metadata.ClrType.Name,
                aggregateId);
            DurableIntegrationEventValidator.Validate(operationEvent);
            var trackedOperationOutbox = ChangeTracker.Entries<OutboxMessage>()
                .FirstOrDefault(entry => entry.Entity.EventId == operationEvent.EventId);
            if (trackedOperationOutbox is null)
            {
                Set<OutboxMessage>().Add(new OutboxMessage
                {
                    EventId = operationEvent.EventId,
                    TenantId = operationEvent.TenantId,
                    ActorId = operationEvent.ActorId,
                    EventName = operationEvent.EventName,
                    EventType = DurableIntegrationEventValidator.GetStableTypeName(operationEvent.GetType()),
                    SourceModule = operationEvent.SourceModule,
                    AggregateType = operationEvent.AggregateType,
                    AggregateId = operationEvent.AggregateId,
                    CorrelationId = operationEvent.CorrelationId,
                    CausationId = operationEvent.CausationId,
                    OccurredAtUtc = operationEvent.OccurredAt,
                    SchemaVersion = operationEvent.SchemaVersion,
                    Payload = DurableEventSerializer.Serialize(operationEvent),
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
                capturedOutboxCount++;
            }
            else if (trackedOperationOutbox.State == EntityState.Added)
            {
                capturedOutboxCount++;
            }

            operationEventPending = true;
        }

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is EntityBase<Guid> entity &&
                entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property(nameof(EntityBase<Guid>.Version)).CurrentValue =
                    (int)entry.Property(nameof(EntityBase<Guid>.Version)).CurrentValue! + 1;
            }
        }

        var affectedRows = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (permissionChanges.Count > 0)
        {
            _pendingPermissionAuditChanges.AddRange(permissionChanges);

            // CQRS commands flush after their transaction commits. Direct context writes without
            // an ambient command are still audited immediately after persistence succeeds.
            if (_useCaseOperationContextAccessor?.Current is null && Database.CurrentTransaction is null)
            {
                await FlushPendingPermissionAuditChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (operationEventPending)
        {
            operationContext!.MarkOperationEventCaptured();
        }

        foreach (var entity in domainEventEntities)
        {
            entity.ClearDomainEvents();
        }

        foreach (var entity in integrationEventEntities)
        {
            entity.ClearIntegrationEvents();
        }

        return affectedRows - capturedOutboxCount;
    }

    /// <summary>
    /// Flushes the permission changes collected during the current use-case transaction through
    /// the centralized audit service. Audit failures are isolated from the permission mutation.
    /// </summary>
    public async Task FlushPendingPermissionAuditChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingPermissionAuditChanges.Count == 0)
        {
            return;
        }

        var changes = _pendingPermissionAuditChanges.ToArray();
        _pendingPermissionAuditChanges.Clear();

        if (_auditService is null)
        {
            _logger?.LogWarning(
                "Skipped {Count} permission audit change(s) because the centralized audit service is unavailable",
                changes.Length);
            return;
        }

        var operation = _useCaseOperationContextAccessor?.Current;
        var groups = changes.GroupBy(change => new
        {
            change.ActionType,
            change.ActorId,
            change.TenantId,
            change.CorrelationId,
            change.CommandType
        });

        foreach (var group in groups)
        {
            var entries = group.ToArray();
            var resourceIds = entries.Select(change => change.ResourceId).Distinct(StringComparer.Ordinal).ToArray();
            var tenantIds = entries.Select(change => change.TenantId).Distinct().ToArray();
            var request = new CreateAuditLogRequest
            {
                ActionType = group.Key.ActionType,
                ResourceType = "Permission",
                ResourceId = resourceIds.Length == 1 ? resourceIds[0] : null,
                UserId = group.Key.ActorId == Guid.Empty ? null : group.Key.ActorId,
                TenantId = tenantIds.Length == 1 ? tenantIds[0] : null,
                Description = $"{entries.Length} permission record(s) changed by {group.Key.CommandType ?? "a direct data operation"}.",
                Metadata = new
                {
                    CommandType = group.Key.CommandType ?? operation?.CommandType,
                    CorrelationId = group.Key.CorrelationId,
                    Changes = entries.Select(change => new
                    {
                        change.Operation,
                        change.EntityType,
                        change.ResourceId,
                        change.TargetUserId,
                        change.TenantId,
                        change.BeforeState,
                        change.AfterState
                    }).ToArray()
                },
                Success = true,
                RiskLevel = AuditRiskLevel.Medium,
                Category = AuditCategory.Permission,
                CorrelationId = group.Key.CorrelationId
            };

            try
            {
                await _auditService.LogAsync(request).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger?.LogError(
                    exception,
                    "Failed to write centralized audit record for permission operation {ActionType}; the permission mutation remains committed",
                    group.Key.ActionType);
            }

            var hookContext = new PermissionAuditHookContext(request, entries);
            foreach (var hook in _permissionAuditHooks)
            {
                try
                {
                    await hook.OnPermissionChangesAuditedAsync(hookContext, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger?.LogError(
                        exception,
                        "A custom permission audit hook failed for operation {ActionType}; the permission mutation remains committed",
                        group.Key.ActionType);
                }
            }
        }
    }

    /// <summary>
    /// Discards audit snapshots when the use-case transaction is rolled back.
    /// </summary>
    public void DiscardPendingPermissionAuditChanges() => _pendingPermissionAuditChanges.Clear();

    private List<PermissionAuditChange> CapturePermissionAuditChanges()
    {
        var operation = _useCaseOperationContextAccessor?.Current;
        var changes = new List<PermissionAuditChange>();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)
                || ContainsIgnoreCase(_permissionAuditOptions.ExcludedOperations, entry.State.ToString())
                || IsExcludedEntityType(entry)
                || !IsPermissionChange(entry))
            {
                continue;
            }

            try
            {
                var isAdded = entry.State == EntityState.Added;
                var isDeleted = entry.State == EntityState.Deleted;
                var oldState = isAdded ? null : SerializePermissionState(entry, useOriginalValues: true);
                var newState = isDeleted ? null : SerializePermissionState(entry, useOriginalValues: false);
                var tenantId = ReadTenantId(entry, useOriginalValues: isDeleted)
                    ?? (operation?.TenantId is { } operationTenantId && operationTenantId != DurableIntegrationEventTenants.Platform
                        ? operationTenantId
                        : null);
                var actorId = operation?.ActorId ?? ReadActorId(entry, useOriginalValues: isDeleted) ?? Guid.Empty;
                var actionType = ResolvePermissionActionType(entry);

                changes.Add(new PermissionAuditChange(
                    actionType,
                    entry.State.ToString(),
                    entry.Metadata.ClrType.Name,
                    ReadPrimaryKey(entry, useOriginalValues: isDeleted),
                    ReadGuid(entry, ["UserId", "TargetUserId", "AffectedUserId"], useOriginalValues: isDeleted),
                    tenantId,
                    actorId,
                    operation?.CorrelationId.ToString(),
                    operation?.CommandType,
                    oldState,
                    newState));
            }
            catch (Exception exception)
            {
                // Snapshot serialization must never prevent an authorized permission change.
                _logger?.LogWarning(
                    exception,
                    "Could not capture a complete permission audit snapshot for {EntityType}",
                    entry.Metadata.ClrType.Name);
            }
        }

        return changes;
    }

    private bool IsExcludedEntityType(EntityEntry entry)
    {
        var type = entry.Metadata.ClrType;
        return ContainsIgnoreCase(_permissionAuditOptions.ExcludedEntityTypes, type.Name)
            || ContainsIgnoreCase(_permissionAuditOptions.ExcludedEntityTypes, type.FullName ?? type.Name);
    }

    private static bool ContainsIgnoreCase(IEnumerable<string> configuredValues, string value) =>
        configuredValues.Any(configuredValue => string.Equals(configuredValue, value, StringComparison.OrdinalIgnoreCase));

    private static bool IsPermissionChange(EntityEntry entry)
    {
        var entityName = entry.Metadata.ClrType.Name;
        if (entityName.Contains("AuditLog", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (entityName.Contains("Permission", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (entityName == "UserRole")
        {
            return true;
        }

        if (entityName == "Role")
        {
            return entry.State is EntityState.Added or EntityState.Deleted
                || entry.Properties.Any(property =>
                    property.IsModified && (property.Metadata.Name is "Permissions" or "IsActive"));
        }

        return entry.Properties.Any(property =>
            property.Metadata.Name.Contains("Permission", StringComparison.OrdinalIgnoreCase)
            && (entry.State is EntityState.Added or EntityState.Deleted || property.IsModified));
    }

    private static string? SerializePermissionState(EntityEntry entry, bool useOriginalValues)
    {
        var values = entry.Properties
            .Where(property => entry.State is EntityState.Added or EntityState.Deleted || property.IsModified)
            .ToDictionary(
                property => property.Metadata.Name,
                property => useOriginalValues ? property.OriginalValue : property.CurrentValue,
                StringComparer.Ordinal);

        return values.Count == 0 ? null : JsonSerializer.Serialize(values);
    }

    private static string ResolvePermissionActionType(EntityEntry entry)
    {
        if (entry.State == EntityState.Added)
        {
            return AuditActionTypes.PermissionGranted;
        }

        if (entry.State == EntityState.Deleted || IsPermissionRevocation(entry))
        {
            return AuditActionTypes.PermissionRevoked;
        }

        return AuditActionTypes.PermissionChanged;
    }

    private static bool IsPermissionRevocation(EntityEntry entry)
    {
        foreach (var property in entry.Properties.Where(property => property.IsModified))
        {
            var previous = property.OriginalValue;
            var current = property.CurrentValue;

            if ((property.Metadata.Name is "RevokedAt" or "DeletedAt") && previous is null && current is not null)
            {
                return true;
            }

            if ((property.Metadata.Name is "IsActive" or "IsDeleted") && previous is true && current is false)
            {
                return true;
            }

            if (property.Metadata.Name == "Permissions"
                && ReadPermissionValues(previous) is { } previousPermissions
                && ReadPermissionValues(current) is { } currentPermissions
                && previousPermissions.Except(currentPermissions, StringComparer.OrdinalIgnoreCase).Any())
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyCollection<string>? ReadPermissionValues(object? value)
    {
        if (value is IEnumerable<string> permissionValues)
        {
            return permissionValues.ToArray();
        }

        if (value is not string text)
        {
            return null;
        }

        if (text.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                return JsonSerializer.Deserialize<string[]>(text) ?? [];
            }
            catch (JsonException)
            {
                // Legacy permission strings may be comma-delimited instead of JSON.
            }
        }

        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string? ReadPrimaryKey(EntityEntry entry, bool useOriginalValues)
    {
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null)
        {
            return null;
        }

        var values = primaryKey.Properties.Select(key =>
        {
            var property = entry.Property(key.Name);
            var value = useOriginalValues ? property.OriginalValue : property.CurrentValue;
            return value?.ToString();
        }).ToArray();

        return values.Any(value => string.IsNullOrWhiteSpace(value)) ? null : string.Join(":", values);
    }

    private static Guid? ReadTenantId(EntityEntry entry, bool useOriginalValues) =>
        ReadGuid(entry, ["TenantId"], useOriginalValues);

    private static Guid? ReadActorId(EntityEntry entry, bool useOriginalValues) =>
        ReadGuid(entry, ["GrantedByUserId", "GrantedBy", "RevokedByUserId", "UpdatedByUserId", "CreatedByUserId", "AssignedBy", "PerformedBy"], useOriginalValues);

    private static Guid? ReadGuid(EntityEntry entry, IReadOnlyList<string> propertyNames, bool useOriginalValues)
    {
        foreach (var propertyName in propertyNames)
        {
            var property = entry.Properties.FirstOrDefault(candidate => candidate.Metadata.Name == propertyName);
            if (property is null)
            {
                continue;
            }

            var value = useOriginalValues ? property.OriginalValue : property.CurrentValue;
            if (value is Guid guid)
            {
                return guid;
            }

            if (value is string text && Guid.TryParse(text, out var parsedGuid))
            {
                return parsedGuid;
            }

            var nestedValue = value?.GetType().GetProperty("Value")?.GetValue(value);
            if (nestedValue is Guid nestedGuid)
            {
                return nestedGuid;
            }
        }

        return null;
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var configurations = GetGameGuildAssemblies()
            .SelectMany(LoadTypes)
            .Where(type => type is { IsClass: true, IsAbstract: false }
                           && typeof(IModelConfiguration).IsAssignableFrom(type)
                           && type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(Activator.CreateInstance)
            .OfType<IModelConfiguration>()
            .OrderBy(configuration => configuration.GetType().FullName, StringComparer.Ordinal)
            .ToList();

        foreach (var configuration in configurations)
        {
            configuration.Configure(modelBuilder);
        }

        base.OnModelCreating(modelBuilder);
    }

    private static IEnumerable<Type> LoadTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }

    private static IReadOnlyCollection<Assembly> GetGameGuildAssemblies()
    {
        ForceLoadGameGuildAssembliesFromOutput();

        var assembliesByName = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => IsGameGuildAssemblyName(assembly.GetName().Name))
            .GroupBy(assembly => assembly.GetName().Name!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var pending = new Queue<Assembly>(assembliesByName.Values);
        while (pending.Count > 0)
        {
            var assembly = pending.Dequeue();
            foreach (var reference in assembly.GetReferencedAssemblies()
                         .Where(reference => IsGameGuildAssemblyName(reference.Name)))
            {
                TryLoadReferencedAssembly(reference, assembliesByName, pending, Assembly.Load);
            }
        }

        return assembliesByName.Values.ToArray();
    }

    private static bool IsGameGuildAssemblyName(string? name) =>
        name?.StartsWith("GameGuild", StringComparison.Ordinal) == true;

    private static void TryLoadReferencedAssembly(
        AssemblyName reference,
        IDictionary<string, Assembly> assembliesByName,
        Queue<Assembly> pending,
        Func<AssemblyName, Assembly> loadAssembly)
    {
        var referenceName = reference.Name!;
        if (assembliesByName.ContainsKey(referenceName))
        {
            return;
        }

        try
        {
            var loaded = loadAssembly(reference);
            assembliesByName[referenceName] = loaded;
            pending.Enqueue(loaded);
        }
        catch
        {
            // Optional modules may be absent from focused test and design-time hosts.
        }
    }

    private static void ForceLoadGameGuildAssembliesFromOutput()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        foreach (var dll in Directory.GetFiles(baseDirectory, "GameGuild.*.dll", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var name = AssemblyName.GetAssemblyName(dll);
                if (ModuleConfiguration.IsTestAssembly(name.Name))
                {
                    continue;
                }

                if (AppDomain.CurrentDomain.GetAssemblies().All(assembly => assembly.FullName != name.FullName))
                {
                    Assembly.LoadFrom(dll);
                }
            }
            catch
            {
                // Optional modules may be absent from focused test and design-time hosts.
            }
        }
    }
}
