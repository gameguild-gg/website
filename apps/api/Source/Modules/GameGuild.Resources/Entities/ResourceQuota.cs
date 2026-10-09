using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameGuild.Resources;

/// <summary>
///     Defines resource usage quotas for tenants and users
/// </summary>
[Table("ResourceQuotas")]
public class ResourceQuota : EntityBase
{
    /// <summary>
    ///     Type of resource being limited
    /// </summary>
    public ResourceUsageType Type { get; set; }

    /// <summary>
    ///     User ID for user-level quotas (null for tenant-level quotas)
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    ///     Soft limit amount (warning threshold)
    /// </summary>
    public long? SoftLimit { get; set; }

    /// <summary>
    ///     Hard limit amount (enforcement threshold)
    /// </summary>
    public long? HardLimit { get; set; }

    /// <summary>
    ///     Current usage count
    /// </summary>
    public long CurrentUsage { get; set; }

    /// <summary>
    ///     Whether this quota is actively enforced
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Period type for quota reset (Monthly, Daily, etc.)
    /// </summary>
    public ResourceQuotaPeriod Period { get; set; } = ResourceQuotaPeriod.Monthly;

    /// <summary>
    ///     Last time the quota was reset
    /// </summary>
    public DateTime? LastReset { get; set; }

    /// <summary>
    ///     Optional time of day for quota resets (for daily/weekly periods)
    /// </summary>
    public TimeSpan? ResetTime { get; set; }

    /// <summary>
    ///     Day of the week for weekly resets (0 = Sunday, 1 = Monday, etc.)
    /// </summary>
    public int? ResetDayOfWeek { get; set; }

    /// <summary>
    ///     Day of the month for monthly resets (1-31)
    /// </summary>
    public int? ResetDayOfMonth { get; set; }

    /// <summary>
    ///     Whether to send notifications when limits are approached
    /// </summary>
    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>
    ///     Percentage thresholds for notifications (e.g., "75,90,100")
    /// </summary>
    [MaxLength(100)]
    public string? NotificationThresholds { get; set; } = "75,90,100";

    /// <summary>
    ///     Optional description or notes about this quota
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    ///     Additional metadata about the quota.
    ///     Stored as JSON in the database but strongly-typed in code.
    /// </summary>
    public ResourceQuotaMetadata? Metadata { get; set; }

    /// <summary>
    ///     Row version for optimistic concurrency control
    /// </summary>
    [Timestamp]
    public byte[ ]? RowVersion { get; set; }

    // Note: TenantId is inherited from EntityBase base class

    /// <summary>
    ///     Calculates the percentage of quota used based on hard limit
    /// </summary>
    public double GetUsagePercentage()
    {
        if (!HardLimit.HasValue || HardLimit.Value == 0)
        {
            return 0;
        }

        return (double) CurrentUsage / HardLimit.Value * 100;
    }

    /// <summary>
    ///     Checks if the current usage exceeds the soft limit
    /// </summary>
    public bool IsSoftLimitExceeded() { return SoftLimit.HasValue && CurrentUsage > SoftLimit.Value; }

    /// <summary>
    ///     Checks if the current usage exceeds the hard limit
    /// </summary>
    public bool IsHardLimitExceeded() { return HardLimit.HasValue && CurrentUsage >= HardLimit.Value; }

    /// <summary>
    ///     Gets remaining quota based on hard limit
    /// </summary>
    public long GetRemainingQuota()
    {
        if (!HardLimit.HasValue)
        {
            return long.MaxValue;
        }

        return Math.Max(0, HardLimit.Value - CurrentUsage);
    }

    /// <summary>
    ///     Checks if the quota needs to be reset based on the period
    /// </summary>
    public bool ShouldReset()
    {
        if (!LastReset.HasValue)
        {
            return true;
        }

        var nextReset = GetNextResetTime();

        if (!nextReset.HasValue)
        {
            return false;
        }

        return SystemClock.UtcNow >= nextReset.Value;
    }

    /// <summary>
    ///     Gets the next reset time based on period and reset time
    /// </summary>
    public DateTime? GetNextResetTime()
    {
        if (!LastReset.HasValue)
        {
            return null;
        }

        var baseDate = LastReset.Value;
        var resetDateTime = baseDate;

        // Apply reset time if specified
        if (ResetTime.HasValue)
        {
            resetDateTime = baseDate.Date.Add(ResetTime.Value);

            // If the reset time for today has already passed, use tomorrow
            if (resetDateTime <= baseDate) { resetDateTime = resetDateTime.AddDays(1); }
        }

        return Period switch
        {
            ResourceQuotaPeriod.Daily => resetDateTime.AddDays(1),
            ResourceQuotaPeriod.Weekly => resetDateTime.AddDays(7),
            ResourceQuotaPeriod.Monthly => resetDateTime.AddMonths(1),
            ResourceQuotaPeriod.Quarterly => resetDateTime.AddMonths(3),
            ResourceQuotaPeriod.Yearly => resetDateTime.AddYears(1),
            ResourceQuotaPeriod.Unlimited => null,
            _ => resetDateTime.AddMonths(1)
        };
    }

    /// <summary>
    ///     Resets the quota usage and calculates next reset date
    /// </summary>
    public void ResetUsage()
    {
        CurrentUsage = 0;
        LastReset = SystemClock.UtcNow;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Resets the quota (alias for ResetUsage for backward compatibility)
    /// </summary>
    public void Reset() { ResetUsage(); }

    /// <summary>
    ///     Adds usage to the current quota
    /// </summary>
    public void AddUsage(long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentException("Usage amount cannot be negative", nameof(amount));
        }

        CurrentUsage += amount;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Removes usage from the current quota (for adjustments)
    /// </summary>
    public void RemoveUsage(long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentException("Usage amount cannot be negative", nameof(amount));
        }

        CurrentUsage = Math.Max(0, CurrentUsage - amount);
        UpdatedAt = SystemClock.UtcNow;
    }
}
