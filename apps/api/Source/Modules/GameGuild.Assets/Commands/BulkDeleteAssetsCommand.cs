namespace GameGuild.Assets.Commands;

public sealed record BulkDeleteAssetsCommand(
    IReadOnlyList<Guid> AssetReferenceIds,
    Guid UserId,
    bool ForceDelete = false) : ICommand<BulkDeleteAssetsResponse>;

public sealed record BulkDeleteAssetsResponse(
    int TotalRequested,
    int Successful,
    int Failed,
    IReadOnlyList<BulkDeleteAssetItem> Items);

public sealed record BulkDeleteAssetItem(
    Guid AssetReferenceId,
    bool Success,
    bool ContentMarkedForDeletion,
    string? Error);

public sealed class BulkDeleteAssetsHandler(
    IAssetReferenceRepository referenceRepository,
    IAssetContentRepository contentRepository) : ICommandHandler<BulkDeleteAssetsCommand, BulkDeleteAssetsResponse>
{
    public async Task<BulkDeleteAssetsResponse> Handle(
        BulkDeleteAssetsCommand request,
        CancellationToken ct = default)
    {
        var items = new List<BulkDeleteAssetItem>();

        foreach (var assetId in request.AssetReferenceIds.Distinct())
        {
            var reference = await referenceRepository.GetByIdAsync(assetId, ct).ConfigureAwait(false);
            if (reference is null)
            {
                items.Add(new BulkDeleteAssetItem(assetId, false, false, "Asset not found."));
                continue;
            }

            if (!request.ForceDelete &&
                !await referenceRepository.IsOwnedByUserAsync(assetId, request.UserId, ct).ConfigureAwait(false))
            {
                items.Add(new BulkDeleteAssetItem(assetId, false, false, "Asset is not owned by the current user."));
                continue;
            }

            await referenceRepository.DeleteAsync(assetId, ct).ConfigureAwait(false);
            await contentRepository.DecrementReferenceCountAsync(reference.AssetContentId, ct).ConfigureAwait(false);

            var content = await contentRepository.GetByIdAsync(reference.AssetContentId, ct).ConfigureAwait(false);
            items.Add(new BulkDeleteAssetItem(
                assetId,
                true,
                content?.MarkedForDeletionAt is not null,
                null));
        }

        return new BulkDeleteAssetsResponse(
            request.AssetReferenceIds.Count,
            items.Count(item => item.Success),
            items.Count(item => !item.Success),
            items);
    }
}
