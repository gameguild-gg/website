using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Scheduled FX rate sync worker (issue #412 spec B): runs one sync pass per day at
/// <c>FxRates:SyncTimeUtc</c> (default 00:00 UTC), persisting fresh rate snapshots so the
/// conversion path and historical lookups always have yesterday-close data. Opt-in —
/// <c>FxRates:WorkerEnabled=false</c> (the default) keeps the worker dormant; the admin
/// sync endpoint can still trigger passes manually. <c>FxRates:IntervalMinutes</c> overrides
/// the daily schedule with a fixed polling interval for development/tests.
/// </summary>
public sealed class FxRateSyncBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<FxSyncOptions> options,
    ILogger<FxRateSyncBackgroundService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.WorkerEnabled)
        {
            logger.LogInformation(
                "FxRateSyncBackgroundService is disabled (FxRates:WorkerEnabled=false); FX snapshots are only refreshed by manual sync runs.");
            return;
        }

        logger.LogInformation(
            "FxRateSyncBackgroundService started (provider {Provider}, schedule {Schedule}).",
            settings.Provider,
            settings.IntervalMinutes > 0 ? $"every {settings.IntervalMinutes}m" : $"daily at {settings.SyncTimeUtc} UTC");

        // First pass shortly after boot so a restarted host does not wait a full day for rates.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
            await RunPassAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "FX sync startup pass failed; the scheduled passes continue.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = settings.IntervalMinutes > 0
                    ? TimeSpan.FromMinutes(settings.IntervalMinutes)
                    : DelayUntilNextSyncTime(settings.SyncTimeUtc);
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                await RunPassAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "FX sync pass failed; retrying on the next scheduled tick.");
            }
        }
    }

    private async Task RunPassAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<IFxRateSyncService>();
        var result = await syncService.SyncAsync(stoppingToken).ConfigureAwait(false);
        if (result.Status == FxRateSyncRunStatus.Failed)
        {
            logger.LogError("Scheduled FX sync failed ({Failures} consecutive): {Error}.", result.ConsecutiveFailures, result.ErrorMessage);
        }
    }

    private static TimeSpan DelayUntilNextSyncTime(string syncTimeUtc)
    {
        if (!TimeSpan.TryParse(syncTimeUtc, System.Globalization.CultureInfo.InvariantCulture, out var syncTime)
            || syncTime < TimeSpan.Zero || syncTime >= TimeSpan.FromDays(1))
        {
            syncTime = TimeSpan.Zero;
        }

        var now = SystemClock.UtcNow;
        var next = now.Date.Add(syncTime);
        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next - now;
    }
}
