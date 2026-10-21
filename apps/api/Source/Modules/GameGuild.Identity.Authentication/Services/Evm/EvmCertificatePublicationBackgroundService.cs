using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>Explicitly selected bounded publisher loop. Scoped workers own no long-lived database context.</summary>
public sealed class EvmCertificatePublicationBackgroundService(IServiceScopeFactory scopes,
    EvmCertificatePublicationPolicy policy, TimeProvider clock, ILogger<EvmCertificatePublicationBackgroundService> logger) : BackgroundService
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Missing persistence/signing/transport composition must fail startup before a retry loop can hide it.
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<EvmCertificatePublicationWorker>();
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(policy.CheckDelay, clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var worker = scope.ServiceProvider.GetRequiredService<EvmCertificatePublicationWorker>();
                await worker.ProcessDueAsync(20, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception)
            {
                // Provider/database exceptions may embed credentials. Leases remain fenced and recoverable.
                logger.LogError("EVM publication batch failed; durable operations remain available for recovery.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
