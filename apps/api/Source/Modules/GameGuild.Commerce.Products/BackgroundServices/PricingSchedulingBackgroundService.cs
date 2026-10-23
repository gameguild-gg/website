using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Products;

/// <summary>
///     Automated pricing scheduling worker (issue #395). Each tick runs one bounded pass of
///     <see cref="IPricingExperimentService.RunSchedulingSweepAsync"/>: opt-in scheduled
///     pricing rules (<see cref="GameGuild.Commerce.PricingRule.IsScheduled"/>) are
///     activated/deactivated to match their date window, and date-bounded experiments start
///     and complete at their bounds. Rule transitions bump the rule version, which changes
///     the engine's cache stamp so the new state is picked up immediately. Database failures
///     are logged and retried on the next tick instead of crashing the host.
/// </summary>
public sealed class PricingSchedulingBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<PricingSchedulingOptions> options,
    ILogger<PricingSchedulingBackgroundService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled)
        {
            logger.LogInformation(
                "PricingSchedulingBackgroundService is disabled (PricingScheduling:WorkerEnabled=false); rule and experiment scheduling stays off.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.WorkerIntervalMinutes));
        logger.LogInformation("PricingSchedulingBackgroundService started with interval {Interval}.", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var schedulingService = scope.ServiceProvider.GetRequiredService<IPricingExperimentService>();
                var result = await schedulingService
                    .RunSchedulingSweepAsync(SystemClock.UtcNow, stoppingToken)
                    .ConfigureAwait(false);

                if (result.RulesActivated > 0 || result.RulesDeactivated > 0 ||
                    result.ExperimentsStarted > 0 || result.ExperimentsCompleted > 0)
                {
                    logger.LogInformation(
                        "Pricing scheduling pass: {Activated} rule(s) activated, {Deactivated} deactivated, {Started} experiment(s) started, {Completed} completed.",
                        result.RulesActivated,
                        result.RulesDeactivated,
                        result.ExperimentsStarted,
                        result.ExperimentsCompleted);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Pricing scheduling pass failed; will retry on next tick");
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
