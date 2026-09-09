namespace GameGuild.Assets;

/// <summary>
/// Repository for AssetContent entities.
/// </summary>
public interface IAssetContentRepository
{
    /// <summary>
    /// Gets an asset content by ID.
    /// </summary>
    Task<AssetContent?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets an asset content by content hash.
    /// </summary>
    Task<AssetContent?> GetByContentHashAsync(string contentHash, CancellationToken ct = default);

    /// <summary>
    /// Adds a new asset content.
    /// </summary>
    Task<AssetContent> AddAsync(AssetContent content, CancellationToken ct = default);

    /// <summary>
    /// Updates an asset content.
    /// </summary>
    Task UpdateAsync(AssetContent content, CancellationToken ct = default);

    /// <summary>
    /// Gets assets pending virus scan.
    /// </summary>
    Task<IReadOnlyList<AssetContent>> GetPendingVirusScanAsync(int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Atomically claims pending content for virus scanning.
    /// </summary>
    Task<bool> TryBeginVirusScanAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets assets pending moderation.
    /// </summary>
    Task<IReadOnlyList<AssetContent>> GetPendingModerationAsync(int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Gets assets eligible for garbage collection.
    /// </summary>
    Task<IReadOnlyList<AssetContent>> GetGarbageCollectionCandidatesAsync(
        TimeSpan gracePeriod,
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Increments reference count.
    /// </summary>
    Task IncrementReferenceCountAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Decrements reference count.
    /// </summary>
    Task DecrementReferenceCountAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Deletes an asset content (hard delete).
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets content marked for deletion before the specified date.
    /// Used for garbage collection.
    /// </summary>
    Task<List<AssetContent>> GetMarkedForDeletionAsync(
        DateTime cutoffDate,
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current reference count (with row version check).
    /// Used for safe GC operations.
    /// </summary>
    Task<int> GetCurrentReferenceCountAsync(Guid id, CancellationToken ct = default);
}
