namespace GameGuild.CQRS;

/// <summary>
///     Marker interface to represent a notification
/// </summary>
public interface INotification { }

/// <summary>
///     Notification publisher interface
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    ///     Publishes a notification to all handlers
    /// </summary>
    Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken);
}

/// <summary>
///     Publish notifications to multiple handlers
/// </summary>
public interface IPublisher
{
    /// <summary>
    ///     Asynchronously send a notification to multiple handlers
    /// </summary>
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;

    /// <summary>
    ///     Asynchronously send an object notification to multiple handlers via dynamic dispatch
    /// </summary>
    Task Publish(object notification, CancellationToken cancellationToken = default);
}

/// <summary>
///     Base notification handler executor
/// </summary>
public abstract class NotificationHandlerExecutor
{
    /// <summary>
    ///     Executes the handler
    /// </summary>
    public abstract Task ExecuteHandler(INotification notification, CancellationToken cancellationToken);
}

/// <summary>
///     Typed notification handler executor that adapts an <see cref="INotificationHandler{TNotification}" />
///     into the abstract <see cref="NotificationHandlerExecutor" /> contract.
/// </summary>
/// <typeparam name="TNotification">Notification type</typeparam>
public sealed class NotificationHandlerExecutorAdapter<TNotification> : NotificationHandlerExecutor where TNotification : INotification
{
    private readonly INotificationHandler<TNotification> _handler;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NotificationHandlerExecutorAdapter{TNotification}" /> class.
    /// </summary>
    public NotificationHandlerExecutorAdapter(INotificationHandler<TNotification> handler) { _handler = handler; }

    /// <summary>
    ///     Executes the handler with type-safe notification dispatch.
    /// </summary>
    public override Task ExecuteHandler(INotification notification, CancellationToken cancellationToken)
    {
        if (notification is not TNotification typed)
        {
            throw new InvalidOperationException(
                $"Expected notification of type {typeof(TNotification).Name} but received {notification.GetType().Name}.");
        }

        return _handler.Handle(typed, cancellationToken);
    }
}
