using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Processes one pass of the automated payment retry queue (issue #415). A pass claims
///     nothing by itself: it selects the failed payments whose scheduled next-retry time has
///     passed via <see cref="IPaymentRepository.GetDueForRetryAsync" /> and dispatches each one
///     through the existing <see cref="RetryPaymentCommand" /> retry path. The handler's
///     persisted Failed → Pending claim transition prevents double-fire, per-item failures are
///     isolated so one payment never aborts the batch, and the batch is capped by
///     <c>Payments:RetryQueue:MaxBatchSize</c>.
/// </summary>
public interface IPaymentRetryQueueWorker
{
    /// <summary>Processes one polling cycle and returns the number of dispatched retries.</summary>
    Task<int> ProcessDueAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class PaymentRetryQueueWorker(
    IPaymentRepository paymentRepository,
    ISender sender,
    IActorContextAccessor actorContextAccessor,
    IOptions<PaymentRetryQueueOptions> queueOptions,
    ILogger<PaymentRetryQueueWorker> logger) : IPaymentRetryQueueWorker
{
    /// <inheritdoc />
    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var settings = queueOptions.Value;
        if (!settings.Enabled)
        {
            return 0;
        }

        var startedAt = SystemClock.UtcNow;
        var due = (await paymentRepository
                .GetDueForRetryAsync(cancellationToken, take: settings.MaxBatchSize)
                .ConfigureAwait(false))
            .ToList();

        var dispatched = 0;
        foreach (var payment in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await RunAsRetryQueueSystemActorAsync(
                        () => sender.Send(new RetryPaymentCommand(payment.Id), cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);

                dispatched++;
                PaymentRetryQueueMetrics.RecordDispatched(payment.Provider);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // One payment must never abort the cycle: the payment stays in its recorded
                // state and is reconsidered on the next poll.
                logger.LogError(exception,
                    "Automated retry dispatch failed for payment {PaymentId}.",
                    payment.Id);
            }
        }

        PaymentRetryQueueMetrics.RecordCycle(SystemClock.UtcNow - startedAt);

        if (dispatched > 0 || due.Count > 0)
        {
            logger.LogInformation(
                "Payment retry queue cycle dispatched {Dispatched} of {Due} due payments.",
                dispatched,
                due.Count);
        }

        return dispatched;
    }

    /// <summary>
    ///     Retries dispatched from the queue run as the well-known system actor so the retry
    ///     path's audit trail can attribute them; the context is scoped to this dispatch and
    ///     restored afterwards.
    /// </summary>
    private async Task RunAsRetryQueueSystemActorAsync(Func<Task> dispatch, CancellationToken cancellationToken)
    {
        var previousContext = actorContextAccessor.ActorContext;
        var systemContext = ActorContextBuilder.ForSystem("PaymentRetryQueue").Build();
        actorContextAccessor.SetActorContext(systemContext);
        try
        {
            await dispatch().ConfigureAwait(false);
        }
        finally
        {
            actorContextAccessor.SetActorContext(previousContext);
        }
    }
}

/// <summary>
///     Hosted service that polls failed payments past their scheduled next-retry time and
///     dispatches them through the existing retry path (issue #415). Opt-in via
///     <c>Payments:RetryQueue:Enabled</c> (safe default: disabled, matching the sibling
///     revenue-auditing worker); the default poll interval is hourly (60 minutes).
/// </summary>
public sealed class PaymentRetryQueueBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<PaymentRetryQueueOptions> queueOptions,
    ILogger<PaymentRetryQueueBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = queueOptions.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation(
                "PaymentRetryQueueBackgroundService is disabled (Payments:RetryQueue:Enabled=false); automated payment retries stay off.");
            return;
        }

        var pollInterval = TimeSpan.FromMinutes(Math.Max(1, settings.PollIntervalMinutes));
        logger.LogInformation(
            "PaymentRetryQueueBackgroundService started with a {Interval} poll interval.",
            pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IPaymentRetryQueueWorker>()
                    .ProcessDueAsync(stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Payment retry queue cycle failed");
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
