namespace GameGuild.Identity.Authentication;

/// <summary>
///     Repository for managing trusted devices.
///     Stores device fingerprints and trust relationships for streamlined authentication.
/// </summary>
public interface ITrustedDeviceRepository
{
    /// <summary>
    ///     Creates a new trusted device record.
    /// </summary>
    Task<TrustedDevice> CreateAsync(TrustedDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a trusted device by ID.
    /// </summary>
    Task<TrustedDevice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a trusted device by user ID and device fingerprint.
    /// </summary>
    Task<TrustedDevice?> GetByUserAndFingerprintAsync(Guid userId, string deviceFingerprint, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all trusted devices for a user.
    /// </summary>
    Task<List<TrustedDevice>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all active (non-expired) trusted devices for a user.
    /// </summary>
    Task<List<TrustedDevice>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates a trusted device (for last used date, etc.).
    /// </summary>
    Task<TrustedDevice> UpdateAsync(TrustedDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revokes trust for a specific device.
    /// </summary>
    Task RevokeAsync(Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revokes all trusted devices for a user.
    /// </summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes expired trusted devices (cleanup task).
    /// </summary>
    Task DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks whether a device fingerprint is currently trusted for a user.
    /// </summary>
    Task<bool> IsDeviceTrustedAsync(Guid userId, string deviceFingerprint, CancellationToken cancellationToken = default);
}
