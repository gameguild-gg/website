namespace GameGuild.Assets;

/// <summary>
/// Service for generating access URLs for assets.
/// </summary>
public interface IAssetAccessService
{
    /// <summary>
    /// Generates an access URL for an asset.
    /// </summary>
    Task<AssetAccessUrl?> GenerateAccessUrlAsync(
        Guid assetReferenceId,
        Guid? userId,
        Guid? tenantId,
        TransformationSpec? transformation = null,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a direct storage presigned URL (bypasses token system).
    /// </summary>
    Task<AssetAccessUrl?> GenerateDirectStorageUrlAsync(
        Guid assetReferenceId,
        Guid? userId,
        Guid? tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Validates access to an asset.
    /// </summary>
    /// <param name="assetReferenceId">The asset reference to validate.</param>
    /// <param name="userId">The requesting user, if authenticated.</param>
    /// <param name="tenantId">The request tenant context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="permitCrossTenant">
    ///     Explicit cross-tenant path: the asset's own tenant is allowed to differ from the
    ///     request tenant. Only SystemAdmin surfaces may pass <c>true</c>; the default is a
    ///     fail-closed tenant-mismatch denial.
    /// </param>
    Task<AssetAccessValidation> ValidateAccessAsync(
        Guid assetReferenceId,
        Guid? userId,
        Guid? tenantId,
        CancellationToken ct = default,
        bool permitCrossTenant = false);

    /// <summary>
    /// Validates a token for asset access.
    /// </summary>
    bool ValidateToken(
        string token,
        Guid assetReferenceId,
        Guid? tenantId);

    /// <summary>
    /// Validates a token for a specific transformed asset representation.
    /// </summary>
    bool ValidateToken(
        string token,
        Guid assetReferenceId,
        Guid? tenantId,
        TransformationSpec? transformation);

    /// <summary>
    /// Validates an access token asynchronously with additional checks.
    /// </summary>
    /// <param name="assetReferenceId">The asset reference ID.</param>
    /// <param name="token">The access token to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Token validation result.</returns>
    Task<TokenValidationResult> ValidateAccessTokenAsync(
        Guid assetReferenceId,
        string token,
        CancellationToken ct = default);

    /// <summary>
    /// Validates an ephemeral token (contains embedded asset reference).
    /// </summary>
    /// <param name="token">The ephemeral token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ephemeral token validation result.</returns>
    Task<EphemeralTokenValidationResult> ValidateEphemeralTokenAsync(
        string token,
        CancellationToken ct = default);

    /// <summary>
    /// Gets or creates a transformed version of an asset.
    /// </summary>
    /// <param name="contentId">The source content ID.</param>
    /// <param name="spec">The transformation specification.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The transformed asset, or null if transformation failed.</returns>
    Task<TransformedAssetInfo?> GetOrCreateTransformationAsync(
        Guid contentId,
        TransformationSpec spec,
        CancellationToken ct = default);

    /// <summary>
    /// Prewarms the default transformation variants for approved image content.
    /// CONTRACT: the caller MUST only invoke this after the content is (or is
    /// past) the Approved moderation state.
    /// </summary>
    Task PrewarmDefaultVariantsAsync(AssetContent content, CancellationToken ct = default);
}

/// <summary>
/// Generated access URL for an asset.
/// </summary>
public record AssetAccessUrl(
    string Url,
    string Token,
    DateTimeOffset ExpiresAt,
    string MimeType);

/// <summary>
/// Result of access validation.
/// </summary>
public record AssetAccessValidation(
    bool IsValid,
    AssetAccessDeniedReason? DeniedReason);

/// <summary>
/// Reason for access denial.
/// </summary>
public enum AssetAccessDeniedReason
{
    NotFound,
    TokenInvalid,
    TokenExpired,
    AuthenticationRequired,
    OwnershipRequired,
    TenantMismatch,
    InvalidPolicy,
    ContentRejected,
    ContentInfected
}

/// <summary>
/// Result of token validation.
/// </summary>
public sealed record TokenValidationResult(
    bool IsValid,
    string? Error = null,
    Guid? UserId = null,
    DateTimeOffset? ExpiresAt = null);

/// <summary>
/// Result of ephemeral token validation.
/// </summary>
public sealed record EphemeralTokenValidationResult(
    bool IsValid,
    Guid AssetReferenceId,
    bool IsExpired = false,
    string? Error = null);

/// <summary>
/// Information about a transformed asset.
/// </summary>
public record TransformedAssetInfo(
    Guid Id,
    Guid SourceContentId,
    string BucketName,
    string ObjectKey,
    string MimeType,
    string ContentHash,
    long SizeBytes);
