using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Operations;

public sealed record EconomyCustodyObservationOperationalStatus(
    Guid Id,
    string Provider,
    string AssetKey,
    ReserveBackingPurpose Purpose,
    long Version,
    long EligibleUsdNanos,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    string PayloadHash,
    string KeyId);

public sealed record EconomyReserveProposalOperationalStatus(
    Guid Id,
    long Version,
    long PolicyVersion,
    long? ExpectedActiveVersion,
    long AuthorizationEpoch,
    string SnapshotHash,
    long LiabilityUsdNanos,
    long EligibleAssetUsdNanos,
    ReserveCoverageState Coverage,
    string ObservationIds,
    string AssetAllocations,
    string EvidenceHash,
    Guid ProposedBy,
    Guid? ApprovedBy,
    DateTimeOffset ProposedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    string Status);

public sealed record EconomyReserveAssetAllocationOperationalStatus(
    Guid Id,
    string AssetKey,
    ReserveBackingPurpose Purpose,
    long EligibleUsdNanos);

public sealed record EconomyCustodyReconciliationOperationalStatus(
    Guid Id,
    long LiabilityUsdNanos,
    long EligibleAssetUsdNanos,
    long VarianceUsdNanos,
    bool IsReconciled,
    string EvidenceHash,
    Guid ReconciledBy,
    DateTimeOffset ReconciledAt);

public sealed record EconomyActiveReserveOperationalDetails(
    EconomyReserveOperationalStatus Head,
    IReadOnlyList<EconomyReserveAssetAllocationOperationalStatus> Allocations,
    EconomyCustodyReconciliationOperationalStatus? Reconciliation);

public interface IEconomyReserveQueryReader
{
    ValueTask<EconomyOperationalPage<EconomyCustodyObservationOperationalStatus>> ListCustodyAsync(
        Guid tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken);

    ValueTask<EconomyCustodyObservationOperationalStatus?> FindCustodyAsync(
        Guid tenantId,
        Guid observationId,
        CancellationToken cancellationToken);

    ValueTask<EconomyOperationalPage<EconomyReserveProposalOperationalStatus>> ListProposalsAsync(
        Guid tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken);

    ValueTask<EconomyReserveProposalOperationalStatus?> FindProposalAsync(
        Guid tenantId,
        Guid proposalId,
        CancellationToken cancellationToken);

    ValueTask<EconomyActiveReserveOperationalDetails?> ReadActiveHeadAsync(
        Guid tenantId,
        CancellationToken cancellationToken);
}
