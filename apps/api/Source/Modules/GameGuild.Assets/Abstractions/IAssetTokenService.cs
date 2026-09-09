namespace GameGuild.Assets;

/// <summary>
/// Service for generating and validating signed asset access tokens.
/// </summary>
public interface IAssetTokenService
{
    /// <summary>
    /// Generates a signed access token for an asset.
    /// </summary>
    string GenerateToken(
        Guid assetReferenceId,
        Guid tenantId,
        AssetAccessPolicy accessPolicy,
        TransformationSpec? transformation = null,
        TimeSpan? customExpiry = null);

    /// <summary>
    /// Validates a token and returns the decoded payload if valid.
    /// </summary>
    AssetTokenPayload? ValidateToken(string token, Guid assetReferenceId, Guid? tenantId);

    /// <summary>
    /// Validates a token for a specific transformed asset representation.
    /// </summary>
    AssetTokenPayload? ValidateToken(
        string token,
        Guid assetReferenceId,
        Guid? tenantId,
        TransformationSpec? transformation);

    /// <summary>
    /// Generates an ephemeral token (self-contained, with embedded asset reference).
    /// </summary>
    string GenerateEphemeralToken(
        Guid assetReferenceId,
        TimeSpan expiry,
        Guid? userId = null);

    /// <summary>
    /// Validates an ephemeral token and extracts the asset reference.
    /// </summary>
    EphemeralTokenPayload? ValidateEphemeralToken(string token);

    /// <summary>
    /// Gets the current time window index.
    /// </summary>
    int GetCurrentTimeWindow();
}

/// <summary>
/// Decoded payload from an asset access token.
/// </summary>
public sealed record AssetTokenPayload(
    Guid AssetReferenceId,
    int TimeWindow,
    long ExpiryTimestamp,
    AssetAccessPolicy AccessPolicy,
    string TransformationSpec,
    Guid TenantId)
{
    /// <summary>
    /// Gets the user ID from the token (if present).
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Gets the expiration as DateTimeOffset.
    /// </summary>
    public DateTimeOffset ExpiresAt => DateTimeOffset.FromUnixTimeSeconds(ExpiryTimestamp);
}

/// <summary>
/// Decoded payload from an ephemeral access token.
/// </summary>
public sealed record EphemeralTokenPayload(
    Guid AssetReferenceId,
    DateTimeOffset ExpiresAt,
    Guid? UserId = null);
