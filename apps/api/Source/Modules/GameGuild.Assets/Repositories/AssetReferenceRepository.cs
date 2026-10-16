using Microsoft.EntityFrameworkCore;

namespace GameGuild.Assets;

/// <summary>
/// Repository implementation for AssetReference entities.
/// </summary>
public class AssetReferenceRepository : IAssetReferenceRepository
{
    private readonly IApplicationDbContext _context;

    public AssetReferenceRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AssetReference?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Set<AssetReference>()
            .FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<AssetReference?> GetByIdWithContentAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Set<AssetReference>()
            .Include(x => x.Content)
            .FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AssetReference>> GetByIdsWithContentAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.Set<AssetReference>()
            .Include(x => x.Content)
            .Where(x => ids.Contains(x.Id) && x.DeletedAt == null)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AssetReference>> GetByParentAsync(
        string parentResourceType,
        Guid parentResourceId,
        CancellationToken ct = default)
    {
        return await _context.Set<AssetReference>()
            .Include(x => x.Content)
            .Where(x => x.ParentResourceType == parentResourceType && x.ParentResourceId == parentResourceId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AssetReference>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Set<AssetReference>()
            .Include(x => x.Content)
            .Where(x => x.CreatedByUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<AssetReference> AddAsync(AssetReference reference, CancellationToken ct = default)
    {
        await _context.Set<AssetReference>().AddAsync(reference, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        return reference;
    }

    public async Task UpdateAsync(AssetReference reference, CancellationToken ct = default)
    {
        _context.Set<AssetReference>().Update(reference);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var reference = await GetByIdAsync(id, ct).ConfigureAwait(false);
        if (reference != null)
        {
            reference.SoftDelete();
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<bool> IsOwnedByUserAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        return await _context.Set<AssetReference>()
            .AnyAsync(x => x.Id == id && x.CreatedByUserId == userId, ct).ConfigureAwait(false);
    }

    public async Task RecordAccessAsync(Guid id, CancellationToken ct = default)
    {
        await _context.Set<AssetReference>()
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(x => x
                .SetProperty(p => p.AccessCount, p => p.AccessCount + 1)
                .SetProperty(p => p.LastAccessedAt, SystemClock.UtcNow), ct).ConfigureAwait(false);
    }
}
