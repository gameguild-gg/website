using System.ComponentModel.DataAnnotations;

namespace GameGuild.Configuration.ApplicationLayer;

/// <summary>
///     Configuration options for Session Management
/// </summary>
public sealed class SessionOptions : BaseOptions
{
    public const string SectionName = "Session";

    /// <summary>
    ///     Session idle timeout in minutes
    /// </summary>
    [Range(1, 10080)]
    public int IdleTimeoutMinutes { get; set; } = 30;

    /// <summary>
    ///     Absolute session timeout in minutes (regardless of activity)
    /// </summary>
    [Range(1, 43200)]
    public int AbsoluteTimeoutMinutes { get; set; } = 1440; // 24 hours

    /// <summary>
    ///     Maximum number of concurrent sessions per user
    /// </summary>
    [Range(1, 100)]
    public int MaxConcurrentSessions { get; set; } = 5;

    /// <summary>
    ///     Duration in days that trusted devices remain trusted
    /// </summary>
    [Range(1, 365)]
    public int TrustedDeviceDurationDays { get; set; } = 30;

    /// <summary>
    ///     Maximum number of trusted devices per user
    /// </summary>
    [Range(1, 50)]
    public int MaxTrustedDevices { get; set; } = 10;

    /// <summary>
    ///     Whether to automatically terminate sessions on password change
    /// </summary>
    public bool TerminateSessionsOnPasswordChange { get; set; } = true;

    /// <summary>
    ///     Whether to automatically terminate sessions on MFA disable
    /// </summary>
    public bool TerminateSessionsOnMfaDisable { get; set; } = true;

    /// <summary>
    ///     Whether to track device fingerprints
    /// </summary>
    public bool EnableDeviceFingerprinting { get; set; } = true;

    /// <summary>
    ///     Whether to track location information
    /// </summary>
    public bool EnableLocationTracking { get; set; } = true;

    /// <summary>
    ///     Whether to require device trust for sensitive operations
    /// </summary>
    public bool RequireTrustedDeviceForSensitiveOps { get; set; } = false;

    public bool IsValid
    {
        get => Validate().IsValid;
    }

    public new (bool IsValid, string[] Errors) Validate()
    {
        var errors = new List<string>();

        if (IdleTimeoutMinutes < 1 || IdleTimeoutMinutes > 10080)
        {
            errors.Add("IdleTimeoutMinutes must be between 1 and 10080 (7 days)");
        }

        if (AbsoluteTimeoutMinutes < 1 || AbsoluteTimeoutMinutes > 43200)
        {
            errors.Add("AbsoluteTimeoutMinutes must be between 1 and 43200 (30 days)");
        }

        if (MaxConcurrentSessions < 1 || MaxConcurrentSessions > 100)
        {
            errors.Add("MaxConcurrentSessions must be between 1 and 100");
        }

        if (TrustedDeviceDurationDays < 1 || TrustedDeviceDurationDays > 365)
        {
            errors.Add("TrustedDeviceDurationDays must be between 1 and 365");
        }

        if (MaxTrustedDevices < 1 || MaxTrustedDevices > 50)
        {
            errors.Add("MaxTrustedDevices must be between 1 and 50");
        }

        if (IdleTimeoutMinutes > AbsoluteTimeoutMinutes)
        {
            errors.Add("IdleTimeoutMinutes cannot be greater than AbsoluteTimeoutMinutes");
        }

        return (errors.Count == 0, errors.ToArray());
    }
}