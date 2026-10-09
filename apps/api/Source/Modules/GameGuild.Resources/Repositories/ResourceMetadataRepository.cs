using Microsoft.EntityFrameworkCore;

namespace GameGuild.Resources;

/// <summary>
///     Repository implementation for ResourceMetadata entity
/// </summary>
public class ResourceMetadataRepository(IApplicationDbContext context) : IResourceMetadataRepository
{
    private DbSet<ResourceMetadata> ResourceMetadataSet => context.Set<ResourceMetadata>();

    public async Task<ResourceMetadata?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await ResourceMetadataSet.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<ResourceMetadata?> GetByKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken = default)
    {
        return await ResourceMetadataSet.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Key == key, cancellationToken);
    }

    public async Task<IEnumerable<ResourceMetadata>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ResourceMetadataSet.Where(m => m.TenantId == tenantId).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Key).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ResourceMetadata>> GetByCategoryAsync(Guid tenantId, string category, CancellationToken cancellationToken = default)
    {
        return await ResourceMetadataSet.Where(m => m.TenantId == tenantId && m.Category == category).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Key).ToListAsync(cancellationToken);
    }

    public async Task<ResourceMetadata> CreateAsync(ResourceMetadata metadata, CancellationToken cancellationToken = default)
    {
        ResourceMetadataSet.Add(metadata);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return metadata;
    }

    public async Task<ResourceMetadata> UpdateAsync(ResourceMetadata metadata, CancellationToken cancellationToken = default)
    {
        ResourceMetadataSet.Update(metadata);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return metadata;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var metadata = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (metadata == null)
        {
            return false;
        }

        ResourceMetadataSet.Remove(metadata);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> DeleteByKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken = default)
    {
        var metadata = await GetByKeyAsync(tenantId, key, cancellationToken).ConfigureAwait(false);

        if (metadata == null)
        {
            return false;
        }

        ResourceMetadataSet.Remove(metadata);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<ResourceMetadata?> GetByUserKeyAsync(Guid userId, string key, CancellationToken cancellationToken = default)
    {
        return await ResourceMetadataSet.FirstOrDefaultAsync(m => m.UserId == userId && m.Key == key, cancellationToken);
    }

    public async Task<IEnumerable<ResourceMetadata>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await ResourceMetadataSet.Where(m => m.UserId == userId).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Key).ToListAsync(cancellationToken);
    }
}
