using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameGuild.Resources;

/// <summary>
///     Resource throttling policy for managing consumption rates
/// </summary>
[Table("ResourceThrottlingPolicies")]
public class ResourceThrottlingPolicy : EntityBase
{
    /// <summary>
    ///     Type of resource being throttled
    /// </summary>
    public ResourceUsageType ResourceType { get; set; }

    /// <summary>
    ///     Throttling strategy to apply
    /// </summary>
    public ThrottlingStrategy Strategy { get; set; } = ThrottlingStrategy.None;

    /// <summary>
    ///     Whether this policy is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Threshold percentage at which throttling begins (e.g., 80 = start at 80% of quota)
    /// </summary>
    public int ThrottlingThresholdPercent { get; set; } = 80;

    /// <summary>
    ///     Alias for ThrottlingThresholdPercent for backward compatibility
    /// </summary>
    [NotMapped]
    public long Threshold { get => ThrottlingThresholdPercent; set => ThrottlingThresholdPercent = (int) value; }

    /// <summary>
    ///     Maximum requests per time window (for rate limiting)
    /// </summary>
    public int? MaxRequestsPerWindow { get; set; }

    /// <summary>
    ///     Time window duration in seconds (for rate limiting)
    /// </summary>
    public int? WindowDurationSeconds { get; set; }

    /// <summary>
    ///     Degradation factor (0.0-1.0) - how much to slow down requests
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal DegradationFactor { get; set; } = 0.5m;

    /// <summary>
    ///     Priority threshold - requests below this priority are throttled first
    /// </summary>
    public int? PriorityThreshold { get; set; }

    /// <summary>
    ///     Additional configuration as JSON
    /// </summary>
    [MaxLength(2000)]
    public string? Configuration { get; set; }

    // Note: TenantId is inherited from EntityBase base class

    /// <summary>
    ///     Calculate delay in milliseconds based on current usage percentage
    /// </summary>
    public int CalculateDelayMs(double usagePercentage)
    {
        if (!IsActive || usagePercentage < ThrottlingThresholdPercent)
        {
            return 0;
        }

        return Strategy switch
        {
            ThrottlingStrategy.None => 0,
            ThrottlingStrategy.HardCutoff => int.MaxValue, // Block completely
            ThrottlingStrategy.GradualDegradation => CalculateGradualDelay(usagePercentage),
            ThrottlingStrategy.RateLimiting => CalculateRateLimitDelay(),
            ThrottlingStrategy.PriorityBased => CalculatePriorityDelay(usagePercentage),
            _ => 0
        };
    }

    private int CalculateGradualDelay(double usagePercentage)
    {
        // Linear increase from 0ms to 5000ms as usage goes from threshold to 100%
        var excessPercentage = usagePercentage - ThrottlingThresholdPercent;
        var maxExcess = 100 - ThrottlingThresholdPercent;
        var delayRatio = excessPercentage / maxExcess;

        return (int) (delayRatio * 5000 * (double) DegradationFactor);
    }

    private int CalculateRateLimitDelay()
    {
        if (!MaxRequestsPerWindow.HasValue || !WindowDurationSeconds.HasValue)
        {
            return 0;
        }

        // Calculate delay to stay within rate limit
        return WindowDurationSeconds.Value * 1000 / MaxRequestsPerWindow.Value;
    }

    private int CalculatePriorityDelay(double usagePercentage)
    {
        // Similar to gradual but can be adjusted by priority
        return CalculateGradualDelay(usagePercentage);
    }

    /// <summary>
    ///     Check if request should be blocked completely
    /// </summary>
    public bool ShouldBlock(double usagePercentage)
    {
        if (!IsActive)
        {
            return false;
        }

        return Strategy == ThrottlingStrategy.HardCutoff && usagePercentage >= ThrottlingThresholdPercent;
    }
}
