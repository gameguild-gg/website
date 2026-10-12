using GameGuild.CQRS.Models;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Default <see cref="IResourceOwnershipProvider"/>: ownership v1 (issue #372) is the
///     unrevoked <see cref="ResourceUserPermission"/> row flagged <c>IsOwner = true</c>
///     for the exact (tenant, user, resource type, resource id) tuple. The row is created
///     by the resource-creation auto-grant (<c>GrantOwnerPermissionsOnCreationAsync</c> —
///     the creator is the owner by default, recorded through the entity's
///     <c>CreatedBy => GrantedByUserId</c> mapping) and moves only through the guarded,
///     versioned and audited <c>TransferOwnershipCommand</c>, which soft-revokes the
///     previous owner row instead of hard-deleting it.
/// </summary>
public sealed class ResourceOwnershipService(IApplicationDbContext dbContext)
    : IResourceOwnershipProvider
{
    /// <inheritdoc />
    public async Task<ResourceOwnership?> GetOwnerAsync(
        Guid tenantId,
        Guid userId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty
            || string.IsNullOrWhiteSpace(resourceType) || string.IsNullOrWhiteSpace(resourceId))
        {
            return null;
        }

        var tenantKey = new TenantId(tenantId);
        var row = await dbContext.Set<ResourceUserPermission>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                    p.TenantId == tenantKey
                    && p.UserId == userId
                    && p.ResourceType == resourceType
                    && p.ResourceId == resourceId
                    && p.IsOwner
                    && p.RevokedAt == null,
                cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? null
            : new ResourceOwnership(
                row.UserId,
                row.TenantId.Value,
                row.ResourceType,
                row.ResourceId,
                row.Permissions,
                row.ExpiresAt,
                row.Id);
    }
}
