namespace GameGuild;

/// <summary>
///     Marker interface for integration events that cross module boundaries.
///     Integration events are used for loose coupling between modules, allowing
///     them to communicate without direct dependencies.
/// </summary>
/// <remarks>
///     <para>
///         Unlike domain events (which are internal to a module), integration events
///         are designed for cross-module communication. They should only contain
///         primitive types and DTOs to avoid sharing domain entities across modules.
///     </para>
///     <para>
///         Example usage:
///         - UserCreatedIntegrationEvent: Published by Users module, consumed by Notifications module
///         - PaymentCompletedIntegrationEvent: Published by Payments module, consumed by Subscriptions module
///     </para>
/// </remarks>
public interface IIntegrationEvent
{
    /// <summary>
    ///     Gets the unique identifier for this event instance.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    ///     Gets the timestamp when this event occurred.
    /// </summary>
    DateTime OccurredAt { get; }

    /// <summary>
    ///     Gets the name of the module that published this event.
    /// </summary>
    string SourceModule { get; }
}

/// <summary>
///     Base implementation of <see cref="IIntegrationEvent"/> with sensible defaults.
///     Uses <see cref="SystemClock.UtcNow"/> for testable timestamps.
/// </summary>
public abstract record IntegrationEventBase : IIntegrationEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime OccurredAt { get; init; } = SystemClock.UtcNow;

    /// <inheritdoc />
    public abstract string SourceModule { get; }
}

public interface IDurableIntegrationEvent : IIntegrationEvent
{
    string EventName { get; }
    int SchemaVersion { get; }
    Guid TenantId { get; }
    Guid ActorId { get; }
    string AggregateType { get; }
    string AggregateId { get; }
    Guid CorrelationId { get; }
    Guid? CausationId { get; }
}

public abstract record DurableIntegrationEventBase : IntegrationEventBase, IDurableIntegrationEvent
{
    public abstract string EventName { get; }
    public virtual int SchemaVersion => 1;
    public Guid TenantId { get; init; }
    public Guid ActorId { get; init; }
    public required string AggregateType { get; init; }
    public required string AggregateId { get; init; }
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public Guid? CausationId { get; init; }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class NonPersonalEventDataAttribute : Attribute;

public static class DurableIntegrationEventActors
{
    public static readonly Guid System = Guid.Parse("00000000-0000-0000-0000-000000000001");
}

public static class DurableIntegrationEventTenants
{
    public static readonly Guid Platform = Guid.Parse("00000000-0000-0000-0000-000000000002");
}

public interface IHasIntegrationEvents
{
    IReadOnlyList<IDurableIntegrationEvent> IntegrationEvents { get; }
    void AddIntegrationEvent(IDurableIntegrationEvent integrationEvent);
    void ClearIntegrationEvents();
}

public interface IDurableEventProducer
{
    Task RecordAsync(IDurableIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}

/// <summary>
///     Handler for integration events.
///     Each module can register handlers for integration events from other modules.
/// </summary>
/// <typeparam name="TEvent">The type of integration event to handle</typeparam>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    /// <summary>
    ///     Handles the integration event.
    /// </summary>
    /// <param name="event">The event to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
///     Event bus for publishing integration events across module boundaries.
///     Provides loose coupling between modules without circular dependencies.
/// </summary>
public interface IIntegrationEventBus
{
    /// <summary>
    ///     Publishes an integration event to all registered handlers.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to publish</typeparam>
    /// <param name="event">The event to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;

    /// <summary>
    ///     Publishes multiple integration events.
    /// </summary>
    /// <param name="events">The events to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task PublishAsync(IEnumerable<IIntegrationEvent> events, CancellationToken cancellationToken = default);
}

/// <summary>
///     Options for configuring integration event handling.
/// </summary>
public sealed class IntegrationEventOptions
{
    /// <summary>
    ///     Gets or sets whether to throw exceptions from handlers.
    ///     If false (default), exceptions are logged but don't prevent other handlers from running.
    /// </summary>
    public bool ThrowOnHandlerException { get; set; }

    /// <summary>
    ///     Gets or sets whether to run handlers in parallel.
    ///     If false (default), handlers run sequentially in registration order.
    /// </summary>
    public bool RunHandlersInParallel { get; set; }

    /// <summary>
    ///     Gets or sets the timeout for individual handler execution.
    ///     Default is 30 seconds.
    /// </summary>
    public TimeSpan HandlerTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Optional callback invoked when a handler fails.
    ///     Use this for dead-letter logging, metrics, or retry scheduling.
    ///     Parameters: (event, exception, handlerType).
    /// </summary>
    public Action<IIntegrationEvent, Exception, Type>? OnHandlerError { get; set; }
}
