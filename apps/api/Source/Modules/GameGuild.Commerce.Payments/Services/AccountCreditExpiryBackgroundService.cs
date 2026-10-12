using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Daily account credit expiry worker (issue #410 v1). Each tick runs one bounded
///     pass of <see cref="IAccountCreditService.ExpireDueAsync" /> flipping due credit
///     grants and unredeemed gifts to Expired, with per-pass OpenTelemetry metrics.
///     Opt-out via <c>AccountCredits:WorkerEnabled=false</c>; database failures are logged
///     and retried on the next tick instead of crashing the host.
/// </summary>
public sealed class AccountCreditExpiryBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<AccountCreditOptions> options,
    ILogger<AccountCreditExpiryBackgroundService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled)
        {
            logger.LogInformation(
                "AccountCreditExpiryBackgroundService is disabled (AccountCredits:WorkerEnabled=false); credit and gift expiry stay off.");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, options.Value.WorkerIntervalHours));
        logger.LogInformation("AccountCreditExpiryBackgroundService started with interval {Interval}.", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var creditService = scope.ServiceProvider.GetRequiredService<IAccountCreditService>();
                var started = System.Diagnostics.Stopwatch.StartNew();
                var result = await creditService
                    .ExpireDueAsync(SystemClock.UtcNow, options.Value.ExpiryBatchSize, stoppingToken)
                    .ConfigureAwait(false);
                started.Stop();
                AccountCreditMetrics.RecordExpiryCycle(started.Elapsed);

                if (result.ExpiredGrants > 0 || result.ExpiredGifts > 0)
                {
                    logger.LogInformation(
                        "Account credit expiry pass expired {ExpiredGrants} grant(s) ({ExpiredAmount} wasted) and {ExpiredGifts} gift(s).",
                        result.ExpiredGrants,
                        result.ExpiredGrantAmount,
                        result.ExpiredGifts);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Account credit expiry pass failed; will retry on next tick");
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
