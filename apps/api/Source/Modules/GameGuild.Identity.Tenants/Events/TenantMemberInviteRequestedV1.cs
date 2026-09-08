namespace GameGuild.Identity.Tenants;

/// <summary>Durable invitation intent. Recipient details remain in the membership row.</summary>
public sealed record TenantMemberInviteRequestedV1(
    [property: NonPersonalEventData] Guid MemberId = default,
    [property: NonPersonalEventData] bool Resend = false) : DurableIntegrationEventBase
{
    public override string EventName => "identity.tenant-member.invite-requested.v1";
    public override string SourceModule => "Identity.Tenants";
}
