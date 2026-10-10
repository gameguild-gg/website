namespace GameGuild.Identity.Authentication;

/// <summary>
///     Alert kinds raised by the authentication security pipeline. Each kind maps to a
///     dedicated owner-facing security notification; the payloads stay non-personal.
/// </summary>
public static class SecurityAlertKinds
{
    /// <summary>A sign-in was challenged with step-up authentication because its risk reached the high-risk bar.</summary>
    public const string LoginStepUpRequired = "LoginStepUpRequired";

    /// <summary>Repeated failed sign-in attempts were detected against a known account.</summary>
    public const string BruteForceDetected = "BruteForceDetected";

    /// <summary>A successful sign-in originated from a location unreachable from the previous one in the elapsed time.</summary>
    public const string ImpossibleTravel = "ImpossibleTravel";
}

/// <summary>
///     Suspicious authentication pattern confirmed (step-up challenge, brute force, or impossible
///     travel). The payload is redacted to non-personal identifiers and risk metadata; recipient
///     data is loaded by the consumer. Owner alerts are queued by the host-side notification bridge.
/// </summary>
public sealed record SuspiciousLoginDetectedV1(
    [property: NonPersonalEventData] Guid UserId,
    [property: NonPersonalEventData] string AlertKind,
    [property: NonPersonalEventData] string RiskLevel,
    [property: NonPersonalEventData] int RiskScore) : DurableIntegrationEventBase
{
    public override string EventName => "identity.authentication.suspicious-login-detected.v1";
    public override string SourceModule => "Identity.Authentication";
}
