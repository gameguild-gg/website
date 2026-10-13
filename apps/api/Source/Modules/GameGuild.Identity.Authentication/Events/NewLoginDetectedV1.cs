namespace GameGuild.Identity.Authentication;

/// <summary>
///     A successful sign-in created a new session. Unlike
///     <see cref="SuspiciousLoginDetectedV1"/> this event is informational: the host-side
///     notification bridge only turns it into an owner email when the user explicitly opted in
///     (default off). The payload stays non-personal — the coarse parsed device summary is
///     derived server-side from the user agent and never includes the raw agent, IP address,
///     or precise location.
/// </summary>
public sealed record NewLoginDetectedV1(
    [property: NonPersonalEventData] Guid UserId,
    [property: NonPersonalEventData] Guid SessionId,
    [property: NonPersonalEventData] string DeviceSummary) : DurableIntegrationEventBase
{
    public override string EventName => "identity.authentication.new-login-detected.v1";
    public override string SourceModule => "Identity.Authentication";
}
