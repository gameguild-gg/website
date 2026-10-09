namespace GameGuild.Identity.Authentication;

/// <summary>
///     Thin facade that preserves the original <see cref="IWebAuthnService" /> contract
///     by delegating to focused sub-services.
/// </summary>
public class WebAuthnService(
    IWebAuthnRegistrationService registration,
    IWebAuthnAuthenticationService authentication,
    IWebAuthnCredentialManagementService credentials) : IWebAuthnService
{
    // ── Registration (attestation) ──────────────────────────────────────

    public Task<WebAuthnRegistrationOptionsResult> BeginRegistrationAsync(
        Guid userId,
        string userEmail,
        string displayName,
        WebAuthnAuthenticatorType? preferredType = null,
        CancellationToken cancellationToken = default) =>
        registration.BeginRegistrationAsync(userId, userEmail, displayName, preferredType, cancellationToken);

    public Task<WebAuthnRegistrationResult> CompleteRegistrationAsync(
        Guid userId,
        string attestationResponse,
        string? friendlyName = null,
        bool isPasswordless = false,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default) =>
        registration.CompleteRegistrationAsync(userId, attestationResponse, friendlyName, isPasswordless, ipAddress, userAgent, cancellationToken);

    // ── Authentication (assertion) ──────────────────────────────────────

    public Task<WebAuthnAuthenticationOptionsResult> BeginAuthenticationAsync(
        string? userEmail = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default) =>
        authentication.BeginAuthenticationAsync(userEmail, userId, cancellationToken);

    public Task<WebAuthnAuthenticationResult> CompleteAuthenticationAsync(
        string assertionResponse,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default) =>
        authentication.CompleteAuthenticationAsync(assertionResponse, ipAddress, userAgent, cancellationToken);

    // ── Credential management ───────────────────────────────────────────

    public Task<List<WebAuthnCredentialInfo>> GetUserCredentialsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        credentials.GetUserCredentialsAsync(userId, cancellationToken);

    public Task<WebAuthnCredentialInfo?> GetCredentialByIdAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default) =>
        credentials.GetCredentialByIdAsync(userId, credentialId, cancellationToken);

    public Task<bool> CredentialExistsAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default) =>
        credentials.CredentialExistsAsync(userId, credentialId, cancellationToken);

    public Task<WebAuthnCredentialVerifyResult> VerifyCredentialAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default) =>
        credentials.VerifyCredentialAsync(userId, credentialId, cancellationToken);

    public Task<bool> DeleteCredentialAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default) =>
        credentials.DeleteCredentialAsync(userId, credentialId, cancellationToken);

    public Task<WebAuthnCredentialTransitionResult> DeactivateCredentialAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default) =>
        credentials.DeactivateCredentialAsync(userId, credentialId, cancellationToken);

    public Task<WebAuthnCredentialTransitionResult> ActivateCredentialAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default) =>
        credentials.ActivateCredentialAsync(userId, credentialId, cancellationToken);

    public Task<bool> UpdateCredentialNameAsync(
        Guid userId,
        Guid credentialId,
        string friendlyName,
        CancellationToken cancellationToken = default) =>
        credentials.UpdateCredentialNameAsync(userId, credentialId, friendlyName, cancellationToken);

    public Task<bool> IsWebAuthnEnabledAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        credentials.IsWebAuthnEnabledAsync(userId, cancellationToken);
}
