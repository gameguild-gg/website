using System.ComponentModel.DataAnnotations;

namespace GameGuild.Configuration.ApplicationLayer;

/// <summary>
///     Configuration options for Authentication Security features
/// </summary>
public sealed class AuthenticationSecurityOptions : BaseOptions
{
    public const string SectionName = "AuthenticationSecurity";

    /// <summary>
    ///     Maximum failed authentication attempts per hour per identifier (email/username)
    /// </summary>
    [Range(1, 100)]
    public int MaxFailedAttemptsPerHour { get; set; } = 5;

    /// <summary>
    ///     Maximum failed authentication attempts per day per identifier
    /// </summary>
    [Range(1, 500)]
    public int MaxFailedAttemptsPerDay { get; set; } = 20;

    /// <summary>
    ///     Maximum authentication attempts per IP address per hour
    /// </summary>
    [Range(1, 1000)]
    public int MaxAttemptsPerIpPerHour { get; set; } = 50;

    /// <summary>
    ///     Account lockout duration in minutes after exceeding max failed attempts
    /// </summary>
    [Range(1, 1440)]
    public int AccountLockoutDurationMinutes { get; set; } = 30;

    /// <summary>
    ///     Whether to enable IP-based throttling
    /// </summary>
    public bool EnableIpThrottling { get; set; } = true;

    /// <summary>
    ///     Whether to enable user enumeration protection (consistent timing and messages)
    /// </summary>
    public bool EnableUserEnumerationProtection { get; set; } = true;

    /// <summary>
    ///     Whether to enable anomaly detection (suspicious patterns, velocity checks)
    /// </summary>
    public bool EnableAnomalyDetection { get; set; } = true;

    /// <summary>
    ///     Whether to require email verification for new accounts
    /// </summary>
    public bool RequireEmailVerification { get; set; } = true;

    /// <summary>
    ///     Duration in hours that email verification tokens are valid
    /// </summary>
    [Range(1, 168)]
    public int EmailVerificationTokenValidityHours { get; set; } = 24;

    /// <summary>
    ///     Duration in hours that password reset tokens are valid
    /// </summary>
    [Range(1, 24)]
    public int PasswordResetTokenValidityHours { get; set; } = 1;

    /// <summary>
    ///     Whether to enable CAPTCHA for suspicious authentication attempts
    /// </summary>
    public bool EnableCaptchaOnSuspiciousActivity { get; set; } = false;

    /// <summary>
    ///     Number of suspicious indicators before flagging as high risk
    /// </summary>
    [Range(1, 10)]
    public int SuspiciousThreshold { get; set; } = 3;

    public bool IsValid
    {
        get => Validate().IsValid;
    }

    public new (bool IsValid, string[] Errors) Validate()
    {
        var errors = new List<string>();

        if (MaxFailedAttemptsPerHour < 1 || MaxFailedAttemptsPerHour > 100)
        {
            errors.Add("MaxFailedAttemptsPerHour must be between 1 and 100");
        }

        if (MaxFailedAttemptsPerDay < 1 || MaxFailedAttemptsPerDay > 500)
        {
            errors.Add("MaxFailedAttemptsPerDay must be between 1 and 500");
        }

        if (MaxAttemptsPerIpPerHour < 1 || MaxAttemptsPerIpPerHour > 1000)
        {
            errors.Add("MaxAttemptsPerIpPerHour must be between 1 and 1000");
        }

        if (AccountLockoutDurationMinutes < 1 || AccountLockoutDurationMinutes > 1440)
        {
            errors.Add("AccountLockoutDurationMinutes must be between 1 and 1440 (24 hours)");
        }

        if (EmailVerificationTokenValidityHours < 1 || EmailVerificationTokenValidityHours > 168)
        {
            errors.Add("EmailVerificationTokenValidityHours must be between 1 and 168 (7 days)");
        }

        if (PasswordResetTokenValidityHours < 1 || PasswordResetTokenValidityHours > 24)
        {
            errors.Add("PasswordResetTokenValidityHours must be between 1 and 24");
        }

        if (SuspiciousThreshold < 1 || SuspiciousThreshold > 10)
        {
            errors.Add("SuspiciousThreshold must be between 1 and 10");
        }

        if (MaxFailedAttemptsPerHour > MaxFailedAttemptsPerDay)
        {
            errors.Add("MaxFailedAttemptsPerHour cannot exceed MaxFailedAttemptsPerDay");
        }

        return (errors.Count == 0, errors.ToArray());
    }
}