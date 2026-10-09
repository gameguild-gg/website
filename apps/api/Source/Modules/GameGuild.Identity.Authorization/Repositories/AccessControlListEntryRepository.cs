using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Entity Framework implementation of the Access Control List entry repository.
/// </summary>
public class AccessControlListEntryRepository(IApplicationDbContext context) : IAccessControlListEntryRepository
{
    public async Task<AccessControlListEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<AccessControlListEntry>()
            .FirstOrDefaultAsync(e => e.Id == id && e.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AccessControlListEntry?> GetByUserAndResourceAsync(
        Guid tenantId,
        Guid userId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        // Use new principal-based query for backward compatibility
        return await context.Set<AccessControlListEntry>()
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId
                     && e.PrincipalType == AclPrincipalType.User
                     && e.PrincipalId == userId
                     && e.ResourceType == resourceType
                     && e.ResourceId == resourceId
                     && e.DeletedAt == null
                     && e.IsActive
                     && (e.ExpiresAt == null || e.ExpiresAt > SystemClock.UtcNow),
                cancellationToken)
            ;
    }

    public async Task<AccessControlListEntry?> GetByPrincipalAndResourceAsync(
        Guid tenantId,
        AclPrincipalType principalType,
        Guid? principalId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<AccessControlListEntry>()
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId
                     && e.PrincipalType == principalType
                     && e.PrincipalId == principalId
                     && e.ResourceType == resourceType
                     && e.ResourceId == resourceId
                     && e.DeletedAt == null
                     && e.IsActive
                     && (e.ExpiresAt == null || e.ExpiresAt > SystemClock.UtcNow),
                cancellationToken)
            ;
    }

    public async Task<IReadOnlyList<AccessControlListEntry>> GetByResourceAndPrincipalsAsync(
        Guid tenantId,
        string resourceType,
        string resourceId,
        IEnumerable<(AclPrincipalType Type, Guid? Id)> principals,
        CancellationToken cancellationToken = default)
    {
        var principalList = principals.ToList();
        if (principalList.Count == 0)
        {
            return Array.Empty<AccessControlListEntry>();
        }

        // Build query for all matching principals
        var query = context.Set<AccessControlListEntry>()
            .Where(e => e.TenantId == tenantId
                        && e.ResourceType == resourceType
                        && e.ResourceId == resourceId
                        && e.DeletedAt == null
                        && e.IsActive
                        && (e.ExpiresAt == null || e.ExpiresAt > SystemClock.UtcNow));

        // Filter by principals - handle anonymous (null principalId) separately
        var anonymousPrincipals = principalList.Where(p => p.Id == null).ToList();
        var namedPrincipals = principalList.Where(p => p.Id != null).ToList();

        if (anonymousPrincipals.Count > 0 && namedPrincipals.Count > 0)
        {
            // Both anonymous and named principals
            var namedPrincipalIds = namedPrincipals.Select(p => p.Id!.Value).ToList();
            var namedPrincipalTypes = namedPrincipals.Select(p => p.Type).Distinct().ToList();
            
            query = query.Where(e => 
                (e.PrincipalType == AclPrincipalType.Anonymous && e.PrincipalId == null) ||
                (namedPrincipalTypes.Contains(e.PrincipalType) && e.PrincipalId != null && namedPrincipalIds.Contains(e.PrincipalId.Value)));
        }
        else if (anonymousPrincipals.Count > 0)
        {
            // Only anonymous
            query = query.Where(e => e.PrincipalType == AclPrincipalType.Anonymous && e.PrincipalId == null);
        }
        else
        {
            // Only named principals
            var namedPrincipalIds = namedPrincipals.Select(p => p.Id!.Value).ToList();
            var namedPrincipalTypes = namedPrincipals.Select(p => p.Type).Distinct().ToList();
            query = query.Where(e => namedPrincipalTypes.Contains(e.PrincipalType) && e.PrincipalId != null && namedPrincipalIds.Contains(e.PrincipalId.Value));
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AccessControlListEntry>> GetByUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<AccessControlListEntry>()
            .Where(e => e.TenantId == tenantId
                        && e.PrincipalType == AclPrincipalType.User
                        && e.PrincipalId == userId
                        && e.DeletedAt == null
                        && e.IsActive
                        && (e.ExpiresAt == null || e.ExpiresAt > SystemClock.UtcNow))
            .ToListAsync(cancellationToken)
            ;
    }

    public async Task<IReadOnlyList<AccessControlListEntry>> GetByResourceAsync(
        Guid tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<AccessControlListEntry>()
            .Where(e => e.TenantId == tenantId
                        && e.ResourceType == resourceType
                        && e.ResourceId == resourceId
                        && e.DeletedAt == null
                        && e.IsActive
                        && (e.ExpiresAt == null || e.ExpiresAt > SystemClock.UtcNow))
            .ToListAsync(cancellationToken)
            ;
    }

    public async Task<IReadOnlyList<AccessControlListEntry>> GetByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<AccessControlListEntry>()
            .Where(e => e.TenantId == tenantId && e.DeletedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AccessControlListEntry>> GetExpiredEntriesAsync(
        DateTime cutoffDate,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<AccessControlListEntry>()
            .Where(e => e.ExpiresAt != null && e.ExpiresAt <= cutoffDate && e.DeletedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(AccessControlListEntry entry, CancellationToken cancellationToken = default)
    {
        await context.Set<AccessControlListEntry>().AddAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(AccessControlListEntry entry, CancellationToken cancellationToken = default)
    {
        entry.Touch();
        context.Set<AccessControlListEntry>().Update(entry);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(AccessControlListEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entry.SoftDelete();
        context.Set<AccessControlListEntry>().Update(entry);
        return Task.CompletedTask;
    }

    public async Task DeleteByResourceAsync(
        Guid tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        var entries = await context.Set<AccessControlListEntry>()
            .Where(e => e.TenantId == tenantId
                        && e.ResourceType == resourceType
                        && e.ResourceId == resourceId
                        && e.DeletedAt == null)
            .ToListAsync(cancellationToken)
            ;

        foreach (var entry in entries)
        {
            entry.SoftDelete();
            context.Set<AccessControlListEntry>().Update(entry);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
