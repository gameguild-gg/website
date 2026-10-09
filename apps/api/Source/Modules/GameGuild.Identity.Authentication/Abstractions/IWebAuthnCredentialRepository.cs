namespace GameGuild.Identity.Authentication;

/// <summary>
///     Repository for managing WebAuthn/FIDO2 credentials.
/// </summary>
public interface IWebAuthnCredentialRepository
{
    /// <summary>
    ///     Get a credential by its unique ID.
    /// </summary>
    Task<UserWebAuthnCredential?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a credential by its credential ID (from authenticator).
    /// </summary>
    Task<UserWebAuthnCredential?> GetByCredentialIdAsync(string credentialId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all credentials for a user.
    /// </summary>
    Task<List<UserWebAuthnCredential>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all active credentials for a user.
    /// </summary>
    Task<List<UserWebAuthnCredential>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get credential IDs for a user (used during authentication).
    /// </summary>
    Task<List<string>> GetCredentialIdsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Create a new credential.
    /// </summary>
    Task<UserWebAuthnCredential> CreateAsync(UserWebAuthnCredential credential, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Update a credential (e.g., signature counter).
    /// </summary>
    Task<UserWebAuthnCredential> UpdateAsync(UserWebAuthnCredential credential, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete a credential.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if a user has any active WebAuthn credentials.
    /// </summary>
    Task<bool> HasActiveCredentialsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Count active credentials for a user.
    /// </summary>
    Task<int> CountActiveCredentialsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revoke a credential. Revocation is terminal: a revoked credential can never be reactivated.
    /// </summary>
    Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Temporarily deactivate an active credential. Returns false when the credential
    ///     does not exist or is not currently active (deactivated or revoked).
    /// </summary>
    Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reverse a temporary deactivation. Returns false when the credential does not
    ///     exist, is already active, or is revoked (revocation is terminal).
    /// </summary>
    Task<bool> ReactivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Update the signature counter and last used timestamp.
    /// </summary>
    Task UpdateSignatureCounterAsync(Guid id, uint newCounter, CancellationToken cancellationToken = default);
}
