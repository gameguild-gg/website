using Fido2NetLib;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Service for managing WebAuthn/FIDO2 passwordless authentication.
/// </summary>
public interface IWebAuthnService
{
    /// <summary>
    ///     Begin registration of a new WebAuthn credential.
    ///     Returns options to pass to the browser's navigator.credentials.create() API.
    /// </summary>
    Task<WebAuthnRegistrationOptionsResult> BeginRegistrationAsync(
        Guid userId,
        string userEmail,
        string displayName,
        WebAuthnAuthenticatorType? preferredType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Complete registration of a new WebAuthn credential.
    /// </summary>
    Task<WebAuthnRegistrationResult> CompleteRegistrationAsync(
        Guid userId,
        string attestationResponse,
        string? friendlyName = null,
        bool isPasswordless = false,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Begin authentication with WebAuthn.
    ///     Returns options to pass to the browser's navigator.credentials.get() API.
    /// </summary>
    Task<WebAuthnAuthenticationOptionsResult> BeginAuthenticationAsync(
        string? userEmail = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Complete authentication with WebAuthn.
    /// </summary>
    Task<WebAuthnAuthenticationResult> CompleteAuthenticationAsync(
        string assertionResponse,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all credentials for a user.
    /// </summary>
    Task<List<WebAuthnCredentialInfo>> GetUserCredentialsAsync(
        Guid userId,
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
    ///     Temporarily deactivate an active credential. Reversible via
    ///     <see cref="ActivateCredentialAsync" />; never applies to revoked credentials.
    /// </summary>
    Task<WebAuthnCredentialTransitionResult> DeactivateCredentialAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reverse a temporary deactivation. A revoked credential is never reactivated:
    ///     revocation is terminal.
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
}

/// <summary>
///     Result of beginning WebAuthn registration.
/// </summary>
public class WebAuthnRegistrationOptionsResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    /// <summary>
    ///     The challenge session ID to correlate with completion.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    ///     JSON options to pass to navigator.credentials.create().
    /// </summary>
    public string? OptionsJson { get; set; }

    /// <summary>
    ///     The Fido2 registration options object.
    /// </summary>
    public CredentialCreateOptions? Options { get; set; }
}

/// <summary>
///     Result of completing WebAuthn registration.
/// </summary>
public class WebAuthnRegistrationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    /// <summary>
    ///     The ID of the newly created credential.
    /// </summary>
    public Guid? CredentialId { get; set; }

    /// <summary>
    ///     Friendly name assigned to the credential.
    /// </summary>
    public string? FriendlyName { get; set; }
}

/// <summary>
///     Result of beginning WebAuthn authentication.
/// </summary>
public class WebAuthnAuthenticationOptionsResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    /// <summary>
    ///     The challenge session ID to correlate with completion.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    ///     JSON options to pass to navigator.credentials.get().
    /// </summary>
    public string? OptionsJson { get; set; }

    /// <summary>
    ///     The Fido2 assertion options object.
    /// </summary>
    public AssertionOptions? Options { get; set; }
}

/// <summary>
///     Result of completing WebAuthn authentication.
/// </summary>
public class WebAuthnAuthenticationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    /// <summary>
    ///     The authenticated user ID.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    ///     The credential that was used.
    /// </summary>
    public Guid? CredentialId { get; set; }

    /// <summary>
    ///     Whether this was a passwordless authentication.
    /// </summary>
    public bool IsPasswordless { get; set; }

    /// <summary>
    ///     Authenticated user's email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    ///     JWT access token issued after successful WebAuthn authentication.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    ///     Refresh token issued after successful WebAuthn authentication.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    ///     Access token expiration timestamp.
    /// </summary>
    public DateTime? AccessTokenExpiresAt { get; set; }

    /// <summary>
    ///     Refresh token expiration timestamp.
    /// </summary>
    public DateTime? RefreshTokenExpiresAt { get; set; }

    /// <summary>
    ///     Access token lifetime in seconds.
    /// </summary>
    public int ExpiresIn { get; set; }
}

/// <summary>
///     Information about a user's WebAuthn credential.
/// </summary>
public class WebAuthnCredentialInfo
{
    public Guid Id { get; set; }
    public string? FriendlyName { get; set; }
    public WebAuthnAuthenticatorType AuthenticatorType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsPasswordless { get; set; }
    public bool IsDefault { get; set; }
    public bool BackedUp { get; set; }

    /// <summary>
    ///     Lifecycle status of the credential (Active, Deactivated, or Revoked).
    /// </summary>
    public WebAuthnCredentialStatus Status { get; set; } = WebAuthnCredentialStatus.Active;

    /// <summary>
    ///     When the credential was temporarily deactivated, if applicable.
    /// </summary>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>
    ///     When the credential was revoked (terminal), if applicable.
    /// </summary>
    public DateTime? RevokedAt { get; set; }
}

/// <summary>
///     Result of verifying a WebAuthn credential.
/// </summary>
public class WebAuthnCredentialVerifyResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool IsValid { get; set; }
    public bool IsExpired { get; set; }
    public bool IsRevoked { get; set; }

    /// <summary>
    ///     Whether the credential is temporarily deactivated (reversible, unlike revocation).
    /// </summary>
    public bool IsDeactivated { get; set; }

    /// <summary>
    ///     Lifecycle status of the credential (Active, Deactivated, or Revoked).
    /// </summary>
    public WebAuthnCredentialStatus Status { get; set; }

    public DateTime? LastUsedAt { get; set; }
    /// <summary>
    ///     Signature counter for replay attack protection (increases with each use).
    /// </summary>
    public uint SignatureCount { get; set; }
}
