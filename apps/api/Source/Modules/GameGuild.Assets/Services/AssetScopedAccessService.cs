using Microsoft.EntityFrameworkCore;

namespace GameGuild.Assets;

public sealed class AssetScopedAccessService(IApplicationDbContext context) : IAssetScopedAccessService
{
    public async Task GrantAsync(
        IReadOnlyCollection<Guid> assetReferenceIds,
        Guid userId,
        Guid tenantId,
        string scopeType,
        Guid scopeId,
        DateTime expiresAt,
        Guid grantedByUserId,
        CancellationToken cancellationToken = default)
    {
        var referenceIds = assetReferenceIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (referenceIds.Length == 0)
        {
            return;
        }

        var validReferenceIds = await context.Set<AssetReference>().AsNoTracking()
            .Where(reference => referenceIds.Contains(reference.Id) && reference.TenantId == tenantId && reference.DeletedAt == null)
            .Select(reference => reference.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (validReferenceIds.Count != referenceIds.Length)
        {
            throw new InvalidOperationException("One or more submitted assets do not belong to the application tenant.");
        }

        var now = SystemClock.UtcNow;
        var existing = await context.Set<AssetScopedAccessGrant>()
            .Where(grant => grant.UserId == userId && grant.ScopeType == scopeType && grant.ScopeId == scopeId &&
                            validReferenceIds.Contains(grant.AssetReferenceId) && grant.RevokedAt == null &&
                            grant.ExpiresAt > now && grant.DeletedAt == null)
            .Select(grant => grant.AssetReferenceId).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var referenceId in validReferenceIds.Except(existing))
        {
            context.Set<AssetScopedAccessGrant>().Add(AssetScopedAccessGrant.Create(
            referenceId, userId, tenantId, scopeType, scopeId, expiresAt, grantedByUserId));
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> HasActiveGrantAsync(
        Guid assetReferenceId,
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == null)
        {
            return Task.FromResult(false);
        }

        var now = SystemClock.UtcNow;
        return context.Set<AssetScopedAccessGrant>().AsNoTracking().AnyAsync(grant =>
            grant.AssetReferenceId == assetReferenceId && grant.UserId == userId && grant.TenantId == tenantId &&
            grant.RevokedAt == null && grant.ExpiresAt > now && grant.DeletedAt == null,
            cancellationToken);
    }

    public async Task RevokeScopeAsync(string scopeType, Guid scopeId, CancellationToken cancellationToken = default)
    {
        var grants = await context.Set<AssetScopedAccessGrant>()
            .Where(grant => grant.ScopeType == scopeType && grant.ScopeId == scopeId && grant.RevokedAt == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var grant in grants)
        {
            grant.Revoke();
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
