using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GameGuild.Push;

namespace GameGuild.Notifications.Services.Push;

/// <summary>
/// Host wrapper for <see cref="PushDispatcherService"/>: scope-per-pass loop honoring Interval,
/// logging exceptions without crashing, first sweep delayed so startup is never blocked. Dormant
/// (single cheap sweep per interval) while <see cref="PushDeliveryOptions.Enabled"/> is false
/// because no push rows are queued in that state.
/// </summary>
public sealed class PushDispatcherBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<PushDispatcherOptions> options,
    IOptions<PushDeliveryOptions> deliveryOptions,
    ILogger<PushDispatcherBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!deliveryOptions.Value.Enabled)
        {
            logger.LogInformation("PushDispatcherBackgroundService is dormant because push delivery is disabled (no provider configured)");
        }

        logger.LogInformation("PushDispatcherBackgroundService started");

        try
        {
            await Task.Delay(InitialDelay, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<PushDispatcherService>();
                var processed = await dispatcher.SweepOnceAsync(stoppingToken).ConfigureAwait(false);
                if (processed > 0)
                {
                    logger.LogInformation("Push dispatcher sweep processed {Count} notification(s)", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Push dispatcher sweep failed; will retry on next tick");
            }

            try
            {
                await Task.Delay(options.Value.Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
