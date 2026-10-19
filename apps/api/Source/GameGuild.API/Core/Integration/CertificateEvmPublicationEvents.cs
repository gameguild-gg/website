using GameGuild.Configuration.ApplicationLayer;

namespace GameGuild.API.Core.Integration;

/// <summary>Public publication identity captured at issuance. No endpoint, key, recipient name or revocation reason.</summary>
public sealed record CertificateEvmAnchorRequestedV1 : DurableIntegrationEventBase
{
    public const string Name = "Learning.Certificates.EvmAnchorRequested.v1";
    public override string EventName => Name;
    public override string SourceModule => "GameGuild.API";
    [NonPersonalEventData] public required Guid DocumentId { get; init; }
    [NonPersonalEventData] public required Guid OwnerUserId { get; init; }
    [NonPersonalEventData] public Guid? OriginalTenantId { get; init; }
    [NonPersonalEventData] public required string CertificateHash { get; init; }
    [NonPersonalEventData] public required string CanonicalIssuer { get; init; }
    [NonPersonalEventData] public required string Profile { get; init; }
    [NonPersonalEventData] public required long ChainId { get; init; }
    [NonPersonalEventData] public required string ContractAddress { get; init; }
    [NonPersonalEventData] public required string IssuerId { get; init; }
    [NonPersonalEventData] public required string RuntimeCodeSha256 { get; init; }
    [NonPersonalEventData] public required EvmCertificateFinalityMode FinalityMode { get; init; }
    [NonPersonalEventData] public required int MinimumConfirmations { get; init; }
}

/// <summary>Revocation references the original issuance event instead of hashing current configuration.</summary>
public sealed record CertificateEvmRevocationRequestedV1 : DurableIntegrationEventBase
{
    public const string Name = "Learning.Certificates.EvmRevocationRequested.v1";
    public override string EventName => Name;
    public override string SourceModule => "GameGuild.API";
    [NonPersonalEventData] public required Guid DocumentId { get; init; }
    [NonPersonalEventData] public required Guid OwnerUserId { get; init; }
    [NonPersonalEventData] public Guid? OriginalTenantId { get; init; }
    [NonPersonalEventData] public required Guid AnchorEventId { get; init; }
}
