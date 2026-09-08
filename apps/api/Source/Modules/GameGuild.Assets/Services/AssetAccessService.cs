using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GameGuild.Features;
using GameGuild.Identity.Tenants;

namespace GameGuild.Assets;

/// <summary>
/// Options for asset access URLs.
/// </summary>
public class AssetAccessOptions
{
    public const string SectionName = "Assets:Access";

    public string BaseUrl { get; set; } = string.Empty;
    public int DefaultExpiryMinutes { get; set; } = 60;
    public bool UsePresignedUrls { get; set; } = true;
}

/// <summary>
/// Implementation of access URL generation for assets.
/// </summary>
public class AssetAccessService : IAssetAccessService
{
    private readonly IAssetReferenceRepository _referenceRepository;
    private readonly ITransformedAssetRepository _transformedAssetRepository;
    private readonly IAssetStorageService _storageService;
    private readonly IAssetTokenService _tokenService;
    private readonly ITenantMemberRepository _tenantMemberRepository;
    private readonly IFeatureFlagEvaluationService _featureService;
    private readonly IReadOnlyList<IAssetParentAuthorizationResolver> _parentAuthorizationResolvers;
    private readonly IAssetFolderAuthorizationService _folderAuthorizationService;
    private readonly IAssetScopedAccessService _scopedAccessService;
    private readonly AssetAccessOptions _options;
    private readonly ILogger<AssetAccessService> _logger;

    public AssetAccessService(
        IAssetReferenceRepository referenceRepository,
        ITransformedAssetRepository transformedAssetRepository,
        IAssetStorageService storageService,
        IAssetTokenService tokenService,
        ITenantMemberRepository tenantMemberRepository,
        IFeatureFlagEvaluationService featureService,
        IEnumerable<IAssetParentAuthorizationResolver> parentAuthorizationResolvers,
        IAssetFolderAuthorizationService folderAuthorizationService,
        IAssetScopedAccessService scopedAccessService,
        IOptions<AssetAccessOptions> options,
        ILogger<AssetAccessService> logger)
    {
        _referenceRepository = referenceRepository;
        _transformedAssetRepository = transformedAssetRepository;
        _storageService = storageService;
        _tokenService = tokenService;
        _tenantMemberRepository = tenantMemberRepository;
        _featureService = featureService;
        _parentAuthorizationResolvers = parentAuthorizationResolvers.ToArray();
        _folderAuthorizationService = folderAuthorizationService;
        _scopedAccessService = scopedAccessService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AssetAccessUrl?> GenerateAccessUrlAsync(
        Guid assetReferenceId,
        Guid? userId,
        Guid? tenantId,
        TransformationSpec? transformation = null,
        CancellationToken ct = default)
    {
        var reference = await _referenceRepository.GetByIdWithContentAsync(assetReferenceId, ct).ConfigureAwait(false);
        if (reference == null || reference.Content == null)
        {
            return null;
        }

        // Check access policy
        var validation = await ValidateAccessAsync(assetReferenceId, userId, tenantId, ct).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            _logger.LogWarning(
                "Access denied to asset {AssetId} for user {UserId}: {Reason}",
                assetReferenceId, userId, validation.DeniedReason);
            return null;
        }

        // Check content status
        if (reference.Content.VirusScanStatus == VirusScanStatus.Infected ||
            reference.Content.ModerationStatus == ModerationStatus.Blocked)
        {
            return null;
        }

        // Feature flag check for transformations
        if (transformation != null && !transformation.IsIdentity)
        {
            var featureContext = CreateFeatureContext(userId, tenantId);
            var transformationsEnabled = await _featureService.IsEnabledAsync(
                FeatureFlagConstants.AssetFeatureFlags.TransformationsEnabled,
                featureContext,
                ct).ConfigureAwait(false);

            if (!transformationsEnabled)
            {
                _logger.LogDebug(
                    "Transformations disabled by feature flag for tenant {TenantId}",
                    tenantId);
                transformation = null; // Fall back to original
            }
            else
            {
                // Check max dimension limit
                var maxDimension = await _featureService.GetValueAsync<int>(
                    FeatureFlagConstants.AssetFeatureFlags.MaxTransformDimension,
                    featureContext,
                    4096, // Default max dimension
                    ct).ConfigureAwait(false);

                if ((transformation.Width.HasValue && transformation.Width > maxDimension) ||
                    (transformation.Height.HasValue && transformation.Height > maxDimension))
                {
                    _logger.LogWarning(
                        "Transformation dimensions exceed limit {MaxDimension} for tenant {TenantId}",
                        maxDimension, tenantId);
                    return null;
                }
            }
        }

        // Get download window from feature flag
        var downloadWindowHours = await GetDownloadWindowHoursAsync(userId, tenantId, ct).ConfigureAwait(false);
        var expiryMinutes = downloadWindowHours * 60;

        // Generate token
        var token = _tokenService.GenerateToken(
            assetReferenceId,
            tenantId ?? Guid.Empty,
            reference.AccessPolicy,
            transformation,
            TimeSpan.FromMinutes(expiryMinutes));

        var url = BuildAccessUrl(assetReferenceId, token, transformation);
        var expiry = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);

        // Record access
        await _referenceRepository.RecordAccessAsync(assetReferenceId, ct).ConfigureAwait(false);

        return new AssetAccessUrl(url, token, expiry, reference.Content.MimeType);
    }

    /// <summary>
    /// Gets the download window hours from feature flags.
    /// </summary>
    private async Task<int> GetDownloadWindowHoursAsync(
        Guid? userId,
        Guid? tenantId,
        CancellationToken ct)
    {
        var featureContext = CreateFeatureContext(userId, tenantId);
        return await _featureService.GetValueAsync<int>(
            FeatureFlagConstants.AssetFeatureFlags.DownloadWindowHours,
            featureContext,
            _options.DefaultExpiryMinutes / 60, // Fall back to config
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a feature evaluation context.
    /// </summary>
    private static FeatureContext CreateFeatureContext(Guid? userId, Guid? tenantId)
    {
        return new FeatureContext
        {
            UserId = userId,
            TenantId = tenantId
        };
    }

    public async Task<AssetAccessUrl?> GenerateDirectStorageUrlAsync(
        Guid assetReferenceId,
        Guid? userId,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        var reference = await _referenceRepository.GetByIdWithContentAsync(assetReferenceId, ct).ConfigureAwait(false);
        if (reference == null || reference.Content == null)
        {
            return null;
        }

        // Check access
        var validation = await ValidateAccessAsync(assetReferenceId, userId, tenantId, ct).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return null;
        }

        // Generate presigned URL directly from storage
        var expiry = TimeSpan.FromMinutes(_options.DefaultExpiryMinutes);
        var presignedUrl = await _storageService.GeneratePresignedUrlAsync(
            reference.Content.BucketName,
            reference.Content.ObjectKey,
            expiry,
            isDownload: true,
            ct).ConfigureAwait(false);

        await _referenceRepository.RecordAccessAsync(assetReferenceId, ct).ConfigureAwait(false);

        return new AssetAccessUrl(
            presignedUrl,
            string.Empty, // No token for direct access
            DateTimeOffset.UtcNow.Add(expiry),
            reference.Content.MimeType);
    }

    public async Task<AssetAccessValidation> ValidateAccessAsync(
        Guid assetReferenceId,
        Guid? userId,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        var reference = await _referenceRepository.GetByIdAsync(assetReferenceId, ct).ConfigureAwait(false);
        if (reference == null)
        {
            return new AssetAccessValidation(false, AssetAccessDeniedReason.NotFound);
        }

        // Check if soft-deleted
        if (reference.IsDeleted)
        {
            return new AssetAccessValidation(false, AssetAccessDeniedReason.NotFound);
        }

        // Check access policy
        switch (reference.AccessPolicy)
        {
            case AssetAccessPolicy.Private:
                if (userId == null)
                {
                    return new AssetAccessValidation(false, AssetAccessDeniedReason.AuthenticationRequired);
                }

                if (reference.CreatedByUserId == userId)
                {
                    return new AssetAccessValidation(true, null);
                }

                if (tenantId == null)
                {
                    return new AssetAccessValidation(false, AssetAccessDeniedReason.OwnershipRequired);
                }

                var membership = await _tenantMemberRepository
                    .GetByUserAndTenantAsync(userId.Value, tenantId.Value, ct)
                    .ConfigureAwait(false);

                if (membership is { IsActive: true } &&
                    (string.Equals(membership.Role, TenantRole.Owner.Value, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(membership.Role, TenantRole.Admin.Value, StringComparison.OrdinalIgnoreCase)))
                {
                    return new AssetAccessValidation(true, null);
                }

                return new AssetAccessValidation(false, AssetAccessDeniedReason.OwnershipRequired);

            case AssetAccessPolicy.Public:
                return new AssetAccessValidation(true, null);

            case AssetAccessPolicy.Unlisted:
                // Unlisted assets are accessible to anyone with the URL
                return new AssetAccessValidation(true, null);

            case AssetAccessPolicy.Authenticated:
                if (userId == null)
                {
                    return new AssetAccessValidation(false, AssetAccessDeniedReason.AuthenticationRequired);
                }
                return new AssetAccessValidation(true, null);

            case AssetAccessPolicy.OwnerOnly:
                if (userId == null)
                {
                    return new AssetAccessValidation(false, AssetAccessDeniedReason.AuthenticationRequired);
                }
                if (reference.CreatedByUserId != userId)
                {
                    return new AssetAccessValidation(false, AssetAccessDeniedReason.OwnershipRequired);
                }
                return new AssetAccessValidation(true, null);

            case AssetAccessPolicy.Inherited:
                if (userId == null)
                {
                    return new AssetAccessValidation(false, AssetAccessDeniedReason.AuthenticationRequired);
                }
                if (string.IsNullOrWhiteSpace(reference.ParentResourceType) || reference.ParentResourceId == null)
                {
                    return new AssetAccessValidation(false, AssetAccessDeniedReason.OwnershipRequired);
                }

                var parentResolver = _parentAuthorizationResolvers.FirstOrDefault(resolver =>
                    resolver.Supports(reference.ParentResourceType));
                if (parentResolver == null)
                {
                    return new AssetAccessValidation(false, AssetAccessDeniedReason.OwnershipRequired);
                }

                var canReadParent = await parentResolver.CanReadAsync(
                    reference.ParentResourceId.Value,
                    userId.Value,
                    tenantId,
                    ct).ConfigureAwait(false);
                if (!canReadParent)
                {
                    var hasScopedGrant = await _scopedAccessService.HasActiveGrantAsync(
                        reference.Id,
                        userId.Value,
                        tenantId,
                        ct).ConfigureAwait(false);
                    return hasScopedGrant
                        ? new AssetAccessValidation(true, null)
                        : new AssetAccessValidation(false, AssetAccessDeniedReason.OwnershipRequired);
                }

                var canReadFolder = await _folderAuthorizationService.CanReadAsync(
                    reference,
                    userId.Value,
                    tenantId,
                    ct).ConfigureAwait(false);
                return canReadFolder
                    ? new AssetAccessValidation(true, null)
                    : new AssetAccessValidation(false, AssetAccessDeniedReason.OwnershipRequired);

            default:
                return new AssetAccessValidation(false, AssetAccessDeniedReason.InvalidPolicy);
        }
    }

    public bool ValidateToken(
        string token,
        Guid assetReferenceId,
        Guid? tenantId)
    {
        var payload = _tokenService.ValidateToken(token, assetReferenceId, tenantId ?? Guid.Empty);
        return payload != null;
    }

    public bool ValidateToken(
        string token,
        Guid assetReferenceId,
        Guid? tenantId,
        TransformationSpec? transformation)
    {
        var payload = _tokenService.ValidateToken(
            token,
            assetReferenceId,
            tenantId ?? Guid.Empty,
            transformation);
        return payload != null;
    }

    private string BuildAccessUrl(
        Guid assetReferenceId,
        string token,
        TransformationSpec? transformation)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/assets/{assetReferenceId}/content?token={token}";

        if (transformation != null && !transformation.IsIdentity)
        {
            url += $"&transform={Uri.EscapeDataString(transformation.ToCanonicalString())}";
        }

        return url;
    }

    /// <inheritdoc />
    public async Task<TokenValidationResult> ValidateAccessTokenAsync(
        Guid assetReferenceId,
        string token,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new TokenValidationResult(false, "Token is required");
        }

        // Validate token signature and expiration
        var payload = _tokenService.ValidateToken(token, assetReferenceId, null);
        if (payload == null)
        {
            return new TokenValidationResult(false, "Invalid or expired token");
        }

        // Check that the asset still exists
        var reference = await _referenceRepository.GetByIdAsync(assetReferenceId, ct).ConfigureAwait(false);
        if (reference == null || reference.IsDeleted)
        {
            return new TokenValidationResult(false, "Asset not found");
        }

        return new TokenValidationResult(
            true,
            null,
            payload.UserId,
            payload.ExpiresAt);
    }

    /// <inheritdoc />
    public Task<EphemeralTokenValidationResult> ValidateEphemeralTokenAsync(
        string token,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(new EphemeralTokenValidationResult(
                false, Guid.Empty, false, "Token is required"));
        }

        // Decode ephemeral token (format: base64({assetId}:{expiry}:{signature}))
        var ephemeralPayload = _tokenService.ValidateEphemeralToken(token);
        if (ephemeralPayload == null)
        {
            return Task.FromResult(new EphemeralTokenValidationResult(
                false, Guid.Empty, false, "Invalid ephemeral token"));
        }

        if (ephemeralPayload.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return Task.FromResult(new EphemeralTokenValidationResult(
                false, ephemeralPayload.AssetReferenceId, true, "Token has expired"));
        }

        return Task.FromResult(new EphemeralTokenValidationResult(
            true, ephemeralPayload.AssetReferenceId));
    }

    /// <inheritdoc />
    public async Task<TransformedAssetInfo?> GetOrCreateTransformationAsync(
        Guid contentId,
        TransformationSpec spec,
        CancellationToken ct = default)
    {
        // Check feature flag for transformations
        var featureContext = CreateFeatureContext(null, null);
        var transformationsEnabled = await _featureService.IsEnabledAsync(
            FeatureFlagConstants.AssetFeatureFlags.TransformationsEnabled,
            featureContext,
            ct).ConfigureAwait(false);

        if (!transformationsEnabled)
        {
            _logger.LogWarning("Transformations disabled by feature flag");
            return null;
        }

        var canonicalSpec = spec.ToCanonicalString();
        var transformedAsset = await _transformedAssetRepository
            .GetAsync(contentId, canonicalSpec, ct)
            .ConfigureAwait(false);

        if (transformedAsset == null)
        {
            _logger.LogInformation(
                "No cached transformed asset found for content {ContentId} and spec {Spec}",
                contentId, canonicalSpec);
            return null;
        }

        var metadata = await _storageService
            .GetMetadataAsync(transformedAsset.BucketName, transformedAsset.ObjectKey, ct)
            .ConfigureAwait(false);

        transformedAsset.RecordAccess();
        await _transformedAssetRepository.UpdateAsync(transformedAsset, ct).ConfigureAwait(false);

        return new TransformedAssetInfo(
            transformedAsset.Id,
            transformedAsset.SourceContentId,
            transformedAsset.BucketName,
            transformedAsset.ObjectKey,
            metadata?.MimeType ?? transformedAsset.MimeType,
            metadata?.ETag ?? transformedAsset.ObjectKey,
            metadata?.SizeBytes ?? transformedAsset.SizeBytes);
    }
}
