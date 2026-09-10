using GameGuild.Finance.Economy.Operations;

namespace GameGuild.Finance.Economy.AdRewards;

public sealed record AdRewardSessionOperationalSummary(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string Network,
    string CreativeId,
    long PolicyVersion,
    DurableAdRewardSessionState State,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset UpdatedAt);

public sealed record AdRewardMilestoneOperationalStatus(
    Guid Id,
    int Sequence,
    int Percentage,
    DateTimeOffset ObservedAt,
    string EvidenceHash);

public sealed record AdRewardSessionEventOperationalStatus(
    Guid Id,
    long Sequence,
    DurableAdRewardSessionState State,
    string EvidenceHash,
    DateTimeOffset OccurredAt);

public sealed record AdRewardCompletionOperationalStatus(
    AdRewardCompletionState State,
    long RewardSoftUnits,
    Guid? PostingId,
    string? ProviderEventId,
    long? ReserveVersion,
    string? JurisdictionCode,
    DateTimeOffset CompletedAt);

public sealed record AdRewardSessionOperationalDetails(
    AdRewardSessionOperationalSummary Summary,
    IReadOnlyList<AdRewardMilestoneOperationalStatus> Milestones,
    IReadOnlyList<AdRewardSessionEventOperationalStatus> Events,
    AdRewardCompletionOperationalStatus? Completion);

public sealed record AdRewardPendingClaimOperationalStatus(
    Guid SessionId,
    Guid TenantId,
    Guid SourceStampId,
    DateTimeOffset DeferredAt,
    Guid? ProviderReportId,
    DateTimeOffset? ConfirmedAt);

public sealed record AdRewardReconciliationOperationalStatus(
    Guid Id,
    Guid TenantId,
    Guid ProviderReportId,
    string Network,
    string ReportId,
    int Version,
    string BatchId,
    long EstimatedRevenueUsdNanos,
    long ActualRevenueUsdNanos,
    long VarianceUsdNanos,
    long HistoricalRewardSoftUnits,
    DateTimeOffset ReconciledAt);

public interface IAdRewardOperationalQueryReader
{
    ValueTask<EconomyOperationalPage<AdRewardSessionOperationalSummary>> ListSessionsAsync(
        Guid tenantId, DurableAdRewardSessionState? state, string? network, int limit,
        string? cursor, CancellationToken cancellationToken);

    ValueTask<AdRewardSessionOperationalDetails?> FindSessionAsync(
        Guid tenantId, Guid sessionId, CancellationToken cancellationToken);

    ValueTask<EconomyOperationalPage<AdRewardPendingClaimOperationalStatus>> ListPendingClaimsAsync(
        Guid tenantId, bool? confirmed, int limit, string? cursor, CancellationToken cancellationToken);

    ValueTask<EconomyOperationalPage<AdRewardReconciliationOperationalStatus>> ListReconciliationsAsync(
        Guid tenantId, string? network, int limit, string? cursor, CancellationToken cancellationToken);
}
