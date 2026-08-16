using FluentValidation;
using GameGuild.Resources;

namespace GameGuild.Assets.Commands;

/// <summary>
/// Command to upload a new asset.
/// </summary>
/// <remarks>
/// Quota enforcement:
/// - Assets: Counts against total asset files per tenant
/// - AssetStorage: File size in bytes counted post-upload via SecureUploadService
/// </remarks>
[RequiresQuota(ResourceUsageType.Assets, 1)]
public sealed record UploadAssetCommand(
    Stream Content,
    string FileName,
    string MimeType,
    Guid UserId,
    Guid? TenantId,
    string? DisplayName = null,
    AssetAccessPolicy AccessPolicy = AssetAccessPolicy.Private,
    string? ParentResourceType = null,
    Guid? ParentResourceId = null,
    Guid? FolderId = null) : IRequest<UploadAssetResponse>;

public sealed record UploadAssetResponse(
    Guid AssetReferenceId,
    Guid AssetContentId,
    string ContentHash,
    bool WasDeduped,
    string? Error = null);

public sealed class UploadAssetValidator : AbstractValidator<UploadAssetCommand>
{
    public UploadAssetValidator()
    {
        RuleFor(x => x.Content).NotNull();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.MimeType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.DisplayName).MaximumLength(255).When(x => x.DisplayName != null);
        RuleFor(x => x.ParentResourceType).MaximumLength(100).When(x => x.ParentResourceType != null);
        RuleFor(x => x.ParentResourceId).NotEmpty().When(x => !string.IsNullOrWhiteSpace(x.ParentResourceType));
        RuleFor(x => x.ParentResourceType).NotEmpty().When(x => x.ParentResourceId.HasValue);
        RuleFor(x => x.ParentResourceId).NotEmpty().When(x => x.FolderId.HasValue);
    }
}

public sealed class UploadAssetHandler : IRequestHandler<UploadAssetCommand, UploadAssetResponse>
{
    private readonly ISecureUploadService _secureUploadService;
    private readonly IAssetContentRepository _contentRepository;
    private readonly IAssetUploadAuthorizationService _authorizationService;

    public UploadAssetHandler(
        ISecureUploadService secureUploadService,
        IAssetContentRepository contentRepository,
        IAssetUploadAuthorizationService authorizationService)
    {
        _secureUploadService = secureUploadService;
        _contentRepository = contentRepository;
        _authorizationService = authorizationService;
    }

    public async Task<UploadAssetResponse> Handle(
        UploadAssetCommand request,
        CancellationToken ct = default)
    {
        if (!await _authorizationService.CanUploadAsync(
                request.ParentResourceType,
                request.ParentResourceId,
                request.FolderId,
                request.UserId,
                request.TenantId,
                ct).ConfigureAwait(false))
            return new UploadAssetResponse(Guid.Empty, Guid.Empty, string.Empty, false, "Forbidden");

        if (!request.TenantId.HasValue)
            return new UploadAssetResponse(Guid.Empty, Guid.Empty, string.Empty, false, "Tenant context is required");

        var options = new UploadAssetOptions(
            request.DisplayName ?? request.FileName,
            request.AccessPolicy,
            request.ParentResourceType,
            request.ParentResourceId,
            request.FolderId,
            request.TenantId);

        var result = await _secureUploadService.UploadWithSecurityChecksAsync(
            request.Content,
            request.FileName,
            request.MimeType,
            request.UserId,
            request.TenantId.Value,
            options,
            ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return new UploadAssetResponse(
                Guid.Empty, Guid.Empty, string.Empty, false, result.Error ?? "Upload failed");
        }

        // Get content to check if it was deduped
        var content = await _contentRepository.GetByIdAsync(result.AssetContentId!.Value, ct).ConfigureAwait(false);
        var wasDeduped = content?.ReferenceCount > 1;

        return new UploadAssetResponse(
            result.AssetReferenceId!.Value,
            result.AssetContentId!.Value,
            content?.ContentHash ?? string.Empty,
            wasDeduped);
    }
}
