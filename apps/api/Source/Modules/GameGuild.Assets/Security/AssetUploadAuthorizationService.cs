using Microsoft.EntityFrameworkCore;

namespace GameGuild.Assets.Security;

public sealed class AssetUploadAuthorizationService(
    IApplicationDbContext context,
    IEnumerable<IAssetParentAuthorizationResolver> parentResolvers) : IAssetUploadAuthorizationService
{
    private readonly IReadOnlyList<IAssetParentAuthorizationResolver> _parentResolvers = parentResolvers.ToArray();

    public async Task<bool> CanUploadAsync(
        string? parentResourceType,
        Guid? parentResourceId,
        Guid? folderId,
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || tenantId == null)
        {
            return false;
        }

        var hasType = !string.IsNullOrWhiteSpace(parentResourceType);
        var hasId = parentResourceId.HasValue;
        if (hasType != hasId)
        {
            return false;
        }

        // Personal assets without a parent remain supported, but cannot target a scoped folder.
        if (!hasType)
        {
            return folderId == null;
        }

        var resolver = _parentResolvers.FirstOrDefault(candidate => candidate.Supports(parentResourceType!));
        if (resolver == null || !await resolver.CanManageAsync(
                parentResourceId!.Value,
                userId,
                tenantId,
                cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (folderId == null)
        {
            return true;
        }

        return await context.Set<AssetFolder>().AsNoTracking().AnyAsync(folder =>
            folder.Id == folderId &&
            folder.TenantId == tenantId &&
            folder.DeletedAt == null &&
            folder.ParentResourceId == parentResourceId &&
            folder.ParentResourceType.ToLower() == parentResourceType!.ToLower(),
            cancellationToken).ConfigureAwait(false);
    }
}
