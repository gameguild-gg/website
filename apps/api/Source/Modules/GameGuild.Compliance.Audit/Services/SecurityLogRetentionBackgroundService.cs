using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Periodically applies every tenant's security log retention policy. Passes run per tenant,
///     record an execution row each, and never delete rows for tenants under an active legal hold.
/// </summary>
public sealed class SecurityLogRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<SecurityEventPipelineOptions> options,
    ILogger<SecurityLogRetentionBackgroundService> logger,
    TimeSpan? initialDelay = null) : BackgroundService
{
    private readonly TimeSpan interval = options.Value.RetentionEnforcementInterval is { } value && value > TimeSpan.Zero
        ? value
        : TimeSpan.FromHours(24);

    private readonly TimeSpan initialDelay = initialDelay ?? TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Security log retention enforcement started with interval {Interval}", interval);

        // Run the first pass shortly after startup, then on the configured cadence.
        try
        {
            await Task.Delay(initialDelay, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ISecurityLogRetentionRepository>();
                var retention = scope.ServiceProvider.GetRequiredService<ISecurityLogRetentionService>();
                var policies = await repository.GetAllPoliciesAsync(stoppingToken).ConfigureAwait(false);
                foreach (var policy in policies)
                {
                    // System-triggered passes have no acting user; enforcement itself is recorded per tenant.
                    await retention.EnforceForTenantAsync(policy.TenantId, triggeredByUserId: null, dryRun: false, stoppingToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled security log retention enforcement failed; will retry on the next cycle");
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
