using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Represents a WebAuthn/FIDO2 credential registered by a user for passwordless authentication.
/// </summary>
public class UserWebAuthnCredential
{
    /// <summary>
    ///     Unique identifier for this credential record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    ///     The user who owns this credential.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    ///     Credential ID assigned by the authenticator (base64-encoded).
    ///     This is the unique identifier the authenticator uses for the credential.
    /// </summary>
    [Required]
    [MaxLength(1024)]
    public string CredentialId { get; set; } = string.Empty;

    /// <summary>
    ///     Public key of the credential (COSE-encoded, base64).
    ///     Used to verify signatures during authentication.
    /// </summary>
    [Required]
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>
    ///     The AAGUID of the authenticator.
    ///     Identifies the make and model of the authenticator.
    /// </summary>
    [MaxLength(36)]
    public string? AaGuid { get; set; }

    /// <summary>
    ///     Signature counter for replay attack protection.
    ///     Must increase with each authentication.
    /// </summary>
    public uint SignatureCounter { get; set; }

    /// <summary>
    ///     User-friendly name for this credential (e.g., "MacBook Touch ID", "YubiKey 5").
    /// </summary>
    [MaxLength(100)]
    public string? FriendlyName { get; set; }

    /// <summary>
    ///     Type of credential (e.g., "public-key").
    /// </summary>
    [MaxLength(50)]
    public string CredentialType { get; set; } = "public-key";

    /// <summary>
    ///     Type of authenticator attachment (platform, cross-platform).
    /// </summary>
    public WebAuthnAuthenticatorType AuthenticatorType { get; set; }

    /// <summary>
    ///     Transports the credential supports (usb, nfc, ble, internal).
    /// </summary>
    [MaxLength(200)]
    public string? Transports { get; set; }

    /// <summary>
    ///     Whether this credential is used for passwordless authentication
    ///     (as primary factor) vs MFA (as second factor).
    /// </summary>
    public bool IsPasswordless { get; set; }

    /// <summary>
    ///     Whether this credential is the default for the user.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    ///     Whether user verification was performed during registration.
    /// </summary>
    public bool UserVerified { get; set; }

    /// <summary>
    ///     Whether the credential is backed up (synced across devices).
    /// </summary>
    public bool BackedUp { get; set; }

    /// <summary>
    ///     When the credential was registered.
    /// </summary>
    public DateTime CreatedAt { get; set; } = SystemClock.UtcNow;

    /// <summary>
    ///     When the credential was last used for authentication.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    ///     IP address where the credential was registered.
    /// </summary>
    [MaxLength(45)]
    public string? RegisteredFromIp { get; set; }

    /// <summary>
    ///     User agent of the browser used during registration.
    /// </summary>
    [MaxLength(500)]
    public string? RegisteredUserAgent { get; set; }

    /// <summary>
    ///     Whether this credential is still active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     When the credential was temporarily deactivated (if applicable).
    ///     Deactivation is reversible via <see cref="Activate" /> and is distinct from
    ///     the terminal <see cref="RevokedAt" /> revocation.
    /// </summary>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>
    ///     When the credential was revoked (if applicable). Revocation is terminal:
    ///     a revoked credential can never be reactivated.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    ///     Whether this credential is currently deactivated (temporarily disabled).
    /// </summary>
    public bool IsDeactivated => !IsActive && DeactivatedAt.HasValue && !RevokedAt.HasValue;

    /// <summary>
    ///     Whether this credential has been revoked (terminal state).
    /// </summary>
    public bool IsRevoked => RevokedAt.HasValue;

    /// <summary>
    ///     Current lifecycle status. A revoked credential always reports
    ///     <see cref="WebAuthnCredentialStatus.Revoked" /> regardless of any earlier deactivation.
    /// </summary>
    public WebAuthnCredentialStatus Status =>
        IsRevoked ? WebAuthnCredentialStatus.Revoked
        : !IsActive ? WebAuthnCredentialStatus.Deactivated
        : WebAuthnCredentialStatus.Active;

    /// <summary>
    ///     Temporarily deactivates this credential so it can no longer be used for
    ///     authentication ceremonies. Reversible via <see cref="Activate" />.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the credential is already deactivated or revoked.
    /// </exception>
    public void Deactivate()
    {
        if (IsRevoked)
        {
            throw new InvalidOperationException("A revoked credential cannot be deactivated; revocation is terminal.");
        }

        if (IsDeactivated)
        {
            throw new InvalidOperationException("The credential is already deactivated.");
        }

        DeactivatedAt = SystemClock.UtcNow;
        IsActive = false;
    }

    /// <summary>
    ///     Reverses a temporary deactivation, returning the credential to active use.
    ///     Never reactivates a revoked credential: revocation is terminal.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the credential is revoked or not deactivated.
    /// </exception>
    public void Activate()
    {
        if (IsRevoked)
        {
            throw new InvalidOperationException("A revoked credential cannot be reactivated; revocation is terminal.");
        }

        if (IsActive)
        {
            throw new InvalidOperationException("The credential is already active.");
        }

        DeactivatedAt = null;
        IsActive = true;
    }

    /// <summary>
    ///     Revokes this credential permanently. Revocation is terminal and cannot be
    ///     undone by deactivation, activation, or restore operations.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the credential is already revoked.</exception>
    public void Revoke()
    {
        if (IsRevoked)
        {
            throw new InvalidOperationException("The credential is already revoked.");
        }

        RevokedAt = SystemClock.UtcNow;
        IsActive = false;
    }
}

/// <summary>
///     Type of WebAuthn authenticator.
/// </summary>
public enum WebAuthnAuthenticatorType
{
    /// <summary>
    ///     Platform authenticator (built into device, e.g., Touch ID, Windows Hello).
    /// </summary>
    Platform = 1,

    /// <summary>
    ///     Cross-platform authenticator (external, e.g., YubiKey, security key).
    /// </summary>
    CrossPlatform = 2
}
