using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Assets.BackgroundServices;

public sealed class VirusScanBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<VirusScanOptions> options,
    ILogger<VirusScanBackgroundService> logger) : BackgroundService
{
    private readonly VirusScanOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Asset virus scanning is disabled");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IAssetVirusScanProcessor>();
                await processor.ProcessPendingAsync(_options.BatchSize, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Asset virus scan cycle failed");
            }

            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }
    }
}
