using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     EF Core repository for <see cref="ContentTypePermissionGrant"/> rows (the
///     content-type layer's scoped grant store, issues #360/#365). Soft-deleted rows are
///     excluded by the entity's query filter; all lookups are exact-match and
///     tenant-isolated.
/// </summary>
public class ContentTypePermissionGrantRepository(IApplicationDbContext context) : IContentTypePermissionGrantRepository
{
    private DbSet<ContentTypePermissionGrant> Grants => context.Set<ContentTypePermissionGrant>();

    public async Task<ContentTypePermissionGrant> CreateAsync(
        ContentTypePermissionGrant grant,
        CancellationToken cancellationToken = default)
    {
        grant.Touch();
        Grants.Add(grant);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return grant;
    }

    public async Task<ContentTypePermissionGrant> UpdateAsync(
        ContentTypePermissionGrant grant,
        CancellationToken cancellationToken = default)
    {
        grant.Touch();
        Grants.Update(grant);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return grant;
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var grant = await Grants
            .FirstOrDefaultAsync(g => g.Id == id && g.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        if (grant is null)
        {
            return false;
        }

        // Soft delete preserves the permission history for compliance (#360); revoking an
        // active grant also deactivates it so the row conveys nothing while deleted.
        grant.IsActive = false;
        grant.SoftDelete();
        Grants.Update(grant);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<ContentTypePermissionGrant?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => await Grants
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id && g.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);

    public async Task<ContentTypePermissionGrant?> GetBySubjectAndContentTypeAsync(
        Guid? userId,
        Guid? tenantId,
        string contentTypeName,
        CancellationToken cancellationToken = default)
        => await Grants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                g => g.UserId == userId
                     && g.TenantId == tenantId
                     && g.ContentTypeName == contentTypeName
                     && g.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<List<ContentTypePermissionGrant>> GetByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
        => await Grants
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.DeletedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<List<ContentTypePermissionGrant>> GetByContentTypeAsync(
        Guid tenantId,
        string contentTypeName,
        CancellationToken cancellationToken = default)
        => await Grants
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.ContentTypeName == contentTypeName && g.DeletedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<List<ContentTypePermissionGrant>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => await Grants
            .AsNoTracking()
            .Where(g => g.UserId == userId && g.DeletedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
