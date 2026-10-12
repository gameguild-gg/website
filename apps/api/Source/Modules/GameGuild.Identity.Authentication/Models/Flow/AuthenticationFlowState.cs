namespace GameGuild.Identity.Authentication;

/// <summary>
///     Represents the state of a multi-step authentication flow.
/// </summary>
public abstract class AuthenticationFlowState
{
    /// <summary>
    ///     Unique identifier for this authentication flow.
    /// </summary>
    public Guid FlowId { get; set; }

    /// <summary>
    ///     User ID (once identified).
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    ///     Current step in the authentication flow.
    /// </summary>
    public AuthenticationStep CurrentStep { get; set; }

    /// <summary>
    ///     All steps required to complete authentication.
    /// </summary>
    public List<AuthenticationStep> RequiredSteps { get; set; } = new List<AuthenticationStep>();

    /// <summary>
    ///     Steps that have been completed.
    /// </summary>
    public List<AuthenticationStep> CompletedSteps { get; set; } = new List<AuthenticationStep>();

    /// <summary>
    ///     Whether the flow is complete.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    ///     Risk score that triggered additional steps.
    /// </summary>
    public double? RiskScore { get; set; }

    /// <summary>
    ///     When the flow was initiated.
    /// </summary>
    public DateTime InitiatedAt { get; set; }

    /// <summary>
    ///     When the flow expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    ///     IP address for this flow.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    ///     Device fingerprint for this flow.
    /// </summary>
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    ///     State data for each step (keyed by step name).
    /// </summary>
    public Dictionary<string, object>? StepData { get; set; }

    /// <summary>
    ///     Gets the next step to complete, or <see langword="null"/> when every required
    ///     step is already satisfied. (A plain FirstOrDefault over the non-nullable step
    ///     enum would return the default value <c>0</c> instead of null, so an empty
    ///     remainder must be handled explicitly.)
    /// </summary>
    public AuthenticationStep? NextStep
    {
        get
        {
            var remaining = RequiredSteps.Except(CompletedSteps).ToList();
            return remaining.Count > 0 ? remaining[0] : null;
        }
    }

    /// <summary>
    ///     Gets whether the flow has expired.
    /// </summary>
    public bool IsExpired { get => SystemClock.UtcNow > ExpiresAt; }
}
