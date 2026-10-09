using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Persisted state of a multi-step authentication flow.
///     Backs <see cref="AuthenticationFlowState"/> for the orchestration service.
/// </summary>
public class AuthenticationFlowStateRecord
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
    ///     All steps required to complete authentication (JSON array of step names).
    /// </summary>
    [Required]
    public string RequiredStepsJson { get; set; } = string.Empty;

    /// <summary>
    ///     Steps that have been completed (JSON array of step names).
    /// </summary>
    [Required]
    public string CompletedStepsJson { get; set; } = string.Empty;

    /// <summary>
    ///     Whether the flow has completed all required steps.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    ///     Risk score that triggered additional steps (0..1).
    /// </summary>
    public double? RiskScore { get; set; }

    /// <summary>
    ///     When the flow was initiated (UTC).
    /// </summary>
    public DateTime InitiatedAt { get; set; }

    /// <summary>
    ///     When the flow expires (UTC). Expired flows fail closed.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    ///     IP address for this flow.
    /// </summary>
    [MaxLength(64)]
    public string? IpAddress { get; set; }

    /// <summary>
    ///     Device fingerprint for this flow.
    /// </summary>
    [MaxLength(128)]
    public string? DeviceFingerprint { get; set; }

    /// <summary>
    ///     Step data collected during the flow (JSON object keyed by step name).
    /// </summary>
    public string? StepDataJson { get; set; }

    /// <summary>
    ///     When the flow was abandoned. Abandoned flows fail closed.
    /// </summary>
    public DateTime? AbandonedAt { get; set; }

    /// <summary>
    ///     When the flow was completed and tokens were issued.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    ///     Row creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///     Row last-update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
