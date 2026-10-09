using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameGuild.Resources;

/// <summary>
///     Usage trend analysis for detecting patterns and anomalies
/// </summary>
[Table("ResourceUsageTrends")]
public class ResourceUsageTrend : EntityBase
{
    /// <summary>
    ///     Type of resource being analyzed
    /// </summary>
    public ResourceUsageType ResourceType { get; set; }

    /// <summary>
    ///     Alias for ResourceType for backward compatibility
    /// </summary>
    [NotMapped]
    public ResourceUsageType Type { get => ResourceType; set => ResourceType = value; }

    /// <summary>
    ///     Start of the analysis period
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    ///     End of the analysis period
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    ///     Average usage during the period
    /// </summary>
    public double AverageUsage { get; set; }

    /// <summary>
    ///     Minimum usage during the period
    /// </summary>
    public long MinUsage { get; set; }

    /// <summary>
    ///     Maximum usage during the period
    /// </summary>
    public long MaxUsage { get; set; }

    /// <summary>
    ///     Standard deviation of usage
    /// </summary>
    public double StandardDeviation { get; set; }

    /// <summary>
    ///     Usage growth rate compared to previous period (percentage)
    /// </summary>
    public double GrowthRate { get; set; }

    /// <summary>
    ///     Number of anomalies detected in this period
    /// </summary>
    public int AnomalyCount { get; set; }

    /// <summary>
    ///     Peak usage timestamp
    /// </summary>
    public DateTime? PeakUsageTime { get; set; }

    /// <summary>
    ///     Pattern detected (e.g., "Steady", "Growing", "Declining", "Volatile", "Anomalous")
    /// </summary>
    [MaxLength(50)]
    public string Pattern { get; set; } = "Steady";

    /// <summary>
    ///     Confidence score for pattern detection (0.0-1.0)
    /// </summary>
    public double PatternConfidence { get; set; } = 1.0;

    /// <summary>
    ///     Additional trend metadata as JSON
    /// </summary>
    [MaxLength(2000)]
    public string? Metadata { get; set; }

    // Note: TenantId is inherited from EntityBase base class

    /// <summary>
    ///     Detect if current usage is an anomaly based on historical trends
    /// </summary>
    public bool IsAnomaly(long currentUsage, double anomalyThreshold = 2.0)
    {
        // Using z-score method: if value is more than N standard deviations from mean
        if (StandardDeviation == 0)
        {
            return false;
        }

        var zScore = Math.Abs((currentUsage - AverageUsage) / StandardDeviation);

        return zScore > anomalyThreshold;
    }

    /// <summary>
    ///     Calculate forecasted usage for next period
    /// </summary>
    public double ForecastNextPeriod()
    {
        // Simple linear forecast based on growth rate
        return AverageUsage * (1 + GrowthRate / 100);
    }

    /// <summary>
    ///     Get trend direction as string
    /// </summary>
    public string GetTrendDirection()
    {
        return GrowthRate switch
        {
            > 5 => "Growing",
            < -5 => "Declining",
            _ => "Steady"
        };
    }

    /// <summary>
    ///     Check if usage is volatile based on standard deviation
    /// </summary>
    public bool IsVolatile(double volatilityThreshold = 0.3)
    {
        if (AverageUsage == 0)
        {
            return false;
        }

        var coefficientOfVariation = StandardDeviation / AverageUsage;

        return coefficientOfVariation > volatilityThreshold;
    }
}
