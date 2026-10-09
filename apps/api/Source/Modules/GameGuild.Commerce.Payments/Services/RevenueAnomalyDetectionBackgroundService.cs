using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Periodic revenue anomaly detection (issue #404). Opt-in via
///     <c>RevenueAuditing:WorkerEnabled</c> — the safe default is disabled. Each pass
///     evaluates the trailing days for the global revenue aggregate and persists
///     idempotent alerts; database failures are logged and retried on the next tick
///     instead of crashing the host.
/// </summary>
public sealed class RevenueAnomalyDetectionBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<RevenueAuditingOptions> options,
    ILogger<RevenueAnomalyDetectionBackgroundService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled)
        {
            logger.LogInformation(
                "RevenueAnomalyDetectionBackgroundService is disabled (RevenueAuditing:WorkerEnabled=false); automated revenue anomaly detection stays off.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.WorkerIntervalMinutes));
        logger.LogInformation(
            "RevenueAnomalyDetectionBackgroundService started with interval {Interval}.",
            interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var anomalyService = scope.ServiceProvider.GetRequiredService<IRevenueAnomalyService>();
                var created = await anomalyService
                    .DetectAndPersistAsync(SystemClock.UtcNow, tenantId: null, stoppingToken)
                    .ConfigureAwait(false);
                if (created > 0)
                {
                    logger.LogInformation(
                        "Automated revenue anomaly detection created {Created} alert(s).",
                        created);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Revenue anomaly detection pass failed; will retry on next tick");
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
