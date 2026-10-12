using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

public sealed class ScheduledAuditExportBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledAuditExportBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scheduled audit export worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var scheduledExports = scope.ServiceProvider.GetRequiredService<IScheduledAuditExportService>();
                var processed = await scheduledExports.ProcessDueAsync(stoppingToken).ConfigureAwait(false);
                var expired = await scheduledExports.ExpireExpiredFilesAsync(stoppingToken).ConfigureAwait(false);
                if (processed > 0)
                {
                    logger.LogInformation("Scheduled audit export worker processed {ExportCount} export(s)", processed);
                }

                if (expired > 0)
                {
                    logger.LogInformation("Scheduled audit export worker removed {ExpiredCount} expired file(s)", expired);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled audit export polling failed; the worker will retry");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
