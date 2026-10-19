using GameGuild.API.Database;
using GameGuild.API.Eventing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests.Infrastructure;

/// <summary>
/// Observes one real durable event; an empty dispatch batch can mean another
/// worker owns its lease and is not evidence that delivery has finished.
/// </summary>
internal static class DurableEventDeliveryWaiter
{
    public static async Task WaitForCompletionAsync(
        IServiceProvider services, Guid eventId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("An event identifier is required.", nameof(eventId));
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            while (true)
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var message = await db.Set<OutboxMessage>().AsNoTracking()
                    .SingleOrDefaultAsync(value => value.EventId == eventId, deadline.Token)
                    ?? throw new InvalidOperationException("The expected durable event was not recorded.");
                var consumerFailed = await db.Set<InboxReceipt>().AsNoTracking().AnyAsync(value =>
                    value.EventId == eventId && value.CompletedAtUtc == null && value.AttemptCount > 0,
                    deadline.Token);
                if (message.DeadLetteredAtUtc.HasValue || consumerFailed)
                {
                    // Do not retry past a transport/consumer failure to make a test pass.
                    // Exception payloads can contain private event data and are not echoed.
                    throw new InvalidOperationException("The expected durable event has a failed consumer or dead letter.");
                }
                if (message.CompletedAtUtc.HasValue)
                {
                    return;
                }
                await scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>().DispatchPendingAsync(deadline.Token);
                await Task.Delay(TimeSpan.FromMilliseconds(50), deadline.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The expected durable event did not complete within thirty seconds.");
        }
    }
}
