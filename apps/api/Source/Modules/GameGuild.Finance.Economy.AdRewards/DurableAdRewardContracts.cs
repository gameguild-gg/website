using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.AdRewards;

public enum DurableAdRewardSessionState
{
    Issued = 1,
    Active = 2,
    ProofPending = 3,
    Verified = 4,
    Posted = 5,
    Deferred = 6,
    Rejected = 7
}

public enum AdRewardCapScope
{
    User = 1,
    Device = 2,
    Ip = 3,
    Asn = 4,
    Network = 5,
    Global = 6
}

public sealed record DurableAdRewardSessionClaims(
    Guid SessionId,
    Guid TenantId,
    Guid UserId,
    WalletId WalletId,
    string Network,
    string CreativeId,
    string DeviceRiskHash,
    string IpRiskHash,
    string AsnRiskHash,
    string Nonce,
    TimeSpan RequiredDuration,
    PolicyVersion PolicyVersion,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

public sealed record StartDurableAdRewardSessionRequest(
    Guid TenantId,
    Guid UserId,
    WalletId WalletId,
    string Network,
    string CreativeId,
    string DeviceRiskHash,
    string IpRiskHash,
    string AsnRiskHash,
    TimeSpan RequiredDuration,
    IdempotencyKey IdempotencyKey,
    DateTimeOffset RequestedAt);

public sealed record DurableAdRewardSessionResult(
    DurableAdRewardSessionClaims Claims,
    SignedAdRewardSession Token,
    bool IsDuplicate);

public sealed record CompleteDurableAdRewardSessionRequest(
    SignedAdRewardSession Token,
    AdPlaybackEvidence Playback,
    ProviderCompletionProof? ProviderProof,
    IdempotencyKey IdempotencyKey,
    DateTimeOffset CompletedAt);

public sealed record DurableAdRewardCompletionResult(
    Guid SessionId,
    AdRewardCompletionState State,
    long RewardSoftUnits,
    PostingId? PostingId,
    CreditLotId? OutputLotId,
    bool IsDuplicate,
    DateTimeOffset CompletedAt);

public interface IDurableAdRewardCompletionService
{
    ValueTask<DurableAdRewardCompletionResult> CompleteAsync(
        CompleteDurableAdRewardSessionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ConfirmDeferredAdRewardRequest(
    Guid TenantId,
    Guid ActorId,
    Guid SessionId,
    string SubjectReference,
    string JurisdictionCode,
    IdempotencyKey IdempotencyKey,
    Guid RiskDecisionId,
    string OperationFingerprint,
    DateTimeOffset ConfirmedAt);

public interface IDurableDeferredAdRewardService
{
    ValueTask<DurableAdRewardCompletionResult> ConfirmAsync(
        ConfirmDeferredAdRewardRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AdRewardNetworkPolicySnapshot(
    Guid TenantId,
    AdNetworkPolicy Policy,
    AdRewardBudgetPolicy Budget,
    long MaximumIpSoftUnits,
    long MaximumAsnSoftUnits,
    string ProviderHash,
    bool ProviderCertified,
    string PayloadHash,
    string KeyId,
    string Signature);

public interface IDurableAdRewardPolicyReader
{
    ValueTask<AdRewardNetworkPolicySnapshot> GetEffectiveAsync(
        Guid tenantId,
        string network,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);

    ValueTask<AdRewardNetworkPolicySnapshot> GetVersionAsync(
        Guid tenantId,
        string network,
        PolicyVersion version,
        CancellationToken cancellationToken = default);
}

public interface IAdRewardSessionTokenProtector
{
    ValueTask<SignedAdRewardSession> ProtectAsync(
        DurableAdRewardSessionClaims claims,
        CancellationToken cancellationToken = default);

    ValueTask<DurableAdRewardSessionClaims> UnprotectAsync(
        SignedAdRewardSession token,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public sealed record AdRewardProviderProofVerification(
    bool IsValid,
    string EvidenceHash,
    string PayloadHash,
    DateTimeOffset VerifiedAt);

public interface IAdRewardProviderAdapter
{
    string Network { get; }

    ValueTask<AdRewardProviderProofVerification> VerifyCompletionAsync(
        DurableAdRewardSessionClaims session,
        ProviderCompletionProof proof,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default);

    ValueTask<bool> VerifyReportAsync(
        AdProviderReport report,
        CancellationToken cancellationToken = default);
}

public interface IAdRewardProviderAdapterResolver
{
    IAdRewardProviderAdapter Resolve(string network);
}

public sealed class AdRewardProviderUnavailableException(string message) : InvalidOperationException(message);

public sealed record ImportDurableAdProviderReportRequest(
    Guid TenantId,
    AdProviderReport Report,
    DateTimeOffset ReceivedAt);

public sealed record DurableAdProviderReportImportResult(
    Guid ProviderReportId,
    bool IsDuplicate,
    AdRewardReconciliation Reconciliation,
    IReadOnlyList<Guid> VerifiedPendingSessions);

public interface IDurableAdRewardReportService
{
    ValueTask<DurableAdProviderReportImportResult> ImportAsync(
        ImportDurableAdProviderReportRequest request,
        CancellationToken cancellationToken = default);
}
