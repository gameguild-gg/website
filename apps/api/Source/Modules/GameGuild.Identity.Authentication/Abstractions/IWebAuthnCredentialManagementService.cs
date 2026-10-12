namespace GameGuild.Identity.Authentication;

/// <summary>
///     Manages WebAuthn/FIDO2 credential CRUD operations and status queries.
/// </summary>
public interface IWebAuthnCredentialManagementService
{
    /// <summary>
    ///     Get all credentials for a user.
    /// </summary>
    Task<List<WebAuthnCredentialInfo>> GetUserCredentialsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a single credential by ID.
    /// </summary>
    Task<WebAuthnCredentialInfo?> GetCredentialByIdAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if a credential exists for a user.
    /// </summary>
    Task<bool> CredentialExistsAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verify a credential is valid and can be used for authentication.
    /// </summary>
    Task<WebAuthnCredentialVerifyResult> VerifyCredentialAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete a credential. Deletion performs a terminal revocation: the deleted
    ///     credential can never be restored afterwards.
    /// </summary>
    Task<bool> DeleteCredentialAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Temporarily deactivate an active credential so it cannot be used for
    ///     authentication ceremonies. Deactivation is reversible via
    ///     <see cref="ActivateCredentialAsync" /> and never applies to revoked credentials.
    /// </summary>
    Task<WebAuthnCredentialTransitionResult> DeactivateCredentialAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reverse a temporary deactivation, returning the credential to active use.
    ///     A revoked credential is never reactivated: revocation is terminal.
    /// </summary>
    Task<WebAuthnCredentialTransitionResult> ActivateCredentialAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Update a credential's friendly name.
    /// </summary>
    Task<bool> UpdateCredentialNameAsync(
        Guid userId,
        Guid credentialId,
        string friendlyName,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if a user has WebAuthn enabled.
    /// </summary>
    Task<bool> IsWebAuthnEnabledAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
///     Result of an explicit WebAuthn credential lifecycle transition (deactivate/activate).
/// </summary>
public class WebAuthnCredentialTransitionResult
{
    /// <summary>
    ///     Whether the requested transition was applied.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Machine-readable failure reason (<c>CredentialNotFound</c>, <c>InvalidTransition</c>)
    ///     or <c>null</c> on success.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    ///     Human-readable explanation of the failure, or <c>null</c> on success.
    /// </summary>
    public string? ErrorDescription { get; set; }

    /// <summary>
    ///     The credential's lifecycle status after evaluating the request, or <c>null</c>
    ///     when the credential could not be found.
    /// </summary>
    public WebAuthnCredentialStatus? Status { get; set; }
}
