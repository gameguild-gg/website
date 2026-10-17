using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GameGuild.Sms;

namespace GameGuild.Notifications.Services.Sms;

/// <summary>
/// Host wrapper for <see cref="SmsDispatcherService"/>: scope-per-pass loop honoring Interval,
/// logging exceptions without crashing, first sweep delayed so startup is never blocked. Dormant
/// (single cheap sweep per interval) while <see cref="SmsDeliveryOptions.Enabled"/> is false
/// because no SMS rows are queued in that state.
/// </summary>
public sealed class SmsDispatcherBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<SmsDispatcherOptions> options,
    IOptions<SmsDeliveryOptions> deliveryOptions,
    ILogger<SmsDispatcherBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!deliveryOptions.Value.Enabled)
        {
            logger.LogInformation("SmsDispatcherBackgroundService is dormant because SMS delivery is disabled (no provider configured)");
        }

        logger.LogInformation("SmsDispatcherBackgroundService started");

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
                var dispatcher = scope.ServiceProvider.GetRequiredService<SmsDispatcherService>();
                var processed = await dispatcher.SweepOnceAsync(stoppingToken).ConfigureAwait(false);
                if (processed > 0)
                {
                    logger.LogInformation("SMS dispatcher sweep processed {Count} notification(s)", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SMS dispatcher sweep failed; will retry on next tick");
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
