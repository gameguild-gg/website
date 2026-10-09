using System.Collections;
using System.Collections.Concurrent;

namespace GameGuild.CQRS.Implementation;

/// <summary>
///     Handles notification publishing with optimized O(n) enumeration and compiled delegate caching.
/// </summary>
internal class MediatorPublisher : IPublisher
{
    // O(1) lookup cache for performance
    // ReSharper disable InconsistentNaming
#pragma warning disable IDE1006 // Naming Styles - Static readonly fields intentionally use camelCase for cache fields
    private static readonly ConcurrentDictionary<Type, Func<object, NotificationHandlerExecutor>> s_executorFactoryCache = new();
#pragma warning restore IDE1006
    // ReSharper restore InconsistentNaming

    private readonly INotificationPublisher _notificationPublisher;
    private readonly ServiceFactory _serviceFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MediatorPublisher" /> class.
    /// </summary>
    /// <param name="serviceFactory">Service factory</param>
    /// <param name="notificationPublisher">Notification publisher</param>
    public MediatorPublisher(ServiceFactory serviceFactory, INotificationPublisher notificationPublisher)
    {
        _serviceFactory = serviceFactory;
        _notificationPublisher = notificationPublisher;
    }

    // ── Publish (typed) ────────────────────────────────────────────────────

    /// <summary>
    ///     Publish a notification to all handlers with optimized O(n) enumeration.
    /// </summary>
    public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        var handlerType = typeof(INotificationHandler<TNotification>);
        var handlers = _serviceFactory(typeof(IEnumerable<>).MakeGenericType(handlerType)) as IEnumerable<INotificationHandler<TNotification>>;

        if (handlers == null)
        {
            return;
        }

        var handlerArray = handlers as INotificationHandler<TNotification>[] ?? handlers.ToArray();
        if (handlerArray.Length == 0)
        {
            return;
        }

        var executors = new NotificationHandlerExecutorAdapter<TNotification>[handlerArray.Length];
        for (var i = 0; i < handlerArray.Length; i++)
        {
            executors[i] = new NotificationHandlerExecutorAdapter<TNotification>(handlerArray[i]);
        }

        await _notificationPublisher.Publish(executors, notification, cancellationToken).ConfigureAwait(false);
    }

    // ── Publish (object / dynamic dispatch) ────────────────────────────────

    /// <summary>
    ///     Publish a notification to all handlers via dynamic dispatch.
    /// </summary>
    public async Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification is not INotification notificationInstance)
        {
            throw new InvalidOperationException($"Object {notification.GetType()} does not implement INotification");
        }

        var notificationType = notification.GetType();
        var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(handlerType);
        var handlers = _serviceFactory(enumerableType) as IEnumerable;

        if (handlers == null)
        {
            return;
        }

        var executors = new List<NotificationHandlerExecutor>();
        foreach (var handler in handlers)
        {
            var executorFactory = s_executorFactoryCache.GetOrAdd(notificationType, static nt =>
            {
                var executorType = typeof(NotificationHandlerExecutorAdapter<>).MakeGenericType(nt);
                var ctor = executorType.GetConstructors()[0];
                var param = System.Linq.Expressions.Expression.Parameter(typeof(object), "h");
                var body = System.Linq.Expressions.Expression.New(
                    ctor,
                    System.Linq.Expressions.Expression.Convert(param, ctor.GetParameters()[0].ParameterType));
                var lambda = System.Linq.Expressions.Expression.Lambda<Func<object, NotificationHandlerExecutor>>(
                    body, param);
                return lambda.Compile();
            });

            var executor = executorFactory(handler!);
            executors.Add(executor);
        }

        if (executors.Count > 0)
        {
            await _notificationPublisher.Publish(executors, notificationInstance, cancellationToken).ConfigureAwait(false);
        }
    }
}
