namespace GameGuild.Assets.Commands;

public sealed record BulkUploadAssetInput(
    Stream Content,
    string FileName,
    string MimeType,
    string? DisplayName = null);

public sealed record BulkUploadAssetsCommand(
    IReadOnlyList<BulkUploadAssetInput> Files,
    Guid UserId,
    Guid? TenantId,
    AssetAccessPolicy AccessPolicy = AssetAccessPolicy.Private,
    string? ParentResourceType = null,
    Guid? ParentResourceId = null,
    Guid? FolderId = null) : IRequest<BulkUploadAssetsResponse>;

public sealed record BulkUploadAssetsResponse(
    int TotalRequested,
    int Successful,
    int Failed,
    IReadOnlyList<BulkUploadAssetItem> Items);

public sealed record BulkUploadAssetItem(
    string FileName,
    bool Success,
    Guid? AssetReferenceId,
    Guid? AssetContentId,
    string? Error);

public sealed class BulkUploadAssetsHandler(
    ISecureUploadService secureUploadService,
    IAssetUploadAuthorizationService authorizationService) : IRequestHandler<BulkUploadAssetsCommand, BulkUploadAssetsResponse>
{
    public async Task<BulkUploadAssetsResponse> Handle(
        BulkUploadAssetsCommand request,
        CancellationToken ct = default)
    {
        if (!await authorizationService.CanUploadAsync(
                request.ParentResourceType,
                request.ParentResourceId,
                request.FolderId,
                request.UserId,
                request.TenantId,
                ct).ConfigureAwait(false))
        {
            return new BulkUploadAssetsResponse(
                request.Files.Count,
                0,
                request.Files.Count,
                request.Files.Select(file => new BulkUploadAssetItem(
                    file.FileName, false, null, null, "Forbidden")).ToArray());
        }

        if (!request.TenantId.HasValue)
        {
            return new BulkUploadAssetsResponse(
                request.Files.Count,
                0,
                request.Files.Count,
                request.Files.Select(file => new BulkUploadAssetItem(
                    file.FileName, false, null, null, "Tenant context is required")).ToArray());
        }

        var items = new List<BulkUploadAssetItem>();

        foreach (var file in request.Files)
        {
            try
            {
                var options = new UploadAssetOptions(
                    file.DisplayName ?? file.FileName,
                    request.AccessPolicy,
                    request.ParentResourceType,
                    request.ParentResourceId,
                    request.FolderId,
                    request.TenantId);

                var result = await secureUploadService
                    .UploadWithSecurityChecksAsync(
                        file.Content,
                        file.FileName,
                        file.MimeType,
                        request.UserId,
                        request.TenantId.Value,
                        options,
                        ct)
                    .ConfigureAwait(false);

                items.Add(result.Success
                    ? new BulkUploadAssetItem(file.FileName, true, result.AssetReferenceId, result.AssetContentId, null)
                    : new BulkUploadAssetItem(file.FileName, false, null, null, result.Error ?? "Upload failed."));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                items.Add(new BulkUploadAssetItem(file.FileName, false, null, null, ex.Message));
            }
        }

        return new BulkUploadAssetsResponse(
            request.Files.Count,
            items.Count(item => item.Success),
            items.Count(item => !item.Success),
            items);
    }
}
