using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Background worker (issue #331) that periodically deactivates expired permission
///     grants and publishes upcoming-expiration notifications. Creates a fresh scope
///     per bounded cycle and retries on the next interval when a cycle fails or
///     reaches its deadline. Follows the <c>RefreshTokenCleanupWorker</c> pattern.
/// </summary>
public sealed class PermissionExpirationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PermissionExpirationOptions> options,
    ILogger<PermissionExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var policy = options.Value;
        if (policy.Validate().Count != 0)
        {
            throw new InvalidOperationException("Invalid permission expiration configuration.");
        }

        if (!policy.Enabled)
        {
            logger.LogInformation("Permission expiration worker is disabled by configuration.");
            return;
        }

        try
        {
            await Task.Delay(policy.InitialDelay, stoppingToken).ConfigureAwait(false);

            while (!stoppingToken.IsCancellationRequested)
            {
                using var timeout = new CancellationTokenSource(policy.ExecutionTimeout);
                using var cycle = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token);
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var expirationService = scope.ServiceProvider.GetRequiredService<IPermissionExpirationService>();

                    var deactivated = await expirationService
                        .ProcessExpiredAsync(cycle.Token)
                        .ConfigureAwait(false);
                    var reminders = await expirationService
                        .SendUpcomingExpirationRemindersAsync(cycle.Token)
                        .ConfigureAwait(false);

                    logger.LogInformation(
                        "Permission expiration cycle committed. GrantsDeactivated: {GrantsDeactivated}, RemindersPublished: {RemindersPublished}",
                        deactivated,
                        reminders);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    logger.LogWarning("Permission expiration cycle reached its deadline; retrying on the next interval.");
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Permission expiration cycle failed; retrying on the next interval.");
                }

                await Task.Delay(policy.ScanInterval, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
    }
}
