namespace GameGuild.Identity.Authentication;

/// <summary>Committed credential containment; recipient data is loaded by the consumer.</summary>
public sealed record RefreshTokenReplayContainedV1(
    [property: NonPersonalEventData] Guid UserId,
    [property: NonPersonalEventData] Guid TokenId,
    [property: NonPersonalEventData] Guid? SessionId) : DurableIntegrationEventBase
{
    public override string EventName => "identity.authentication.refresh-token.replay-contained.v1";
    public override string SourceModule => "Identity.Authentication";
}
