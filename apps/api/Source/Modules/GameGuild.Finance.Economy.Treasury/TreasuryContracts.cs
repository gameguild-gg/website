using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Treasury;

public enum TreasuryAssetKind
{
    SettledCash = 1,
    ProviderReceivable = 2
}

public enum TreasurySettlementFinality
{
    Pending = 1,
    Final = 2,
    Disputed = 3
}

public enum TreasuryProtectedOperation
{
    Issuance = 1,
    PayoutDispatch = 2,
    Refund = 3,
    AdminWithdrawal = 4
}

public sealed record ExternalAssetObservation(
    string Provider,
    string AccountOrNetwork,
    string ProviderObjectId,
    TreasuryAssetKind Kind,
    ReserveBackingPurpose Purpose,
    long GrossUsdNanos,
    TreasurySettlementFinality Finality,
    int HaircutPpm,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    string EvidenceHash)
{
    public string AssetKey => $"{Provider.Trim().ToLowerInvariant()}:{AccountOrNetwork.Trim()}:{ProviderObjectId.Trim()}";
}

public static class TreasuryProviderSnapshots
{
    public static ExternalAssetObservation StripeCash(
        string account,
        string balanceTransactionId,
        ReserveBackingPurpose purpose,
        long grossUsdNanos,
        DateTimeOffset observedAt,
        DateTimeOffset expiresAt,
        string evidenceHash) => new(
        "stripe", account, balanceTransactionId, TreasuryAssetKind.SettledCash, purpose,
        grossUsdNanos, TreasurySettlementFinality.Final, 0, observedAt, expiresAt, evidenceHash);

    public static ExternalAssetObservation StripeReceivable(
        string account,
        string providerObjectId,
        ReserveBackingPurpose purpose,
        long grossUsdNanos,
        TreasurySettlementFinality finality,
        int haircutPpm,
        DateTimeOffset observedAt,
        DateTimeOffset expiresAt,
        string evidenceHash) => new(
        "stripe", account, providerObjectId, TreasuryAssetKind.ProviderReceivable, purpose,
        grossUsdNanos, finality, haircutPpm, observedAt, expiresAt, evidenceHash);

    public static ExternalAssetObservation AdReceivable(
        string network,
        string reportOrBatchId,
        ReserveBackingPurpose purpose,
        long grossUsdNanos,
        TreasurySettlementFinality finality,
        int haircutPpm,
        DateTimeOffset observedAt,
        DateTimeOffset expiresAt,
        string evidenceHash) => new(
        "ad", network, reportOrBatchId, TreasuryAssetKind.ProviderReceivable, purpose,
        grossUsdNanos, finality, haircutPpm, observedAt, expiresAt, evidenceHash);
}

public sealed record TreasuryServiceCostSnapshot(
    string ServiceCode,
    long CurrentServicePriceSoftUnits,
    long CurrentProviderCostUsdNanos,
    long TrailingHighPercentileCostUsdNanos,
    long ProviderFxStressCostUsdNanos,
    bool Enabled,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt);

public sealed record TreasuryOpenServiceAuthorization(
    string AuthorizationKey,
    string ServiceCode,
    long ReservedSoftUnits,
    long IrreversibleProviderCostUsdNanos);

public sealed record TreasuryLotLiability(
    CreditLotId LotId,
    WalletId WalletId,
    CurrencyCode Currency,
    long OutstandingUnits,
    CreditLotState State);

public sealed record TreasuryLiabilityCalculation(
    ReserveLiabilityPosition Position,
    IReadOnlyList<ReserveServiceObservation> Services,
    IReadOnlyList<TreasuryLotLiability> Lots);

public sealed record TreasuryBufferRule(long AbsoluteFloor, int PercentageFloorPpm);

public sealed record TreasuryBufferExposure(
    long ChargebackRefundUsdMinor,
    long PayoutSettlementUsdMinor,
    long HardOperatingLiquidityUsdMinor,
    long AdEstimateVarianceUsdNanos,
    long FraudLossUsdNanos,
    long ProviderFxUsdNanos,
    long SoftOperatingLiquidityUsdNanos);

public sealed record TreasuryProposalRequest(
    ReserveVersion Version,
    ReserveVersion? ExpectedActiveVersion,
    PolicyVersion PolicyVersion,
    long AuthorizationEpoch,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    InMemoryLedgerKernelStore Ledger,
    IReadOnlySet<WalletId> CompanyOwnedWallets,
    TreasuryBufferPolicy BufferPolicy,
    TreasuryBufferExposure BufferExposure,
    IReadOnlyCollection<TreasuryServiceCostSnapshot> ServiceCosts,
    IReadOnlyCollection<TreasuryOpenServiceAuthorization> OpenAuthorizations,
    IReadOnlyCollection<ExternalAssetObservation> Assets);

public sealed record TreasuryProposalEnvelope(
    ReserveProposal Proposal,
    TreasuryLiabilityCalculation LiabilityCalculation,
    IReadOnlyList<ExternalAssetObservation> AssetObservations,
    string EvidenceManifest,
    string Signature);

public sealed record TreasuryCustodyObservation(
    string AssetKey,
    long ActualUsdNanos,
    long ExplainedVarianceUsdNanos,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    string EvidenceHash);

public sealed record TreasuryCustodyVariance(
    string AssetKey,
    long ExpectedUsdNanos,
    long ActualUsdNanos,
    long ExplainedVarianceUsdNanos,
    long UnexplainedVarianceUsdNanos);

public sealed record TreasuryCustodyReport(
    ReserveVersion ReserveVersion,
    long AuthorizationEpoch,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    long ExpectedUsdNanos,
    long ActualUsdNanos,
    long ExplainedVarianceUsdNanos,
    long UnexplainedVarianceUsdNanos,
    IReadOnlyList<TreasuryCustodyVariance> Variances,
    string EvidenceHash,
    string Signature)
{
    public bool IsReconciled => UnexplainedVarianceUsdNanos == 0;
}

public sealed class TreasurySignatureException(string message) : InvalidOperationException(message);
public sealed class TreasuryCustodyVarianceException(string message) : InvalidOperationException(message);
