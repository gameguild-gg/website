using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Assets.Security;

/// <summary>
/// Configuration for asset garbage collection.
/// Mitigates: Reference Count Race (#10), GC Deletes Active Asset (#11)
/// </summary>
public class AssetGarbageCollectionOptions
{
    public const string SectionName = "Assets:GarbageCollection";

    /// <summary>
    /// Grace period in days before unreferenced content is deleted.
    /// Set high enough to prevent accidental deletion of active content.
    /// </summary>
    public int GracePeriodDays { get; set; } = 30;

    /// <summary>
    /// Batch size for GC operations.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Whether GC is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Minimum time between GC runs in hours.
    /// </summary>
    public int MinIntervalHours { get; set; } = 6;

    /// <summary>
    /// Maximum number of items to process per run.
    /// </summary>
    public int MaxItemsPerRun { get; set; } = 1000;
}

/// <summary>
/// Result of a garbage collection run.
/// </summary>
public sealed record GarbageCollectionResult(
    int ItemsProcessed,
    int ItemsDeleted,
    int ItemsSkipped,
    int Errors,
    TimeSpan Duration,
    List<string> Messages);

/// <summary>
/// Service for managing asset garbage collection.
/// </summary>
public interface IAssetGarbageCollectionService
{
    /// <summary>
    /// Runs garbage collection to clean up unreferenced content.
    /// </summary>
    Task<GarbageCollectionResult> RunGarbageCollectionAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Marks content as eligible for deletion (starts grace period).
    /// </summary>
    Task MarkForDeletionAsync(Guid contentId, CancellationToken ct = default);

    /// <summary>
    /// Clears deletion mark (content is referenced again).
    /// </summary>
    Task ClearDeletionMarkAsync(Guid contentId, CancellationToken ct = default);

    /// <summary>
    /// Gets content that will be deleted soon (warning report).
    /// </summary>
    Task<List<AssetContent>> GetPendingDeletionAsync(
        int daysUntilDeletion,
        CancellationToken ct = default);
}

/// <summary>
/// Implementation of asset garbage collection with safety checks.
/// </summary>
public class AssetGarbageCollectionService : IAssetGarbageCollectionService
{
    private readonly IAssetContentRepository _contentRepository;
    private readonly IAssetStorageService _storageService;
    private readonly AssetGarbageCollectionOptions _options;
    private readonly ILogger<AssetGarbageCollectionService> _logger;

    public AssetGarbageCollectionService(
        IAssetContentRepository contentRepository,
        IAssetStorageService storageService,
        IOptions<AssetGarbageCollectionOptions> options,
        ILogger<AssetGarbageCollectionService> logger)
    {
        _contentRepository = contentRepository;
        _storageService = storageService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GarbageCollectionResult> RunGarbageCollectionAsync(
        CancellationToken ct = default)
    {
        var startTime = SystemClock.UtcNow;
        var messages = new List<string>();
        var processed = 0;
        var deleted = 0;
        var skipped = 0;
        var errors = 0;

        if (!_options.Enabled)
        {
            messages.Add("Garbage collection is disabled");
            return new GarbageCollectionResult(0, 0, 0, 0, TimeSpan.Zero, messages);
        }

        _logger.LogInformation("Starting asset garbage collection");

        try
        {
            // Get content marked for deletion past grace period
            var cutoffDate = SystemClock.UtcNow.AddDays(-_options.GracePeriodDays);
            var candidates = await _contentRepository.GetMarkedForDeletionAsync(
                cutoffDate,
                _options.MaxItemsPerRun,
                ct).ConfigureAwait(false);

            messages.Add($"Found {candidates.Count} candidates for deletion");

            foreach (var content in candidates)
            {
                ct.ThrowIfCancellationRequested();
                processed++;

                try
                {
                    // CRITICAL: Double-check reference count before deletion
                    // This prevents race conditions where new references were added
                    var currentRefCount = await _contentRepository.GetCurrentReferenceCountAsync(
                        content.Id, ct).ConfigureAwait(false);

                    if (currentRefCount > 0)
                    {
                        _logger.LogWarning(
                            "Content {ContentId} has {RefCount} references, skipping deletion",
                            content.Id, currentRefCount);

                        // Clear deletion mark since it's now referenced
                        await ClearDeletionMarkAsync(content.Id, ct).ConfigureAwait(false);
                        skipped++;
                        messages.Add($"Skipped {content.Id}: now has {currentRefCount} references");
                        continue;
                    }

                    // Check if marked for deletion long enough
                    if (content.MarkedForDeletionAt > cutoffDate)
                    {
                        skipped++;
                        continue;
                    }

                    // Check if content is deletable
                    if (!content.IsDeletable)
                    {
                        skipped++;
                        messages.Add($"Skipped {content.Id}: marked as non-deletable");
                        continue;
                    }

                    // Delete from storage
                    await _storageService.DeleteAsync(
                        content.BucketName,
                        content.ObjectKey,
                        ct).ConfigureAwait(false);

                    // Delete transformed versions
                    // (handled by cascade or separate cleanup)

                    content.AddIntegrationEvent(new AssetObjectDeletedEvent(
                        content.Id,
                        content.ContentHash,
                        content.SizeBytes,
                        "object-storage")
                    {
                        TenantId = content.TenantId ?? DurableIntegrationEventTenants.Platform,
                        ActorId = DurableIntegrationEventActors.System,
                        AggregateType = nameof(AssetContent),
                        AggregateId = content.Id.ToString(),
                        CorrelationId = Guid.NewGuid()
                    });

                    await _contentRepository.DeleteAsync(content.Id, ct).ConfigureAwait(false);

                    deleted++;
                    _logger.LogInformation(
                        "Deleted content {ContentId} ({Hash}) - {Size} bytes freed",
                        content.Id, content.ContentHash[..8], content.SizeBytes);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Another process modified the content - skip
                    skipped++;
                    messages.Add($"Skipped {content.Id}: concurrent modification");
                }
                catch (Exception ex)
                {
                    errors++;
                    _logger.LogError(ex, "Error deleting content {ContentId}", content.Id);
                    messages.Add($"Error deleting {content.Id}: {ex.Message}");
                    throw;
                }
            }
        }
        catch (OperationCanceledException)
        {
            messages.Add("Garbage collection cancelled");
            throw;
        }
        catch (Exception ex)
        {
            errors++;
            _logger.LogError(ex, "Garbage collection failed");
            messages.Add($"GC error: {ex.Message}");
            throw;
        }

        var duration = SystemClock.UtcNow - startTime;
        _logger.LogInformation(
            "Garbage collection completed: {Processed} processed, {Deleted} deleted, " +
            "{Skipped} skipped, {Errors} errors in {Duration}s",
            processed, deleted, skipped, errors, duration.TotalSeconds);

        return new GarbageCollectionResult(
            processed, deleted, skipped, errors, duration, messages);
    }

    public async Task MarkForDeletionAsync(Guid contentId, CancellationToken ct = default)
    {
        var content = await _contentRepository.GetByIdAsync(contentId, ct).ConfigureAwait(false);
        if (content == null)
            return;

        // Only mark if not already marked and reference count is 0
        if (content.MarkedForDeletionAt == null && content.ReferenceCount <= 0)
        {
            content.MarkedForDeletionAt = SystemClock.UtcNow;
            await _contentRepository.UpdateAsync(content, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Content {ContentId} marked for deletion (grace period: {Days} days)",
                contentId, _options.GracePeriodDays);
        }
    }

    public async Task ClearDeletionMarkAsync(Guid contentId, CancellationToken ct = default)
    {
        var content = await _contentRepository.GetByIdAsync(contentId, ct).ConfigureAwait(false);
        if (content == null)
            return;

        if (content.MarkedForDeletionAt != null)
        {
            content.MarkedForDeletionAt = null;
            await _contentRepository.UpdateAsync(content, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Content {ContentId} deletion mark cleared (now referenced)",
                contentId);
        }
    }

    public async Task<List<AssetContent>> GetPendingDeletionAsync(
        int daysUntilDeletion,
        CancellationToken ct = default)
    {
        var cutoffDate = SystemClock.UtcNow.AddDays(_options.GracePeriodDays - daysUntilDeletion);
        return await _contentRepository.GetMarkedForDeletionAsync(
            cutoffDate,
            1000,
            ct).ConfigureAwait(false);
    }
}
