using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameGuild.Assets.BackgroundServices;

/// <summary>
/// Background service for garbage collection of orphaned content.
/// </summary>
public class AssetGarbageCollectionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AssetGarbageCollectionService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);
    private readonly TimeSpan _gracePeriod = TimeSpan.FromHours(24);

    public AssetGarbageCollectionService(
        IServiceProvider serviceProvider,
        ILogger<AssetGarbageCollectionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Asset Garbage Collection service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunGarbageCollectionAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during asset garbage collection");
                throw;
            }

            await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunGarbageCollectionAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var contentRepository = scope.ServiceProvider.GetRequiredService<IAssetContentRepository>();
        var storageService = scope.ServiceProvider.GetRequiredService<IAssetStorageService>();
        var transformedRepository = scope.ServiceProvider.GetRequiredService<ITransformedAssetRepository>();

        // Get candidates for deletion
        var candidates = await contentRepository.GetGarbageCollectionCandidatesAsync(
            _gracePeriod,
            100,
            ct).ConfigureAwait(false);

        if (candidates.Count == 0)
        {
            _logger.LogDebug("No content marked for garbage collection");
            return;
        }

        _logger.LogInformation(
            "Processing {Count} content items for garbage collection",
            candidates.Count);

        var deleted = 0;
        var failed = 0;

        foreach (var content in candidates)
        {
            try
            {
                // Delete transformed versions first
                await transformedRepository.DeleteBySourceAsync(content.Id, ct).ConfigureAwait(false);

                // Delete from storage
                await storageService.DeleteAsync(content.BucketName, content.ObjectKey, ct).ConfigureAwait(false);

                content.AddIntegrationEvent(CreateObjectDeletedEvent(content));
                await contentRepository.DeleteAsync(content.Id, ct).ConfigureAwait(false);

                deleted++;

                _logger.LogDebug(
                    "Deleted orphaned content {ContentId} (hash: {Hash})",
                    content.Id, content.ContentHash);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogWarning(
                    ex,
                    "Failed to delete content {ContentId}",
                    content.Id);
                throw;
            }
        }

        _logger.LogInformation(
            "Garbage collection completed: {Deleted} deleted, {Failed} failed",
            deleted, failed);
    }

    private static AssetObjectDeletedEvent CreateObjectDeletedEvent(AssetContent content) =>
        new(content.Id, content.ContentHash, content.SizeBytes, "object-storage")
        {
            TenantId = content.TenantId ?? DurableIntegrationEventTenants.Platform,
            ActorId = DurableIntegrationEventActors.System,
            AggregateType = nameof(AssetContent),
            AggregateId = content.Id.ToString(),
            CorrelationId = Guid.NewGuid()
        };
}
