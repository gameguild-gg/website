namespace GameGuild.Assets.UnitTests.Services;

/// <summary>
/// In-memory <see cref="IAssetContentRepository"/> fake mirroring the increment/decrement
/// semantics of the EF Core <c>AssetContentRepository</c> (decrement auto-marks
/// <c>MarkedForDeletionAt</c> at zero, increment clears it).
/// </summary>
internal sealed class InMemoryAssetContentRepository : IAssetContentRepository
{
    private readonly Dictionary<Guid, AssetContent> _contents = new();

    public int RowCount => _contents.Count;

    public AssetContent Seed(AssetContent content)
    {
        if (content.Id == Guid.Empty)
        {
            content.Id = Guid.NewGuid();
        }

        _contents[content.Id] = content;
        return content;
    }

    public Task<AssetContent?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_contents.TryGetValue(id, out var content) ? content : null);

    public Task<AssetContent?> GetByContentHashAsync(string contentHash, CancellationToken ct = default) =>
        Task.FromResult(_contents.Values.FirstOrDefault(content => content.ContentHash == contentHash));

    public Task<AssetContent> AddAsync(AssetContent content, CancellationToken ct = default)
    {
        if (content.Id == Guid.Empty)
        {
            content.Id = Guid.NewGuid();
        }

        _contents[content.Id] = content;
        return Task.FromResult(content);
    }

    public Task UpdateAsync(AssetContent content, CancellationToken ct = default)
    {
        _contents[content.Id] = content;
        return Task.CompletedTask;
    }

    public Task IncrementReferenceCountAsync(Guid id, CancellationToken ct = default)
    {
        var content = _contents[id];
        content.ReferenceCount++;
        content.MarkedForDeletionAt = null;
        return Task.CompletedTask;
    }

    public Task DecrementReferenceCountAsync(Guid id, CancellationToken ct = default)
    {
        var content = _contents[id];
        content.ReferenceCount--;
        if (content.ReferenceCount <= 0 && content.MarkedForDeletionAt == null)
        {
            content.MarkedForDeletionAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _contents.Remove(id);
        return Task.CompletedTask;
    }

    public Task<int> GetCurrentReferenceCountAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_contents.TryGetValue(id, out var content) ? content.ReferenceCount : 0);

    public Task<IReadOnlyList<AssetContent>> GetPendingVirusScanAsync(int limit = 100, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<bool> TryBeginVirusScanAsync(Guid id, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<AssetContent>> GetPendingModerationAsync(int limit = 100, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<AssetContent>> GetGarbageCollectionCandidatesAsync(
        TimeSpan gracePeriod, int limit = 100, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<List<AssetContent>> GetMarkedForDeletionAsync(
        DateTime cutoffDate, int limit = 100, CancellationToken ct = default) =>
        throw new NotSupportedException();
}
