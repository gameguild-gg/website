using Microsoft.EntityFrameworkCore;

namespace GameGuild.Resources;

/// <summary>
///     Repository implementation for ResourceSettings entity
/// </summary>
public class ResourceSettingsRepository(IApplicationDbContext context) : IResourceSettingsRepository
{
    private DbSet<ResourceSettings> ResourceSettingsSet => context.Set<ResourceSettings>();

    public async Task<ResourceSettings?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await ResourceSettingsSet.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<ResourceSettings?> GetByKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken = default)
    {
        return await ResourceSettingsSet.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == key && s.UserId == null, cancellationToken);
    }

    public async Task<IEnumerable<ResourceSettings>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ResourceSettingsSet.Where(s => s.TenantId == tenantId && s.UserId == null).OrderBy(s => s.DisplayOrder).ThenBy(s => s.Key).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ResourceSettings>> GetByCategoryAsync(Guid tenantId, string category, CancellationToken cancellationToken = default)
    {
        return await ResourceSettingsSet.Where(s => s.TenantId == tenantId && s.Category == category && s.UserId == null).OrderBy(s => s.DisplayOrder).ThenBy(s => s.Key).ToListAsync(cancellationToken);
    }

    public async Task<ResourceSettings> CreateAsync(ResourceSettings settings, CancellationToken cancellationToken = default)
    {
        ResourceSettingsSet.Add(settings);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return settings;
    }

    public async Task<ResourceSettings> UpdateAsync(ResourceSettings settings, CancellationToken cancellationToken = default)
    {
        ResourceSettingsSet.Update(settings);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return settings;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var settings = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (settings == null)
        {
            return false;
        }

        ResourceSettingsSet.Remove(settings);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> DeleteByKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken = default)
    {
        var settings = await GetByKeyAsync(tenantId, key, cancellationToken).ConfigureAwait(false);

        if (settings == null)
        {
            return false;
        }

        ResourceSettingsSet.Remove(settings);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<ResourceSettings?> GetByUserKeyAsync(Guid userId, string key, CancellationToken cancellationToken = default)
    {
        return await ResourceSettingsSet.FirstOrDefaultAsync(s => s.UserId == userId && s.Key == key, cancellationToken);
    }

    public async Task<IEnumerable<ResourceSettings>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await ResourceSettingsSet.Where(s => s.UserId == userId).OrderBy(s => s.DisplayOrder).ThenBy(s => s.Key).ToListAsync(cancellationToken);
    }

    public async Task<string?> GetEffectiveValueAsync(Guid tenantId, string key, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        // First, try to get user-level override if userId is provided
        if (userId.HasValue)
        {
            var userSetting = await GetByUserKeyAsync(userId.Value, key, cancellationToken).ConfigureAwait(false);

            if (userSetting != null)
            {
                return userSetting.GetEffectiveValue();
            }
        }

        // Fall back to tenant-level setting
        var tenantSetting = await GetByKeyAsync(tenantId, key, cancellationToken).ConfigureAwait(false);

        return tenantSetting?.GetEffectiveValue();
    }
}
