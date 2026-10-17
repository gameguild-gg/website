using GameGuild.Assets.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Assets.BackgroundServices;

/// <summary>
/// Hosted wrapper that periodically runs asset garbage collection through the
/// options-driven scoped <see cref="IAssetGarbageCollectionService"/> (which owns
/// grace period, reference-count double-check, and legal-hold/IsDeletable safety).
/// </summary>
public sealed class AssetGarbageCollectionService(
    IServiceScopeFactory scopeFactory,
    IOptions<AssetGarbageCollectionOptions> options,
    ILogger<AssetGarbageCollectionService> logger) : BackgroundService
{
    private readonly AssetGarbageCollectionOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Asset garbage collection is disabled");
            return;
        }

        var delay = TimeSpan.FromHours(Math.Max(1, _options.MinIntervalHours));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var garbageCollection = scope.ServiceProvider
                    .GetRequiredService<IAssetGarbageCollectionService>();
                var result = await garbageCollection.RunGarbageCollectionAsync(stoppingToken)
                    .ConfigureAwait(false);
                logger.LogInformation(
                    "Asset garbage collection cycle: {Processed} processed, {Deleted} deleted, {Skipped} skipped",
                    result.ItemsProcessed, result.ItemsDeleted, result.ItemsSkipped);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Asset garbage collection cycle failed");
            }

            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }
    }
}
