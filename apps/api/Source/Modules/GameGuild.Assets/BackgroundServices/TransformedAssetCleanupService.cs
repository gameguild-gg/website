using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Assets.BackgroundServices;

/// <summary>
/// Configuration for the transformed-asset cache cleanup loop.
/// Mirrors the garbage-collection options pattern.
/// </summary>
public class TransformedAssetCleanupOptions
{
    public const string SectionName = "Assets:TransformedAssetCleanup";

    /// <summary>
    /// Whether transformed-asset cleanup is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Hours between cleanup runs.
    /// </summary>
    public int IntervalHours { get; set; } = 6;

    /// <summary>
    /// Maximum age of an unaccessed transformed asset before eviction.
    /// </summary>
    public int MaxAgeDays { get; set; } = 7;

    /// <summary>
    /// Batch size per cleanup run.
    /// </summary>
    public int BatchSize { get; set; } = 100;
}

/// <summary>
/// Background service for cleaning up stale transformed asset cache.
/// Resolves scoped dependencies per cycle; all knobs come from options.
/// </summary>
public sealed class TransformedAssetCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<TransformedAssetCleanupOptions> options,
    ILogger<TransformedAssetCleanupService> logger) : BackgroundService
{
    private readonly TransformedAssetCleanupOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Transformed asset cleanup is disabled");
            return;
        }

        var delay = TimeSpan.FromHours(Math.Max(1, _options.IntervalHours));
        var maxAge = TimeSpan.FromDays(Math.Max(1, _options.MaxAgeDays));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = await RunCleanupAsync(maxAge, stoppingToken).ConfigureAwait(false);
                logger.LogInformation(
                    "Transformed asset cleanup cycle deleted {Deleted} stale entries", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Transformed asset cleanup cycle failed");
            }

            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task<int> RunCleanupAsync(TimeSpan maxAge, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var transformedRepository = scope.ServiceProvider.GetRequiredService<ITransformedAssetRepository>();
        var storageService = scope.ServiceProvider.GetRequiredService<IAssetStorageService>();
        var storageOptions = scope.ServiceProvider
            .GetRequiredService<IOptions<AssetStorageOptions>>().Value;

        // Get stale transformed assets
        var staleAssets = await transformedRepository
            .GetStaleAssetsAsync(maxAge, _options.BatchSize, ct).ConfigureAwait(false);

        if (staleAssets.Count == 0)
        {
            logger.LogDebug("No stale transformed assets to clean up");
            return 0;
        }

        logger.LogInformation(
            "Cleaning up {Count} stale transformed assets",
            staleAssets.Count);

        var deleted = 0;
        var failed = 0;

        foreach (var asset in staleAssets)
        {
            try
            {
                // Delete from storage
                await storageService.DeleteAsync(
                    storageOptions.GetTransformedBucketName(),
                    asset.ObjectKey,
                    ct).ConfigureAwait(false);

                // Delete record
                await transformedRepository.DeleteAsync(asset.Id, ct).ConfigureAwait(false);

                deleted++;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogWarning(
                    ex,
                    "Failed to delete transformed asset {AssetId}",
                    asset.Id);
            }
        }

        logger.LogInformation(
            "Transformed asset cleanup completed: {Deleted} deleted, {Failed} failed",
            deleted, failed);
        return deleted;
    }
}
