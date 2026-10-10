using System.ComponentModel.DataAnnotations;

namespace GameGuild.Configuration.ApplicationLayer;

/// <summary>
///     Configuration options for Authentication Anomaly Detection
/// </summary>
public sealed class AuthenticationAnomalyOptions : BaseOptions
{
    public const string SectionName = "AuthenticationAnomaly";

    /// <summary>
    ///     Whether anomaly detection is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Maximum failed attempts per hour before flagging as suspicious
    /// </summary>
    [Range(1, 50)]
    public int MaxFailedAttemptsPerHour { get; set; } = 5;

    /// <summary>
    ///     Maximum failed attempts per day before flagging as suspicious
    /// </summary>
    [Range(1, 200)]
    public int MaxFailedAttemptsPerDay { get; set; } = 20;

    /// <summary>
    ///     Number of suspicious indicators before flagging as anomaly
    /// </summary>
    [Range(1, 10)]
    public int SuspiciousThreshold { get; set; } = 3;

    /// <summary>
    ///     Throttle duration in minutes after detecting anomaly
    /// </summary>
    [Range(1, 1440)]
    public int ThrottleDurationMinutes { get; set; } = 15;

    /// <summary>
    ///     Maximum authentication attempts per IP per hour
    /// </summary>
    [Range(10, 1000)]
    public int MaxAttemptsPerIpPerHour { get; set; } = 50;

    /// <summary>
    ///     Whether to track location changes
    /// </summary>
    public bool EnableLocationTracking { get; set; } = true;

    /// <summary>
    ///     Whether to flag authentication from new devices
    /// </summary>
    public bool FlagNewDevices { get; set; } = true;

    /// <summary>
    ///     Whether to flag authentication from new locations
    /// </summary>
    public bool FlagNewLocations { get; set; } = true;

    /// <summary>
    ///     Whether to track authentication velocity (rapid attempts)
    /// </summary>
    public bool EnableVelocityChecks { get; set; } = true;

    /// <summary>
    ///     Minimum time in seconds between authentication attempts before flagging
    /// </summary>
    [Range(1, 300)]
    public int MinTimeBetweenAttemptsSeconds { get; set; } = 5;

    /// <summary>
    ///     Whether to enable behavioral analysis
    /// </summary>
    public bool EnableBehavioralAnalysis { get; set; } = true;

    public bool IsValid { get => Validate().IsValid; }

    public new (bool IsValid, string[ ] Errors) Validate()
    {
        var errors = new List<string>();

        if (MaxFailedAttemptsPerHour < 1 || MaxFailedAttemptsPerHour > 50)
        {
            errors.Add("MaxFailedAttemptsPerHour must be between 1 and 50");
        }

        if (MaxFailedAttemptsPerDay < 1 || MaxFailedAttemptsPerDay > 200)
        {
            errors.Add("MaxFailedAttemptsPerDay must be between 1 and 200");
        }

        if (SuspiciousThreshold < 1 || SuspiciousThreshold > 10)
        {
            errors.Add("SuspiciousThreshold must be between 1 and 10");
        }

        if (ThrottleDurationMinutes < 1 || ThrottleDurationMinutes > 1440)
        {
            errors.Add("ThrottleDurationMinutes must be between 1 and 1440");
        }

        if (MaxAttemptsPerIpPerHour < 10 || MaxAttemptsPerIpPerHour > 1000)
        {
            errors.Add("MaxAttemptsPerIpPerHour must be between 10 and 1000");
        }

        if (EnableVelocityChecks && (MinTimeBetweenAttemptsSeconds < 1 || MinTimeBetweenAttemptsSeconds > 300))
        {
            errors.Add("MinTimeBetweenAttemptsSeconds must be between 1 and 300");
        }

        if (MaxFailedAttemptsPerHour > MaxFailedAttemptsPerDay)
        {
            errors.Add("MaxFailedAttemptsPerHour cannot exceed MaxFailedAttemptsPerDay");
        }

        return (errors.Count == 0, errors.ToArray());
    }
}
