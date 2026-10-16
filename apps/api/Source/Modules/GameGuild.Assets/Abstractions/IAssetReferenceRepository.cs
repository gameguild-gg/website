namespace GameGuild.Assets;

/// <summary>
/// Repository for AssetReference entities.
/// </summary>
public interface IAssetReferenceRepository
{
    /// <summary>
    /// Gets an asset reference by ID.
    /// </summary>
    Task<AssetReference?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets an asset reference by ID with content loaded.
    /// </summary>
    Task<AssetReference?> GetByIdWithContentAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets asset references by IDs with content loaded. Deleted references are excluded.
    /// </summary>
    Task<IReadOnlyList<AssetReference>> GetByIdsWithContentAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Gets asset references by parent resource.
    /// </summary>
    Task<IReadOnlyList<AssetReference>> GetByParentAsync(
        string parentResourceType,
        Guid parentResourceId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets asset references by user.
    /// </summary>
    Task<IReadOnlyList<AssetReference>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Adds a new asset reference.
    /// </summary>
    Task<AssetReference> AddAsync(AssetReference reference, CancellationToken ct = default);

    /// <summary>
    /// Updates an asset reference.
    /// </summary>
    Task UpdateAsync(AssetReference reference, CancellationToken ct = default);

    /// <summary>
    /// Deletes an asset reference (soft delete).
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Checks if user owns the asset reference.
    /// </summary>
    Task<bool> IsOwnedByUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Records an access to the asset.
    /// </summary>
    Task RecordAccessAsync(Guid id, CancellationToken ct = default);
}
