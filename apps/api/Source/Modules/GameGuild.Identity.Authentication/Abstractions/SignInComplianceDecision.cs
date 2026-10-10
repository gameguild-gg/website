namespace GameGuild.Identity.Authentication;

/// <summary>
///     The outcome of a sign-in compliance evaluation.
/// </summary>
public enum SignInComplianceOutcome
{
    /// <summary>Sign-in may proceed and tokens may be issued.</summary>
    Allow,

    /// <summary>Sign-in must complete an additional verification step before tokens are issued.</summary>
    Challenge,

    /// <summary>Sign-in is blocked and no tokens or session may be issued.</summary>
    Deny
}

/// <summary>
///     Coarse, non-sensitive reason codes for sign-in compliance decisions. Reason codes
///     intentionally avoid echoing any underlying verification detail (provider, document,
///     or case references) so they can be audited without leaking compliance specifics.
/// </summary>
public static class SignInComplianceReasons
{
    /// <summary>Default reason attached to allow decisions.</summary>
    public const string Allowed = "Allowed";

    /// <summary>An active restriction hold applies to the user.</summary>
    public const string ComplianceHold = "ComplianceHold";

    /// <summary>The user's verification was completed with a negative outcome.</summary>
    public const string VerificationRejected = "VerificationRejected";

    /// <summary>A previously granted verification was suspended.</summary>
    public const string VerificationSuspended = "VerificationSuspended";

    /// <summary>Verification has been requested but is not decided yet.</summary>
    public const string VerificationPending = "VerificationPending";

    /// <summary>A previously granted verification has lapsed and needs renewal.</summary>
    public const string VerificationExpired = "VerificationExpired";

    /// <summary>The user has no verification on record.</summary>
    public const string VerificationMissing = "VerificationMissing";

    /// <summary>The compliance signals could not be evaluated.</summary>
    public const string EvaluationFailed = "EvaluationFailed";
}

/// <summary>
///     A sign-in compliance decision. Reasons are coarse codes from
///     <see cref="SignInComplianceReasons" /> (or host-defined equivalents) and must not
///     carry verification detail.
/// </summary>
public sealed record SignInComplianceDecision(SignInComplianceOutcome Outcome, string Reason)
{
    /// <summary>The shared allow decision.</summary>
    public static SignInComplianceDecision Allow { get; } =
        new(SignInComplianceOutcome.Allow, SignInComplianceReasons.Allowed);

    /// <summary>Creates a challenge decision.</summary>
    public static SignInComplianceDecision Challenge(string reason) =>
        new(SignInComplianceOutcome.Challenge, reason);

    /// <summary>Creates a deny decision.</summary>
    public static SignInComplianceDecision Deny(string reason) =>
        new(SignInComplianceOutcome.Deny, reason);

    /// <summary>Whether the decision allows the sign-in to proceed.</summary>
    public bool IsAllowed => Outcome == SignInComplianceOutcome.Allow;
}
