namespace GameGuild.CQRS.Publishers;

/// <summary>
///     Publisher that executes handlers in parallel using Task.WhenAll with optimized O(n) enumeration
/// </summary>
public sealed class TaskWhenAllPublisher : INotificationPublisher
{
    /// <summary>
    ///     Publishes a notification to all handlers in parallel
    /// </summary>
    /// <param name="handlerExecutors">Handler executors</param>
    /// <param name="notification">Notification</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        // Convert to array once for efficient parallel execution - O(n)
        var executors = handlerExecutors as NotificationHandlerExecutor[ ] ?? handlerExecutors.ToArray();

        if (executors.Length == 0)
        {
            return;
        }

        // Pre-allocate array for better performance - O(1) allocation
        var tasks = new Task[executors.Length];

        // Create all tasks - O(n)
        for (var i = 0; i < executors.Length; i++) { tasks[i] = executors[i].ExecuteHandler(notification, cancellationToken); }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
