namespace GameGuild.Identity.Authentication;

/// <summary>
///     Repository for API-key credential persistence. API keys are the last credential
///     type without a repository abstraction; this interface mirrors the sibling
///     credential repositories (WebAuthn, ExternalLogin, TrustedDevice, RefreshToken).
/// </summary>
public interface IApiKeyRepository
{
    /// <summary>
    ///     Get a key by its SHA-256 hash (authentication lookup). The returned entity is
    ///     tracked, so usage and lazy rotation finalization can be persisted through
    ///     <see cref="RecordUsageAsync" /> and <see cref="FinalizeRotationRevocationAsync" />.
    /// </summary>
    Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a key by id, scoped to the owning user. Returns null when the key does not
    ///     exist or belongs to another user.
    /// </summary>
    Task<ApiKey?> GetByIdForUserAsync(Guid keyId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all keys for a user (including revoked ones), newest first.
    /// </summary>
    Task<List<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get the active keys for a user (not revoked), newest first. Expiry and rotation
    ///     grace are evaluated by <see cref="ApiKey.IsValid" />, not by this filter.
    /// </summary>
    Task<List<ApiKey>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Persist a new API key.
    /// </summary>
    Task<ApiKey> AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revoke a user's key. Returns the revoked key, or null when the key does not
    ///     exist or belongs to another user. Revocation is terminal.
    /// </summary>
    Task<ApiKey?> RevokeAsync(Guid keyId, Guid userId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Record a use of a key obtained from this repository (updates last-used
    ///     timestamp and usage counter).
    /// </summary>
    Task RecordUsageAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Atomically issue <paramref name="newKey" /> as the replacement for
    /// <paramref name="oldKey" />: links the new key via <see cref="ApiKey.ReplacesKeyId" />,
    ///     starts the rotation overlap window on the old key when <paramref name="graceEndsAt" />
    ///     has a value, or revokes it immediately when null. Both changes are persisted in
    ///     a single save.
    /// </summary>
    Task RotateAsync(ApiKey oldKey, ApiKey newKey, DateTime? graceEndsAt, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lazily finalize the revocation of a rotated key once its overlap window has
    ///     closed (no-op when there is nothing to finalize).
    /// </summary>
    Task FinalizeRotationRevocationAsync(ApiKey apiKey, CancellationToken cancellationToken = default);
}
