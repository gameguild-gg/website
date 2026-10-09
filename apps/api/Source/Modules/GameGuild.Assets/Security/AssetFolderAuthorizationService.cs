using Microsoft.EntityFrameworkCore;

namespace GameGuild.Assets.Security;

/// <summary>
/// Enforces restrictions that may only reduce access already granted by a parent resolver.
/// </summary>
public sealed class AssetFolderAuthorizationService(
    IApplicationDbContext context,
    IEnumerable<IAssetFolderRestrictionAuthorizationResolver> restrictionResolvers)
    : IAssetFolderAuthorizationService
{
    public async Task<bool> CanReadAsync(
        AssetReference reference,
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (reference.FolderId == null)
        {
            return true;
        }

        if (reference.ParentResourceId == null || string.IsNullOrWhiteSpace(reference.ParentResourceType))
        {
            return false;
        }

        if (reference.TenantId.HasValue && reference.TenantId != tenantId)
        {
            return false;
        }

        return await CanReadChainAsync(
            reference.FolderId.Value,
            reference.ParentResourceType,
            reference.ParentResourceId.Value,
            tenantId,
            userId,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> CanReadFolderAsync(
        AssetFolder folder,
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default) => CanReadChainAsync(
            folder.Id,
            folder.ParentResourceType,
            folder.ParentResourceId,
            tenantId,
            userId,
            cancellationToken);

    private async Task<bool> CanReadChainAsync(
        Guid initialFolderId,
        string parentResourceType,
        Guid parentResourceId,
        Guid? tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {

        Guid? folderId = initialFolderId;
        var visited = new HashSet<Guid>();
        while (folderId.HasValue)
        {
            if (!visited.Add(folderId.Value))
            {
                return false;
            }

            var folder = await context.Set<AssetFolder>().AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == folderId && candidate.DeletedAt == null,
                    cancellationToken).ConfigureAwait(false);
            if (folder == null || !folder.BelongsTo(parentResourceType, parentResourceId))
            {
                return false;
            }

            if (tenantId.HasValue && folder.TenantId != tenantId)
            {
                return false;
            }

            if (!await SatisfiesRestrictionAsync(folder, userId, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            folderId = folder.ParentFolderId;
        }

        return true;
    }

    private Task<bool> SatisfiesRestrictionAsync(
        AssetFolder folder,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (folder.RestrictionMode == AssetFolderRestrictionMode.None)
        {
            return Task.FromResult(true);
        }

        var resolver = restrictionResolvers.FirstOrDefault(candidate =>
            candidate.Supports(folder.RestrictionMode, folder.ParentResourceType));
        return resolver?.IsAuthorizedAsync(folder, userId, cancellationToken) ?? Task.FromResult(false);
    }
}
