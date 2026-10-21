using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Processes one pass of the automated refund workflow (issues #403/#419): refunds that were
///     approved (directly or through the pending-approval queue) are executed through the shared
///     <see cref="IRefundProcessingService.ExecuteAsync"/> core, and failed refunds whose retry
///     cooldown elapsed are retried until the execution-attempt budget
///     (<c>Payments:Refunds:Workflow:MaxExecutionAttempts</c>, default 3) is exhausted. Per-refund
///     failures are isolated so one refund never aborts the batch.
/// </summary>
public interface IRefundWorkflowWorker
{
    /// <summary>Processes one polling cycle and returns the number of executed refunds.</summary>
    Task<int> ProcessDueAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class RefundWorkflowWorker(
    IRefundRepository refundRepository,
    IRefundProcessingService refundProcessingService,
    IActorContextAccessor actorContextAccessor,
    IOptions<RefundWorkflowOptions> workflowOptions,
    ILogger<RefundWorkflowWorker> logger) : IRefundWorkflowWorker
{
    /// <inheritdoc />
    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var settings = workflowOptions.Value;
        if (!settings.Enabled)
        {
            return 0;
        }

        var retryEligibleAfter = SystemClock.UtcNow.AddMinutes(-settings.FailedRetryDelayMinutes);
        var due = await refundRepository
            .ListDueForWorkflowAsync(
                settings.MaxExecutionAttempts,
                retryEligibleAfter,
                settings.MaxBatchSize,
                cancellationToken)
            .ConfigureAwait(false);

        var executed = 0;
        foreach (var refund in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await RunAsWorkflowSystemActorAsync(
                        () => refundProcessingService.ExecuteAsync(refund, cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
                executed++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // One refund must never abort the cycle: the refund stays recorded in its
                // current state and is reconsidered on the next poll.
                logger.LogError(exception,
                    "Automated refund workflow execution failed for refund {RefundId}.",
                    refund.Id);
            }
        }

        if (executed > 0 || due.Count > 0)
        {
            logger.LogInformation(
                "Refund workflow cycle executed {Executed} of {Due} due refunds.",
                executed,
                due.Count);
        }

        return executed;
    }

    /// <summary>
    ///     Workflow executions run as a well-known system actor so the refund accounting's audit
    ///     trail can attribute them; the context is scoped to this execution and restored after.
    /// </summary>
    private async Task RunAsWorkflowSystemActorAsync(Func<Task> execution, CancellationToken cancellationToken)
    {
        var previousContext = actorContextAccessor.ActorContext;
        var systemContext = ActorContextBuilder.ForSystem("RefundWorkflow").Build();
        actorContextAccessor.SetActorContext(systemContext);
        try
        {
            await execution().ConfigureAwait(false);
        }
        finally
        {
            actorContextAccessor.SetActorContext(previousContext);
        }
    }
}

/// <summary>
///     Hosted service that polls approved and retry-eligible failed refunds and executes them
///     (issues #403/#419: "process approved refunds" every poll, "retry failed refunds" hourly).
///     Opt-in via <c>Payments:Refunds:Workflow:Enabled</c> (safe default: disabled, matching the
///     payment retry-queue worker); the default poll interval is 5 minutes.
/// </summary>
public sealed class RefundWorkflowBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<RefundWorkflowOptions> workflowOptions,
    ILogger<RefundWorkflowBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = workflowOptions.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation(
                "RefundWorkflowBackgroundService is disabled (Payments:Refunds:Workflow:Enabled=false); automated refund processing stays off.");
            return;
        }

        var pollInterval = TimeSpan.FromMinutes(Math.Max(1, settings.PollIntervalMinutes));
        logger.LogInformation(
            "RefundWorkflowBackgroundService started with a {Interval} poll interval.",
            pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IRefundWorkflowWorker>()
                    .ProcessDueAsync(stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Refund workflow cycle failed");
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
