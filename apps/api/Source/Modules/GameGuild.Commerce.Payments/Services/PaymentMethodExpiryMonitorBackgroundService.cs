using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Daily payment-method expiry monitoring (issue #414). Checks card expiry horizons
///     (30/14/7 days), issues notifications through the Notifications module and auto-expires
///     cards whose expiry month has passed. Opt-out via
///     <c>PaymentMethodExpiry:WorkerEnabled=false</c>; database failures are logged and
///     retried on the next tick instead of crashing the host.
/// </summary>
public sealed class PaymentMethodExpiryMonitorBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<PaymentMethodExpiryOptions> options,
    ILogger<PaymentMethodExpiryMonitorBackgroundService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled)
        {
            logger.LogInformation(
                "PaymentMethodExpiryMonitorBackgroundService is disabled (PaymentMethodExpiry:WorkerEnabled=false); expiry notices stay off.");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, options.Value.WorkerIntervalHours));
        logger.LogInformation("PaymentMethodExpiryMonitorBackgroundService started with interval {Interval}.", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var monitor = scope.ServiceProvider.GetRequiredService<IPaymentMethodExpiryMonitor>();
                await monitor.RunPassAsync(SystemClock.UtcNow, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Payment method expiry pass failed; will retry on next tick");
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
