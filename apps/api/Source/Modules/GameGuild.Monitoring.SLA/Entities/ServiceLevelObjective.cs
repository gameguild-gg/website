
namespace GameGuild.Monitoring.SLA;

/// <summary>
///     Represents a Service Level Objective (SLO) that defines performance targets for a service
/// </summary>
public class ServiceLevelObjective : EntityBase
{
    /// <summary>
    ///     Human-readable name of the SLO
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Description of what this SLO measures
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Name of the service this SLO applies to
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    ///     Target percentage for success (e.g., 99.9 for 99.9% uptime)
    /// </summary>
    public double TargetPercentage { get; set; }

    /// <summary>
    ///     Number of days in the time window for calculating compliance
    /// </summary>
    public int TimeWindowDays { get; set; } = 30;

    /// <summary>
    ///     Calculated error budget percentage (100 - TargetPercentage)
    /// </summary>
    public double ErrorBudgetPercentage { get; set; }

    /// <summary>
    ///     Threshold percentage of error budget remaining before alerting (default 50%)
    /// </summary>
    public double AlertThresholdPercentage { get; set; } = 50.0;

    /// <summary>
    ///     Whether this SLO is currently enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    ///     Current status of the SLO
    /// </summary>
    public SloStatus Status { get; set; } = SloStatus.Active;

    /// <summary>
    ///     Last time the SLO was evaluated
    /// </summary>
    public DateTimeOffset? LastEvaluatedAt { get; set; }

    /// <summary>
    ///     Current actual performance percentage
    /// </summary>
    public double? CurrentActualPercentage { get; set; }

    /// <summary>
    ///     Remaining error budget as a percentage
    /// </summary>
    public double? RemainingErrorBudget { get; set; }

    /// <summary>
    ///     Collection of Service Level Indicators (metrics) for this SLO
    /// </summary>
    public ICollection<ServiceLevelIndicator> Indicators { get; set; } = new List<ServiceLevelIndicator>();

    /// <summary>
    ///     Collection of violations for this SLO
    /// </summary>
    public ICollection<SloViolation> Violations { get; set; } = new List<SloViolation>();

    /// <summary>
    ///     Calculates the error budget percentage from the target
    /// </summary>
    public void CalculateErrorBudget() { ErrorBudgetPercentage = 100.0 - TargetPercentage; }

    /// <summary>
    ///     Updates the SLO status based on actual performance
    /// </summary>
    /// <param name="actualPercentage">Current actual performance percentage</param>
    public void UpdateStatus(double actualPercentage)
    {
        CurrentActualPercentage = actualPercentage;
        LastEvaluatedAt = DateTimeOffset.UtcNow;

        if (!IsEnabled)
        {
            Status = SloStatus.Disabled;

            return;
        }

        if (actualPercentage < TargetPercentage) { Status = SloStatus.Breached; }
        else
        {
            // Calculate remaining error budget
            var errorBudget = 100.0 - TargetPercentage;
            var usedBudget = 100.0 - actualPercentage;
            var remainingBudget = errorBudget - usedBudget;
            RemainingErrorBudget = remainingBudget / errorBudget * 100.0;

            // Check if we should be in AtRisk status
            if (RemainingErrorBudget <= AlertThresholdPercentage) { Status = SloStatus.AtRisk; }
            else { Status = SloStatus.Active; }
        }
    }

    /// <summary>
    ///     Determines if an alert should be triggered based on current state
    /// </summary>
    /// <returns>True if alert conditions are met</returns>
    public bool ShouldTriggerAlert()
    {
        if (!IsEnabled)
        {
            return false;
        }

        // Alert if breached
        if (Status == SloStatus.Breached)
        {
            return true;
        }

        // Alert if at risk (error budget threshold exceeded)
        if (Status == SloStatus.AtRisk && RemainingErrorBudget.HasValue) { return RemainingErrorBudget.Value <= AlertThresholdPercentage; }

        return false;
    }

    /// <summary>
    ///     Disables the SLO
    /// </summary>
    public void Disable()
    {
        IsEnabled = false;
        Status = SloStatus.Disabled;
    }

    /// <summary>
    ///     Enables the SLO
    /// </summary>
    public void Enable()
    {
        IsEnabled = true;

        if (Status == SloStatus.Disabled) { Status = SloStatus.Active; }
    }
}
