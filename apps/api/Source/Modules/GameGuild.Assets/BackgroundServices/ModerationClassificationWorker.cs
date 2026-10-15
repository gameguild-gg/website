using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Assets.BackgroundServices;

public sealed class ModerationClassificationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AutoModerationOptions> options,
    ILogger<ModerationClassificationWorker> logger) : BackgroundService
{
    private readonly AutoModerationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Automated asset moderation is disabled");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(1, _options.ModerationPollingIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IAssetModerationProcessor>();
                await processor.ProcessPendingAsync(batchSize: 10, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Asset moderation cycle failed");
            }

            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }
    }
}
