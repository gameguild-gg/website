using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Replays spooled security events back into the audit database after an outage.
///     The drainer stops at the first event that still cannot be persisted and retries
///     on the next cycle, preserving spool order.
/// </summary>
public sealed class SecurityEventSpoolDrainerBackgroundService(
    IServiceScopeFactory scopeFactory,
    ISecurityEventPipelineStatusTracker statusTracker,
    IOptions<SecurityEventPipelineOptions> options,
    ILogger<SecurityEventSpoolDrainerBackgroundService> logger) : BackgroundService
{
    private readonly TimeSpan interval = options.Value.DrainInterval is { } value && value > TimeSpan.Zero
        ? value
        : TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Security event spool drainer started with interval {Interval}", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            statusTracker.RecordDrainAttempt(SystemClock.UtcNow);
            try
            {
                using var scope = scopeFactory.CreateScope();
                var securityEventLogger = scope.ServiceProvider.GetRequiredService<ISecurityEventLogger>();
                var delivered = await securityEventLogger.ReplaySpooledEventsAsync(stoppingToken).ConfigureAwait(false);
                if (delivered > 0)
                {
                    logger.LogInformation("Replayed {Count} spooled security events to the audit database", delivered);
                }

                statusTracker.RecordDrainSuccess(SystemClock.UtcNow);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                statusTracker.RecordDrainFailure(SystemClock.UtcNow, exception.Message);
                logger.LogError(exception, "Security event spool drain failed; will retry on the next cycle");
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
