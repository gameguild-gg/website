using System.Collections.Concurrent;
using System.Reflection;
using GameGuild.CQRS.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild;

/// <summary>
///     In-memory implementation of <see cref="IIntegrationEventBus"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>⚠️ In-Memory Only:</b> Events published through this bus are delivered in-process only.
/// Events are <b>lost</b> if the process crashes before handlers complete. There is no durable
/// queue, no retry-with-backoff, and no cross-service delivery.
/// </para>
/// <para>
/// <b>Production Migration Path:</b> Replace with a message-broker-backed implementation
/// (RabbitMQ, Azure Service Bus, Amazon SQS, Kafka, etc.) that provides:
/// <list type="bullet">
///     <item><description>Durable message persistence (survives process restarts)</description></item>
///     <item><description>At-least-once delivery guarantees</description></item>
///     <item><description>Cross-service / cross-process event delivery</description></item>
///     <item><description>Dead-letter queues for poison messages</description></item>
///     <item><description>Retry policies with exponential backoff</description></item>
/// </list>
/// The <see cref="IIntegrationEventBus"/> interface is already abstracted, so swapping
/// the implementation requires only a DI registration change.
/// </para>
/// </remarks>
public sealed class InMemoryIntegrationEventBus : IIntegrationEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryIntegrationEventBus> _logger;
    private readonly IntegrationEventOptions _options;

    /// <summary>
    ///     Cache for handler types to avoid repeated reflection
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Type> HandlerTypeCache = new();

    /// <summary>
    ///     Cache for resolved PublishAsync generic methods per event type
    /// </summary>
    private static readonly ConcurrentDictionary<Type, MethodInfo?> PublishMethodCache = new();

    public InMemoryIntegrationEventBus(
        IServiceProvider serviceProvider,
        ILogger<InMemoryIntegrationEventBus> logger,
        IOptions<IntegrationEventOptions>? options = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options?.Value ?? new IntegrationEventOptions();
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (@event is IDurableIntegrationEvent)
            throw new InvalidOperationException(
                "Durable integration events must be recorded in the transactional outbox and dispatched through the inbox-aware outbox dispatcher.");

        var eventType = typeof(TEvent);
        var eventName = eventType.Name;

        _logger.LogDebug(
            "Publishing integration event {EventName} (Id: {EventId}) from {SourceModule}",
            eventName, @event.EventId, @event.SourceModule);

        using var scope = _serviceProvider.CreateScope();

        // Get all handlers for this event type
        var handlerType = HandlerTypeCache.GetOrAdd(
            eventType,
            static et => typeof(IIntegrationEventHandler<>).MakeGenericType(et));

        var enumerableType = typeof(IEnumerable<>).MakeGenericType(handlerType);
        var handlers = scope.ServiceProvider.GetService(enumerableType) as IEnumerable<object>;

        if (handlers == null)
        {
            _logger.LogDebug("No handlers registered for integration event {EventName}", eventName);
            return;
        }

        var handlerList = handlers.ToList();

        if (handlerList.Count == 0)
        {
            _logger.LogDebug("No handlers registered for integration event {EventName}", eventName);
            return;
        }

        _logger.LogDebug("Found {HandlerCount} handlers for integration event {EventName}", handlerList.Count, eventName);

        if (_options.RunHandlersInParallel)
        {
            await ExecuteHandlersInParallel(@event, handlerList, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await ExecuteHandlersSequentially(@event, handlerList, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync(IEnumerable<IIntegrationEvent> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var @event in events)
        {
            // Use compiled expression delegate to call the generic PublishAsync<TEvent> method
            var eventType = @event.GetType();

            // Find the generic PublishAsync<TEvent> method and make a concrete version
            var method = PublishMethodCache.GetOrAdd(eventType, static et =>
            {
                var methods = typeof(InMemoryIntegrationEventBus)
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance);

                foreach (var m in methods)
                {
                    if (m.Name != nameof(PublishAsync) || !m.IsGenericMethodDefinition)
                        continue;

                    var parameters = m.GetParameters();
                    if (parameters.Length == 2 &&
                        parameters[1].ParameterType == typeof(CancellationToken))
                        return m.MakeGenericMethod(et);
                }

                return null;
            });

            if (method != null)
            {
                var compiledPublish = ExpressionTreeCompiler.GetOrCompile(method);
                await compiledPublish(this, [@event, cancellationToken]).ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning(
                    "Could not resolve PublishAsync method for event type {EventType}",
                    eventType.Name);
            }
        }
    }

    private async Task ExecuteHandlersSequentially<TEvent>(
        TEvent @event,
        List<object> handlers,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        foreach (var handler in handlers)
        {
            await ExecuteHandler(@event, handler, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteHandlersInParallel<TEvent>(
        TEvent @event,
        List<object> handlers,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        var tasks = handlers.Select(h => ExecuteHandler(@event, h, cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task ExecuteHandler<TEvent>(
        TEvent @event,
        object handler,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        var handlerTypeName = handler.GetType().Name;
        var eventName = typeof(TEvent).Name;

        try
        {
            _logger.LogDebug(
                "Executing handler {HandlerName} for event {EventName} (Id: {EventId})",
                handlerTypeName, eventName, @event.EventId);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_options.HandlerTimeout);

            if (handler is IIntegrationEventHandler<TEvent> typedHandler)
            {
                await typedHandler.HandleAsync(@event, cts.Token).ConfigureAwait(false);
            }
            else
            {
                // Fallback to compiled expression delegate for non-generic handler resolution
                var handleMethod = handler.GetType().GetMethod("HandleAsync");
                if (handleMethod != null)
                {
                    var compiledHandle = ExpressionTreeCompiler.GetOrCompile(handleMethod);
                    await compiledHandle(handler, [@event, cts.Token]).ConfigureAwait(false);
                }
            }

            _logger.LogDebug(
                "Handler {HandlerName} completed for event {EventName} (Id: {EventId})",
                handlerTypeName, eventName, @event.EventId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Handler {HandlerName} was cancelled for event {EventName} (Id: {EventId})",
                handlerTypeName, eventName, @event.EventId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Handler {HandlerName} failed for event {EventName} (Id: {EventId})",
                handlerTypeName, eventName, @event.EventId);

            // Invoke dead-letter / error callback if configured
            try
            {
                _options.OnHandlerError?.Invoke(@event, ex, handler.GetType());
            }
            catch (Exception callbackEx)
            {
                _logger.LogError(callbackEx, "OnHandlerError callback failed for {HandlerName}", handlerTypeName);
            }

            if (_options.ThrowOnHandlerException)
                throw;
        }
    }

}

/// <summary>
///     Extension methods for registering integration event services.
/// </summary>
public static class IntegrationEventServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the integration event bus to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddIntegrationEventBus(
        this IServiceCollection services,
        Action<IntegrationEventOptions>? configure = null)
    {
        if (configure != null)
            services.Configure(configure);

        services.AddSingleton<IIntegrationEventBus, InMemoryIntegrationEventBus>();

        return services;
    }

    /// <summary>
    ///     Registers an integration event handler.
    /// </summary>
    /// <typeparam name="TEvent">The event type</typeparam>
    /// <typeparam name="THandler">The handler type</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        return services;
    }
}
