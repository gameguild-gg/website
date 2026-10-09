using Microsoft.EntityFrameworkCore;

namespace GameGuild.Assets;

public interface IAssetLibraryService
{
    Task<AssetLibraryResult<AssetLibraryView>> GetAsync(string resourceType, Guid resourceId, Guid userId, Guid? tenantId, CancellationToken ct = default);
    Task<AssetLibraryResult<AssetFolder>> CreateFolderAsync(string resourceType, Guid resourceId, Guid userId, Guid? tenantId, string name, Guid? parentFolderId, CancellationToken ct = default);
    Task<AssetLibraryResult<AssetFolder>> RestrictFolderAsync(Guid folderId, Guid userId, Guid? tenantId, AssetFolderRestrictionMode mode, IReadOnlyCollection<Guid> teamIds, IReadOnlyCollection<string> authorities, CancellationToken ct = default);
    Task<AssetLibraryResult<AssetReference>> CopyAsync(Guid referenceId, Guid userId, Guid? tenantId, string? displayName, Guid? folderId, CancellationToken ct = default);
    Task<AssetLibraryResult<IReadOnlyList<AssetReferenceRevision>>> GetRevisionsAsync(Guid referenceId, Guid userId, Guid? tenantId, CancellationToken ct = default);
    Task<AssetLibraryResult<AssetReferenceRevision>> RestoreRevisionAsync(Guid referenceId, Guid revisionId, Guid userId, Guid? tenantId, CancellationToken ct = default);
}

public sealed record AssetLibraryView(
    IReadOnlyList<AssetFolder> Folders,
    IReadOnlyList<AssetReference> Assets);

public sealed record AssetLibraryResult<T>(bool IsSuccess, T? Value, string? Error)
{
    public static AssetLibraryResult<T> Success(T value) => new(true, value, null);
    public static AssetLibraryResult<T> Failure(string error) => new(false, default, error);
}

public sealed class AssetLibraryService(
    IApplicationDbContext context,
    IEnumerable<IAssetParentAuthorizationResolver> parentResolvers,
    IAssetAccessService assetAccessService,
    IAssetFolderAuthorizationService folderAuthorizationService) : IAssetLibraryService
{
    private readonly IReadOnlyList<IAssetParentAuthorizationResolver> _parentResolvers = parentResolvers.ToArray();

    public async Task<AssetLibraryResult<AssetLibraryView>> GetAsync(
        string resourceType,
        Guid resourceId,
        Guid userId,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        if (!await CanReadParentAsync(resourceType, resourceId, userId, tenantId, ct).ConfigureAwait(false))
        {
            return AssetLibraryResult<AssetLibraryView>.Failure("NotFound");
        }

        var normalizedResourceType = resourceType.ToLowerInvariant();
        var folderCandidates = await context.Set<AssetFolder>().AsNoTracking()
            .Where(folder => folder.ParentResourceId == resourceId && folder.ParentResourceType.ToLower() == normalizedResourceType)
            .OrderBy(folder => folder.Name).ToListAsync(ct).ConfigureAwait(false);
        var folders = new List<AssetFolder>(folderCandidates.Count);
        foreach (var folder in folderCandidates)
        {
            if (await folderAuthorizationService.CanReadFolderAsync(folder, userId, tenantId, ct).ConfigureAwait(false))
            {
                folders.Add(folder);
            }
        }

        var candidates = await context.Set<AssetReference>().AsNoTracking()
            .Where(reference => reference.ParentResourceId == resourceId && reference.ParentResourceType != null &&
                                reference.ParentResourceType.ToLower() == normalizedResourceType)
            .OrderBy(reference => reference.DisplayName).ToListAsync(ct).ConfigureAwait(false);
        var assets = new List<AssetReference>();
        foreach (var reference in candidates)
        {
            var access = await assetAccessService.ValidateAccessAsync(reference.Id, userId, tenantId, ct).ConfigureAwait(false);
            if (access.IsValid)
            {
                assets.Add(reference);
            }
        }
        return AssetLibraryResult<AssetLibraryView>.Success(new AssetLibraryView(folders, assets));
    }

    public async Task<AssetLibraryResult<AssetFolder>> CreateFolderAsync(
        string resourceType,
        Guid resourceId,
        Guid userId,
        Guid? tenantId,
        string name,
        Guid? parentFolderId,
        CancellationToken ct = default)
    {
        if (tenantId == null || !await CanManageParentAsync(resourceType, resourceId, userId, tenantId, ct).ConfigureAwait(false))
        {
            return AssetLibraryResult<AssetFolder>.Failure("Forbidden");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return AssetLibraryResult<AssetFolder>.Failure("Validation");
        }

        if (parentFolderId.HasValue)
        {
            var normalizedResourceType = resourceType.ToLowerInvariant();
            if (!await context.Set<AssetFolder>().AnyAsync(folder =>
                    folder.Id == parentFolderId && folder.ParentResourceId == resourceId &&
                    folder.ParentResourceType.ToLower() == normalizedResourceType &&
                    folder.TenantId == tenantId, ct).ConfigureAwait(false))
            {
                return AssetLibraryResult<AssetFolder>.Failure("InvalidParentFolder");
            }
        }

        var folder = AssetFolder.Create(tenantId.Value, resourceType, resourceId, parentFolderId, name);
        context.Set<AssetFolder>().Add(folder);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return AssetLibraryResult<AssetFolder>.Success(folder);
    }

    public async Task<AssetLibraryResult<AssetFolder>> RestrictFolderAsync(
        Guid folderId,
        Guid userId,
        Guid? tenantId,
        AssetFolderRestrictionMode mode,
        IReadOnlyCollection<Guid> teamIds,
        IReadOnlyCollection<string> authorities,
        CancellationToken ct = default)
    {
        var folder = await context.Set<AssetFolder>().SingleOrDefaultAsync(candidate => candidate.Id == folderId, ct).ConfigureAwait(false);
        if (folder == null)
        {
            return AssetLibraryResult<AssetFolder>.Failure("NotFound");
        }

        if (!await CanManageParentAsync(folder.ParentResourceType, folder.ParentResourceId, userId, tenantId, ct).ConfigureAwait(false))
        {
            return AssetLibraryResult<AssetFolder>.Failure("Forbidden");
        }

        folder.SetRestriction(mode, teamIds, authorities);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return AssetLibraryResult<AssetFolder>.Success(folder);
    }

    public async Task<AssetLibraryResult<AssetReference>> CopyAsync(
        Guid referenceId,
        Guid userId,
        Guid? tenantId,
        string? displayName,
        Guid? folderId,
        CancellationToken ct = default)
    {
        var access = await assetAccessService.ValidateAccessAsync(referenceId, userId, tenantId, ct).ConfigureAwait(false);
        if (!access.IsValid)
        {
            return AssetLibraryResult<AssetReference>.Failure("NotFound");
        }

        var source = await context.Set<AssetReference>().SingleOrDefaultAsync(reference => reference.Id == referenceId, ct).ConfigureAwait(false);
        if (source?.ParentResourceId == null || string.IsNullOrWhiteSpace(source.ParentResourceType))
        {
            return AssetLibraryResult<AssetReference>.Failure("NotFound");
        }

        if (!await CanManageParentAsync(source.ParentResourceType, source.ParentResourceId.Value, userId, tenantId, ct).ConfigureAwait(false))
        {
            return AssetLibraryResult<AssetReference>.Failure("Forbidden");
        }

        if (folderId.HasValue && !await context.Set<AssetFolder>().AnyAsync(folder =>
                folder.Id == folderId && folder.ParentResourceId == source.ParentResourceId &&
                folder.ParentResourceType.ToLower() == source.ParentResourceType.ToLower() &&
                folder.TenantId == tenantId, ct).ConfigureAwait(false))
        {
            return AssetLibraryResult<AssetReference>.Failure("InvalidFolder");
        }

        var copy = source.CopyTo(userId, displayName, folderId);
        context.Set<AssetReference>().Add(copy);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return AssetLibraryResult<AssetReference>.Success(copy);
    }

    public async Task<AssetLibraryResult<IReadOnlyList<AssetReferenceRevision>>> GetRevisionsAsync(
        Guid referenceId,
        Guid userId,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        var access = await assetAccessService.ValidateAccessAsync(referenceId, userId, tenantId, ct).ConfigureAwait(false);
        if (!access.IsValid)
        {
            return AssetLibraryResult<IReadOnlyList<AssetReferenceRevision>>.Failure("NotFound");
        }

        var revisions = await context.Set<AssetReferenceRevision>().AsNoTracking()
            .Where(revision => revision.AssetReferenceId == referenceId)
            .OrderByDescending(revision => revision.RevisionNumber).ToListAsync(ct).ConfigureAwait(false);
        return AssetLibraryResult<IReadOnlyList<AssetReferenceRevision>>.Success(revisions);
    }

    public async Task<AssetLibraryResult<AssetReferenceRevision>> RestoreRevisionAsync(
        Guid referenceId,
        Guid revisionId,
        Guid userId,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        var access = await assetAccessService.ValidateAccessAsync(referenceId, userId, tenantId, ct).ConfigureAwait(false);
        if (!access.IsValid)
        {
            return AssetLibraryResult<AssetReferenceRevision>.Failure("NotFound");
        }

        var reference = await context.Set<AssetReference>().Include(candidate => candidate.Revisions)
            .SingleOrDefaultAsync(candidate => candidate.Id == referenceId, ct).ConfigureAwait(false);
        if (reference?.ParentResourceId == null || string.IsNullOrWhiteSpace(reference.ParentResourceType))
        {
            return AssetLibraryResult<AssetReferenceRevision>.Failure("NotFound");
        }

        if (!await CanManageParentAsync(reference.ParentResourceType, reference.ParentResourceId.Value, userId, tenantId, ct).ConfigureAwait(false))
        {
            return AssetLibraryResult<AssetReferenceRevision>.Failure("Forbidden");
        }

        var revision = reference.Revisions.SingleOrDefault(candidate => candidate.Id == revisionId);
        if (revision == null)
        {
            return AssetLibraryResult<AssetReferenceRevision>.Failure("RevisionNotFound");
        }

        var restored = reference.RestoreRevision(revision, userId);
        context.Set<AssetReferenceRevision>().Add(restored);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return AssetLibraryResult<AssetReferenceRevision>.Success(restored);
    }

    private Task<bool> CanReadParentAsync(string resourceType, Guid resourceId, Guid userId, Guid? tenantId, CancellationToken ct) =>
        Resolve(resourceType)?.CanReadAsync(resourceId, userId, tenantId, ct) ?? Task.FromResult(false);

    private Task<bool> CanManageParentAsync(string resourceType, Guid resourceId, Guid userId, Guid? tenantId, CancellationToken ct) =>
        Resolve(resourceType)?.CanManageAsync(resourceId, userId, tenantId, ct) ?? Task.FromResult(false);

    private IAssetParentAuthorizationResolver? Resolve(string resourceType) =>
        _parentResolvers.FirstOrDefault(resolver => resolver.Supports(resourceType));
}
