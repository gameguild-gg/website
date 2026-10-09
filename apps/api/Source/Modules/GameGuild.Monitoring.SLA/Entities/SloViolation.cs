
namespace GameGuild.Monitoring.SLA;

/// <summary>
///     Represents a violation of a Service Level Objective
/// </summary>
public class SloViolation : EntityBase
{
    /// <summary>
    ///     Foreign key to the Service Level Objective that was violated
    /// </summary>
    public Guid ServiceLevelObjectiveId { get; set; }

    /// <summary>
    ///     Navigation property to the Service Level Objective
    /// </summary>
    public ServiceLevelObjective? ServiceLevelObjective { get; set; }

    /// <summary>
    ///     When the violation started
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    ///     When the violation ended (null if still ongoing)
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    ///     Actual performance value during the violation
    /// </summary>
    public double ActualValue { get; set; }

    /// <summary>
    ///     Target value that was not met
    /// </summary>
    public double TargetValue { get; set; }

    /// <summary>
    ///     Severity level of the violation
    /// </summary>
    public ViolationSeverity Severity { get; set; }

    /// <summary>
    ///     Whether an alert was triggered for this violation
    /// </summary>
    public bool AlertTriggered { get; set; }

    /// <summary>
    ///     When the alert was sent (if applicable)
    /// </summary>
    public DateTimeOffset? AlertSentAt { get; set; }

    /// <summary>
    ///     Whether this violation has been acknowledged by a user
    /// </summary>
    public bool IsAcknowledged { get; set; }

    /// <summary>
    ///     User ID who acknowledged the violation
    /// </summary>
    public Guid? AcknowledgedByUserId { get; set; }

    /// <summary>
    ///     When the violation was acknowledged
    /// </summary>
    public DateTimeOffset? AcknowledgedAt { get; set; }

    /// <summary>
    ///     Notes added when acknowledging the violation
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    ///     Description of the violation
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Gets the duration of the violation
    /// </summary>
    /// <returns>TimeSpan of the violation duration</returns>
    public TimeSpan GetDuration()
    {
        var endTime = EndedAt ?? DateTimeOffset.UtcNow;

        return endTime - StartedAt;
    }

    /// <summary>
    ///     Marks the violation as resolved
    /// </summary>
    public void Resolve()
    {
        if (EndedAt.HasValue)
        {
            return; // Already resolved
        }

        EndedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    ///     Acknowledges the violation with user information
    /// </summary>
    /// <param name="userId">User ID who is acknowledging</param>
    /// <param name="notes">Optional notes about the acknowledgment</param>
    public void Acknowledge(Guid userId, string? notes = null)
    {
        IsAcknowledged = true;
        AcknowledgedByUserId = userId;
        AcknowledgedAt = DateTimeOffset.UtcNow;
        Notes = notes;
    }

    /// <summary>
    ///     Determines severity based on how far actual is from target
    /// </summary>
    /// <param name="actualPercentage">Actual performance percentage</param>
    /// <param name="targetPercentage">Target performance percentage</param>
    /// <returns>Calculated severity level</returns>
    public static ViolationSeverity DetermineSeverity(double actualPercentage, double targetPercentage)
    {
        var difference = targetPercentage - actualPercentage;

        // More than 5% below target
        if (difference >= 5.0)
        {
            return ViolationSeverity.Critical;
        }

        // 2-5% below target
        if (difference >= 2.0)
        {
            return ViolationSeverity.High;
        }

        // 0.5-2% below target
        if (difference >= 0.5)
        {
            return ViolationSeverity.Medium;
        }

        // Less than 0.5% below target
        return ViolationSeverity.Low;
    }

    /// <summary>
    ///     Triggers an alert for this violation
    /// </summary>
    public void TriggerAlert()
    {
        AlertTriggered = true;
        AlertSentAt = DateTimeOffset.UtcNow;
    }
}
