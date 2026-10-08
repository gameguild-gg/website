using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Options for the durable security event pipeline: database write retries before local
///     spooling, the spool directory, alert thresholds, and retention enforcement cadence.
/// </summary>
public sealed class SecurityEventPipelineOptions
{
    public const string ConfigurationSection = "Audit:SecurityEventPipeline";

    /// <summary>Attempts to persist a security event to the database before spooling locally.</summary>
    [Range(1, 10)] public int DatabaseWriteAttempts { get; set; } = 3;

    /// <summary>Base delay between database write attempts; scaled linearly by attempt number.</summary>
    [Range(0, 10000)] public int RetryDelayMilliseconds { get; set; } = 50;

    /// <summary>When false, a failed database capture is only logged; spooling is disabled.</summary>
    public bool SpoolingEnabled { get; set; } = true;

    /// <summary>Directory holding the durable local spool for security events that could not be persisted.</summary>
    public string SpoolDirectoryPath { get; set; } = Path.Combine("artifacts", "security-event-spool");

    /// <summary>Interval at which the drainer replays spooled security events.</summary>
    public TimeSpan DrainInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Failed authentication events for one subject (user or IP) before an alert is raised.</summary>
    [Range(2, 1000)] public int FailedAuthenticationAlertThreshold { get; set; } = 5;

    /// <summary>Window (minutes) in which failed authentication events are counted toward the threshold.</summary>
    [Range(1, 1440)] public int FailedAuthenticationWindowMinutes { get; set; } = 15;

    /// <summary>Interval at which the retention enforcement service applies tenant retention policies.</summary>
    public TimeSpan RetentionEnforcementInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Default retention (days) proposed for new tenant policies; 400 days is a conservative security-logs default.</summary>
    [Range(30, 3650)] public int DefaultRetentionDays { get; set; } = 400;

    /// <summary>Maximum number of audit rows deleted in a single enforcement batch.</summary>
    [Range(1, 100000)] public int RetentionBatchSize { get; set; } = 5000;
}

public static class SecurityEventPipelineOptionsConfiguration
{
    public static SecurityEventPipelineOptions BindFrom(IConfiguration configuration)
    {
        var section = configuration.GetSection(SecurityEventPipelineOptions.ConfigurationSection);
        var options = new SecurityEventPipelineOptions();

        if (int.TryParse(section[nameof(SecurityEventPipelineOptions.DatabaseWriteAttempts)], out var attempts))
        {
            options.DatabaseWriteAttempts = Math.Clamp(attempts, 1, 10);
        }

        if (int.TryParse(section[nameof(SecurityEventPipelineOptions.RetryDelayMilliseconds)], out var retryDelay))
        {
            options.RetryDelayMilliseconds = Math.Clamp(retryDelay, 0, 10000);
        }

        if (bool.TryParse(section[nameof(SecurityEventPipelineOptions.SpoolingEnabled)], out var spoolingEnabled))
        {
            options.SpoolingEnabled = spoolingEnabled;
        }

        var spoolPath = section[nameof(SecurityEventPipelineOptions.SpoolDirectoryPath)];
        if (!string.IsNullOrWhiteSpace(spoolPath))
        {
            options.SpoolDirectoryPath = spoolPath;
        }

        if (int.TryParse(section["DrainIntervalMinutes"], out var drainMinutes))
        {
            options.DrainInterval = TimeSpan.FromMinutes(Math.Clamp(drainMinutes, 1, 1440));
        }

        if (int.TryParse(section[nameof(SecurityEventPipelineOptions.FailedAuthenticationAlertThreshold)], out var threshold))
        {
            options.FailedAuthenticationAlertThreshold = Math.Clamp(threshold, 2, 1000);
        }

        if (int.TryParse(section[nameof(SecurityEventPipelineOptions.FailedAuthenticationWindowMinutes)], out var windowMinutes))
        {
            options.FailedAuthenticationWindowMinutes = Math.Clamp(windowMinutes, 1, 1440);
        }

        if (int.TryParse(section["RetentionEnforcementIntervalHours"], out var enforcementHours))
        {
            options.RetentionEnforcementInterval = TimeSpan.FromHours(Math.Clamp(enforcementHours, 1, 168));
        }

        if (int.TryParse(section[nameof(SecurityEventPipelineOptions.DefaultRetentionDays)], out var retentionDays))
        {
            options.DefaultRetentionDays = Math.Clamp(retentionDays, 30, 3650);
        }

        if (int.TryParse(section[nameof(SecurityEventPipelineOptions.RetentionBatchSize)], out var batchSize))
        {
            options.RetentionBatchSize = Math.Clamp(batchSize, 1, 100000);
        }

        return options;
    }
}
