using System.Reflection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using GameGuild.API.Eventing;
using GameGuild.API.Setup;
using GameGuild.CQRS;

namespace GameGuild.API.Database;

/// <summary>
///     Thin-shell database context that delegates module-specific configuration
///     to <see cref="IModelConfiguration"/> implementations discovered via assembly scanning.
/// </summary>
public class ApplicationDbContext : DbContext, IApplicationDbContext, IDataProtectionKeyContext
{
    private readonly IUseCaseOperationContextAccessor? _useCaseOperationContextAccessor;

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
        IUseCaseOperationContextAccessor useCaseOperationContextAccessor) : this(options, publisher)
    {
        _useCaseOperationContextAccessor = useCaseOperationContextAccessor;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
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
            return;

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
