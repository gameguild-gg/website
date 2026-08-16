using Microsoft.EntityFrameworkCore;

namespace GameGuild.Assets;

/// <summary>
/// Repository implementation for AssetContent entities.
/// </summary>
public class AssetContentRepository : IAssetContentRepository
{
    private readonly IApplicationDbContext _context;

    public AssetContentRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AssetContent?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Set<AssetContent>()
            .FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<AssetContent?> GetByContentHashAsync(string contentHash, CancellationToken ct = default)
    {
        return await _context.Set<AssetContent>()
            .FirstOrDefaultAsync(x => x.ContentHash == contentHash, ct).ConfigureAwait(false);
    }

    public async Task<AssetContent> AddAsync(AssetContent content, CancellationToken ct = default)
    {
        await _context.Set<AssetContent>().AddAsync(content, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        return content;
    }

    public async Task UpdateAsync(AssetContent content, CancellationToken ct = default)
    {
        _context.Set<AssetContent>().Update(content);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AssetContent>> GetPendingVirusScanAsync(int limit = 100, CancellationToken ct = default)
    {
        return await _context.Set<AssetContent>()
            .Where(x => x.VirusScanStatus == VirusScanStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> TryBeginVirusScanAsync(Guid id, CancellationToken ct = default)
    {
        var updated = await _context.Set<AssetContent>()
            .Where(content => content.Id == id && content.VirusScanStatus == VirusScanStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(content => content.VirusScanStatus, VirusScanStatus.Scanning)
                    .SetProperty(content => content.VirusScanCompletedAt, (DateTime?)null),
                ct)
            .ConfigureAwait(false);

        return updated == 1;
    }

    public async Task<IReadOnlyList<AssetContent>> GetPendingModerationAsync(int limit = 100, CancellationToken ct = default)
    {
        return await _context.Set<AssetContent>()
            .Where(x => x.VirusScanStatus == VirusScanStatus.Clean)
            .Where(x => x.ModerationStatus == ModerationStatus.Pending || 
                        x.ModerationStatus == ModerationStatus.NeedsReview)
            .OrderBy(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AssetContent>> GetGarbageCollectionCandidatesAsync(
        TimeSpan gracePeriod,
        int limit = 100,
        CancellationToken ct = default)
    {
        var cutoff = SystemClock.UtcNow - gracePeriod;
        
        return await _context.Set<AssetContent>()
            .Where(x => x.ReferenceCount == 0)
            .Where(x => x.MarkedForDeletionAt != null && x.MarkedForDeletionAt < cutoff)
            .Where(x => x.IsDeletable)
            .OrderBy(x => x.MarkedForDeletionAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task IncrementReferenceCountAsync(Guid id, CancellationToken ct = default)
    {
        await _context.Set<AssetContent>()
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(x => x
                .SetProperty(p => p.ReferenceCount, p => p.ReferenceCount + 1)
                .SetProperty(p => p.MarkedForDeletionAt, (DateTime?)null), ct).ConfigureAwait(false);
    }

    public async Task DecrementReferenceCountAsync(Guid id, CancellationToken ct = default)
    {
        // First decrement
        await _context.Set<AssetContent>()
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(x => x
                .SetProperty(p => p.ReferenceCount, p => p.ReferenceCount - 1), ct).ConfigureAwait(false);

        // Then mark for deletion if count reached 0
        await _context.Set<AssetContent>()
            .Where(x => x.Id == id && x.ReferenceCount <= 0 && x.MarkedForDeletionAt == null)
            .ExecuteUpdateAsync(x => x
                .SetProperty(p => p.MarkedForDeletionAt, SystemClock.UtcNow), ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _context.Set<AssetContent>()
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<AssetContent>> GetMarkedForDeletionAsync(
        DateTime cutoffDate,
        int limit,
        CancellationToken ct = default)
    {
        return await _context.Set<AssetContent>()
            .Where(x => x.MarkedForDeletionAt != null && x.MarkedForDeletionAt < cutoffDate)
            .Where(x => x.ReferenceCount == 0)
            .Where(x => x.IsDeletable)
            .OrderBy(x => x.MarkedForDeletionAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> GetCurrentReferenceCountAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Set<AssetContent>()
            .Where(x => x.Id == id)
            .Select(x => x.ReferenceCount)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }
}
