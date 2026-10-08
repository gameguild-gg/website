using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Manages WebAuthn/FIDO2 credential CRUD operations and status queries.
/// </summary>
public class WebAuthnCredentialManagementService(
    IWebAuthnCredentialRepository credentialRepository,
    ILogger<WebAuthnCredentialManagementService> logger) : IWebAuthnCredentialManagementService
{
    public async Task<List<WebAuthnCredentialInfo>> GetUserCredentialsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var credentials = await credentialRepository
            .GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        return credentials
            .Where(c => c.IsActive)
            .Select(MapToInfo)
            .ToList();
    }

    public async Task<WebAuthnCredentialInfo?> GetCredentialByIdAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        var credential = await credentialRepository
            .GetByIdAsync(credentialId, cancellationToken).ConfigureAwait(false);

        if (credential == null || credential.UserId != userId)
            return null;

        return MapToInfo(credential);
    }

    public async Task<bool> CredentialExistsAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        var credential = await credentialRepository
            .GetByIdAsync(credentialId, cancellationToken).ConfigureAwait(false);
        return credential != null && credential.UserId == userId;
    }

    public async Task<WebAuthnCredentialVerifyResult> VerifyCredentialAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var credential = await credentialRepository
                .GetByIdAsync(credentialId, cancellationToken).ConfigureAwait(false);

            if (credential == null || credential.UserId != userId)
            {
                return new WebAuthnCredentialVerifyResult
                {
                    Success = false,
                    Error = "Credential not found",
                    IsValid = false
                };
            }

            // Check if revoked (terminal)
            if (credential.IsRevoked)
            {
                return new WebAuthnCredentialVerifyResult
                {
                    Success = true,
                    IsValid = false,
                    IsRevoked = true,
                    IsDeactivated = false,
                    Status = WebAuthnCredentialStatus.Revoked,
                    LastUsedAt = credential.LastUsedAt,
                    SignatureCount = credential.SignatureCounter
                };
            }

            // Check if temporarily deactivated (reversible)
            if (credential.IsDeactivated)
            {
                return new WebAuthnCredentialVerifyResult
                {
                    Success = true,
                    IsValid = false,
                    IsRevoked = false,
                    IsDeactivated = true,
                    Status = WebAuthnCredentialStatus.Deactivated,
                    LastUsedAt = credential.LastUsedAt,
                    SignatureCount = credential.SignatureCounter
                };
            }

            return new WebAuthnCredentialVerifyResult
            {
                Success = true,
                IsValid = credential.IsActive,
                IsRevoked = false,
                IsDeactivated = false,
                Status = credential.Status,
                LastUsedAt = credential.LastUsedAt,
                SignatureCount = credential.SignatureCounter
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error verifying credential {CredentialId}", credentialId);
            return new WebAuthnCredentialVerifyResult
            {
                Success = false,
                Error = "Verification failed",
                IsValid = false
            };
        }
    }

    public async Task<bool> DeleteCredentialAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        var credential = await credentialRepository
            .GetByIdAsync(credentialId, cancellationToken).ConfigureAwait(false);
        if (credential == null || credential.UserId != userId)
            return false;

        return await credentialRepository
            .RevokeAsync(credentialId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WebAuthnCredentialTransitionResult> DeactivateCredentialAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        var credential = await credentialRepository
            .GetByIdAsync(credentialId, cancellationToken).ConfigureAwait(false);

        if (credential == null || credential.UserId != userId)
        {
            return NotFoundTransition();
        }

        if (credential.Status == WebAuthnCredentialStatus.Revoked)
        {
            return InvalidTransition(
                WebAuthnCredentialStatus.Revoked,
                "A revoked credential cannot be deactivated; revocation is terminal.");
        }

        if (credential.Status == WebAuthnCredentialStatus.Deactivated)
        {
            return InvalidTransition(
                WebAuthnCredentialStatus.Deactivated,
                "The credential is already deactivated.");
        }

        var applied = await credentialRepository
            .DeactivateAsync(credentialId, cancellationToken).ConfigureAwait(false);

        return new WebAuthnCredentialTransitionResult
        {
            Success = applied,
            Error = applied ? null : "InvalidTransition",
            ErrorDescription = applied ? null : "The credential is no longer active.",
            Status = WebAuthnCredentialStatus.Deactivated
        };
    }

    public async Task<WebAuthnCredentialTransitionResult> ActivateCredentialAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        var credential = await credentialRepository
            .GetByIdAsync(credentialId, cancellationToken).ConfigureAwait(false);

        if (credential == null || credential.UserId != userId)
        {
            return NotFoundTransition();
        }

        if (credential.Status == WebAuthnCredentialStatus.Revoked)
        {
            return InvalidTransition(
                WebAuthnCredentialStatus.Revoked,
                "A revoked credential cannot be reactivated; revocation is terminal.");
        }

        if (credential.Status == WebAuthnCredentialStatus.Active)
        {
            return InvalidTransition(
                WebAuthnCredentialStatus.Active,
                "The credential is already active.");
        }

        var applied = await credentialRepository
            .ReactivateAsync(credentialId, cancellationToken).ConfigureAwait(false);

        return new WebAuthnCredentialTransitionResult
        {
            Success = applied,
            Error = applied ? null : "InvalidTransition",
            ErrorDescription = applied ? null : "The credential is no longer deactivated.",
            Status = WebAuthnCredentialStatus.Active
        };
    }

    private static WebAuthnCredentialTransitionResult NotFoundTransition() => new()
    {
        Success = false,
        Error = "CredentialNotFound",
        ErrorDescription = "Credential not found",
        Status = null
    };

    private static WebAuthnCredentialTransitionResult InvalidTransition(
        WebAuthnCredentialStatus status,
        string description) => new()
    {
        Success = false,
        Error = "InvalidTransition",
        ErrorDescription = description,
        Status = status
    };

    public async Task<bool> UpdateCredentialNameAsync(
        Guid userId,
        Guid credentialId,
        string friendlyName,
        CancellationToken cancellationToken = default)
    {
        var credential = await credentialRepository
            .GetByIdAsync(credentialId, cancellationToken).ConfigureAwait(false);
        if (credential == null || credential.UserId != userId)
            return false;

        credential.FriendlyName = friendlyName;
        await credentialRepository
            .UpdateAsync(credential, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> IsWebAuthnEnabledAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await credentialRepository
            .HasActiveCredentialsAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    private static WebAuthnCredentialInfo MapToInfo(UserWebAuthnCredential c) => new()
    {
        Id = c.Id,
        FriendlyName = c.FriendlyName,
        AuthenticatorType = c.AuthenticatorType,
        CreatedAt = c.CreatedAt,
        LastUsedAt = c.LastUsedAt,
        IsPasswordless = c.IsPasswordless,
        IsDefault = c.IsDefault,
        BackedUp = c.BackedUp,
        Status = c.Status,
        DeactivatedAt = c.DeactivatedAt,
        RevokedAt = c.RevokedAt
    };
}
