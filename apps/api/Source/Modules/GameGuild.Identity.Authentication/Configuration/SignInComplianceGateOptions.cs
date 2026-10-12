namespace GameGuild.Identity.Authentication;

/// <summary>
///     How the sign-in compliance gate reacts to non-allow decisions.
/// </summary>
public enum SignInComplianceGateMode
{
    /// <summary>The gate is inactive: every sign-in is allowed without evaluation.</summary>
    Off = 0,

    /// <summary>
    ///     The gate may only challenge: deny-class decisions are downgraded to a step-up
    ///     challenge, so no sign-in is outright rejected. Rollout mode for observing the
    ///     gate before hard enforcement.
    /// </summary>
    ChallengeOnly = 1,

    /// <summary>
    ///     Full enforcement: deny-class decisions reject the sign-in and evaluation
    ///     failures fail closed.
    /// </summary>
    Enforce = 2
}

/// <summary>
///     Configuration for the local sign-in compliance gate
///     (<c>Authentication:Compliance:SignInGate</c>). The safe default keeps the gate
///     disabled: hosts opt in explicitly.
/// </summary>
public sealed class SignInComplianceGateOptions
{
    public const string SectionName = "Authentication:Compliance:SignInGate";

    /// <summary>Master switch for the gate. Defaults to <c>false</c>.</summary>
    public bool Enabled { get; set; }

    /// <summary>Strictness of the gate once enabled. Defaults to <see cref="SignInComplianceGateMode.Off" />.</summary>
    public SignInComplianceGateMode Mode { get; set; } = SignInComplianceGateMode.Off;

    /// <summary>Whether the gate is active and compliance decisions must be evaluated.</summary>
    public bool IsActive => Enabled && Mode != SignInComplianceGateMode.Off;

    /// <summary>Whether evaluation failures must fail closed (deny the sign-in).</summary>
    public bool FailClosed => Enabled && Mode == SignInComplianceGateMode.Enforce;

    /// <summary>The disabled instance used when no configuration is supplied.</summary>
    public static SignInComplianceGateOptions Disabled { get; } = new();
}
