using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Represents a user's MFA (Multi-Factor Authentication) configuration
/// </summary>
public class UserMfaConfiguration
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    ///     Indicates if MFA is enabled for this user
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    ///     TOTP secret key (encrypted)
    /// </summary>
    [MaxLength(500)]
    public string? TotpSecretKey { get; set; }

    /// <summary>
    ///     Versioned issued-set metadata and salted hashes; legacy SHA-256 lists remain readable
    /// </summary>
    [MaxLength(2000)]
    public string? BackupCodes { get; set; }

    /// <summary>
    ///     When MFA was first enabled
    /// </summary>
    public DateTime? EnabledAt { get; set; }

    /// <summary>
    ///     Last time MFA was used successfully
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    ///     Number of failed MFA attempts (reset on success)
    /// </summary>
    public int FailedAttempts { get; set; }

    /// <summary>
    ///     When the user was locked out due to failed attempts
    /// </summary>
    public DateTime? LockedOutUntil { get; set; }

    /// <summary>
    ///     Preferred MFA method
    /// </summary>
    public MfaMethod PreferredMethod { get; set; } = MfaMethod.Totp;

    /// <summary>
    ///     QR code data for TOTP setup (temporary, cleared after setup)
    /// </summary>
    [MaxLength(1000)]
    public string? QrCodeSetupData { get; set; }

    /// <summary>
    ///     Indicates if the user has completed MFA setup
    /// </summary>
    public bool IsSetupComplete { get; set; }

    /// <summary>Fixed deadline for a new enrollment; failed attempts cannot extend it.</summary>
    public DateTime? SetupExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
