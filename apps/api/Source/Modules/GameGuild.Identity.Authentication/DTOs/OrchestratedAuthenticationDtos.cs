using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Request body to initiate an orchestrated authentication flow.
/// </summary>
public class InitiateOrchestratedAuthenticationRequestDto
{
    /// <summary>
    ///     Credential identifier (email) of the account signing in.
    /// </summary>
    [Required]
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    ///     The account password; verified as the primary credential at initiation.
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    ///     Optional tenant context for the resulting sign-in.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    ///     Optional device fingerprint used for trusted-device recognition.
    ///     Missing or unrecognized fingerprints fail closed to a device-trust challenge.
    /// </summary>
    public string? DeviceFingerprint { get; set; }
}

/// <summary>
///     Request body to satisfy one challenge step of an orchestrated authentication flow.
///     Exactly one proof field (TOTP code, backup code or WebAuthn assertion) is required.
/// </summary>
public class ProcessOrchestratedAuthenticationStepRequestDto
{
    /// <summary>
    ///     The challenge step being satisfied.
    /// </summary>
    [Required]
    public AuthenticationStep Step { get; set; }

    /// <summary>
    ///     TOTP verification code, when satisfying the step with the authenticator app.
    /// </summary>
    public string? TotpCode { get; set; }

    /// <summary>
    ///     One-time backup code, when satisfying the step with a recovery code.
    /// </summary>
    public string? BackupCode { get; set; }

    /// <summary>
    ///     Serialized WebAuthn assertion, when satisfying the step with a passkey/security key.
    /// </summary>
    public string? WebAuthnAssertion { get; set; }
}

/// <summary>
///     Response describing the state of an orchestrated authentication flow.
/// </summary>
public class OrchestratedFlowStateResponse
{
    /// <summary>
    ///     Unique identifier of the flow; carried by the client across step processing and completion.
    /// </summary>
    public Guid FlowId { get; set; }

    /// <summary>
    ///     The authenticated user, identified after primary-credential validation.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    ///     The step the flow is currently waiting on.
    /// </summary>
    public AuthenticationStep CurrentStep { get; set; }

    /// <summary>
    ///     Every step the flow requires before completion.
    /// </summary>
    public List<AuthenticationStep> RequiredSteps { get; set; } = [];

    /// <summary>
    ///     Steps already satisfied.
    /// </summary>
    public List<AuthenticationStep> CompletedSteps { get; set; } = [];

    /// <summary>
    ///     The next step to satisfy, or null when all required steps are satisfied.
    /// </summary>
    public AuthenticationStep? NextStep { get; set; }

    /// <summary>
    ///     Whether every required step is satisfied and the flow can be completed.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    ///     Risk score (0–1) that drove the challenge ladder for this flow.
    /// </summary>
    public double? RiskScore { get; set; }

    /// <summary>
    ///     When the flow was initiated.
    /// </summary>
    public DateTime InitiatedAt { get; set; }

    /// <summary>
    ///     When the flow expires; expired flows fail closed.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
