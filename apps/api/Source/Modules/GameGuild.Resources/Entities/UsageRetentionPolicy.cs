using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameGuild.Resources;

/// <summary>
///     Usage retention policy for managing data lifecycle
/// </summary>
[Table("UsageRetentionPolicies")]
public class UsageRetentionPolicy : EntityBase
{
    /// <summary>
    ///     Policy name
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Optional description
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    ///     Resource type this policy applies to (null means all types)
    /// </summary>
    public ResourceUsageType? ResourceType { get; set; }

    /// <summary>
    ///     Number of days to retain raw usage data
    /// </summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    ///     Number of days after which to archive data to cold storage
    /// </summary>
    public int ArchiveAfterDays { get; set; } = 30;

    /// <summary>
    ///     Whether to enable data compaction/aggregation
    /// </summary>
    public bool EnableCompaction { get; set; } = true;

    /// <summary>
    ///     Compaction interval in days (e.g., compact daily records to weekly after N days)
    /// </summary>
    public int CompactionIntervalDays { get; set; } = 7;

    /// <summary>
    ///     Down-sampling strategy (e.g., "hourly", "daily", "weekly")
    /// </summary>
    [MaxLength(50)]
    public string DownSamplingStrategy { get; set; } = "daily";

    /// <summary>
    ///     Whether this policy is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Last time this policy was executed
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }

    /// <summary>
    ///     Next scheduled execution time
    /// </summary>
    public DateTime? NextExecutionAt { get; set; }

    /// <summary>
    ///     Additional configuration as JSON
    /// </summary>
    [MaxLength(2000)]
    public string? Configuration { get; set; }

    // Note: TenantId is inherited from EntityBase base class (null means applies to all tenants)

    /// <summary>
    ///     Calculate when the next compaction should occur
    /// </summary>
    public DateTime CalculateNextCompaction()
    {
        var lastExecution = LastExecutedAt ?? SystemClock.UtcNow;

        return lastExecution.AddDays(CompactionIntervalDays);
    }

    /// <summary>
    ///     Check if the policy should run now
    /// </summary>
    public bool ShouldExecute()
    {
        if (!IsActive)
        {
            return false;
        }

        if (!NextExecutionAt.HasValue)
        {
            return true;
        }

        return SystemClock.UtcNow >= NextExecutionAt.Value;
    }

    /// <summary>
    ///     Get archive threshold date
    /// </summary>
    public DateTime GetArchiveThresholdDate() { return SystemClock.UtcNow.AddDays(-ArchiveAfterDays); }

    /// <summary>
    ///     Get deletion threshold date
    /// </summary>
    public DateTime GetDeletionThresholdDate() { return SystemClock.UtcNow.AddDays(-RetentionDays); }
}
