using GameGuild.Identity.Authentication;
using Microsoft.Extensions.Options;

namespace GameGuild.API.Core.Security;

/// <summary>Creates a fresh scope per bounded cycle and retries on the next interval.</summary>
internal sealed class RefreshTokenCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RefreshTokenCleanupOptions> options,
    TimeProvider timeProvider,
    ILogger<RefreshTokenCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var policy = options.Value;
        if (policy.Validate().Count != 0) { throw new InvalidOperationException("Invalid refresh-token cleanup configuration."); }
        if (!policy.Enabled) { return; }
        try
        {
            await Task.Delay(policy.InitialDelay, timeProvider, stoppingToken).ConfigureAwait(false);
            while (!stoppingToken.IsCancellationRequested)
            {
                using var timeout = new CancellationTokenSource(policy.ExecutionTimeout, timeProvider);
                using var cycle = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token);
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var operation = scope.ServiceProvider.GetRequiredService<IRefreshTokenCleanupOperation>();
                    var result = await operation.RunAsync(cycle.Token).ConfigureAwait(false);
                    logger.LogInformation("Credential cleanup committed. TokensDeleted: {TokensDeleted}, SessionsDeleted: {SessionsDeleted}",
                        result.TokensDeleted, result.SessionsDeleted);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    RefreshTokenLifecycleMetrics.RecordCleanupFailure(true);
                    logger.LogWarning("Credential cleanup reached its cycle deadline; retrying on the next interval.");
                }
                catch (Exception exception)
                {
                    RefreshTokenLifecycleMetrics.RecordCleanupFailure(false);
                    logger.LogWarning(exception, "Credential cleanup failed; retrying on the next interval.");
                }
                await Task.Delay(policy.Interval, timeProvider, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
    }
}
