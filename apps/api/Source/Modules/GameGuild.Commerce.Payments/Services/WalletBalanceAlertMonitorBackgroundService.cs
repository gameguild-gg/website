using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Periodic low-balance wallet monitoring (issue #406). Issues
///     <c>WalletBalanceBelowThreshold</c> notifications through the Notifications module and
///     re-arms them after recovery. Opt-out via <c>Wallets:BalanceAlertsEnabled=false</c>;
///     database failures are logged and retried on the next tick instead of crashing the host.
/// </summary>
public sealed class WalletBalanceAlertMonitorBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<WalletsOptions> options,
    ILogger<WalletBalanceAlertMonitorBackgroundService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.BalanceAlertsEnabled)
        {
            logger.LogInformation(
                "WalletBalanceAlertMonitorBackgroundService is disabled (Wallets:BalanceAlertsEnabled=false); low-balance notices stay off.");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, options.Value.BalanceAlertIntervalHours));
        logger.LogInformation("WalletBalanceAlertMonitorBackgroundService started with interval {Interval}.", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var monitor = scope.ServiceProvider.GetRequiredService<IWalletBalanceAlertMonitor>();
                await monitor.RunPassAsync(SystemClock.UtcNow, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Wallet low-balance pass failed; will retry on next tick");
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
