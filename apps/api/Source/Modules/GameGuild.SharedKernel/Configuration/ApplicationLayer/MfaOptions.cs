using System.ComponentModel.DataAnnotations;

namespace GameGuild.Configuration.ApplicationLayer;

/// <summary>
///     Configuration options for Multi-Factor Authentication
/// </summary>
public sealed class MfaOptions : BaseOptions
{
    public const string SectionName = "Mfa";

    /// <summary>
    ///     Maximum number of failed MFA attempts before account lockout
    /// </summary>
    [Range(1, 100)]
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    ///     Duration in minutes for MFA lockout after max failed attempts
    /// </summary>
    [Range(1, 1440)]
    public int LockoutDurationMinutes { get; set; } = 15;

    /// <summary>
    ///     Number of backup codes to generate
    /// </summary>
    [Range(1, 20)]
    public int BackupCodesCount { get; set; } = 10;

    /// <summary>
    ///     Length of each backup code
    /// </summary>
    [Range(6, 16)]
    public int BackupCodeLength { get; set; } = 12;

    /// <summary>
    ///     TOTP time step in seconds (RFC 6238 standard is 30)
    /// </summary>
    [Range(15, 60)]
    public int TotpTimeStepSeconds { get; set; } = 30;

    /// <summary>
    ///     Number of time steps to allow for clock skew (before and after current time)
    /// </summary>
    [Range(0, 5)]
    public int TotpClockSkew { get; set; } = 1;

    /// <summary>
    ///     Duration in minutes that MFA setup session is valid
    /// </summary>
    [Range(1, 60)]
    public int SetupSessionDurationMinutes { get; set; } = 10;

    /// <summary>
    ///     Whether to require MFA for all users (can be overridden per user)
    /// </summary>
    public bool RequireMfaByDefault { get; set; } = false;

    /// <summary>
    ///     Issuer name shown in authenticator apps
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string TotpIssuer { get; set; } = "GameGuild";

    /// <summary>
    ///     Whether MFA is enabled globally
    /// </summary>
    public bool Enabled { get; set; } = true;

    public bool IsValid { get => Validate().IsValid; }

    public new (bool IsValid, string[ ] Errors) Validate()
    {
        var errors = new List<string>();

        if (MaxFailedAttempts < 1 || MaxFailedAttempts > 100)
        {
            errors.Add("MaxFailedAttempts must be between 1 and 100");
        }

        if (LockoutDurationMinutes < 1 || LockoutDurationMinutes > 1440)
        {
            errors.Add("LockoutDurationMinutes must be between 1 and 1440 (24 hours)");
        }

        if (BackupCodesCount < 1 || BackupCodesCount > 20)
        {
            errors.Add("BackupCodesCount must be between 1 and 20");
        }

        if (BackupCodeLength < 6 || BackupCodeLength > 16)
        {
            errors.Add("BackupCodeLength must be between 6 and 16");
        }

        if (TotpTimeStepSeconds < 15 || TotpTimeStepSeconds > 60)
        {
            errors.Add("TotpTimeStepSeconds must be between 15 and 60");
        }

        if (TotpClockSkew < 0 || TotpClockSkew > 5)
        {
            errors.Add("TotpClockSkew must be between 0 and 5");
        }

        if (SetupSessionDurationMinutes < 1 || SetupSessionDurationMinutes > 60)
        {
            errors.Add("SetupSessionDurationMinutes must be between 1 and 60");
        }

        if (string.IsNullOrWhiteSpace(TotpIssuer))
        {
            errors.Add("TotpIssuer is required");
        }

        if (!Enabled && RequireMfaByDefault)
        {
            errors.Add("RequireMfaByDefault cannot be enabled when MFA is disabled");
        }

        return (errors.Count == 0, errors.ToArray());
    }
}
