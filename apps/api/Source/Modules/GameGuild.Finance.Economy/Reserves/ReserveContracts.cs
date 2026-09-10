using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Reserves;

public enum ReserveBackingPurpose
{
    HardCoin = 1,
    SoftCoin = 2
}

public enum ReserveCoverageState
{
    Covered = 1,
    Shortfall = 2
}

public sealed record ExternalReserveAsset(
    string AssetKey,
    ReserveBackingPurpose Purpose,
    long EligibleUsdNanos);

public sealed record ReserveServiceObservation(
    string ServiceCode,
    long CurrentServicePriceSoftUnits,
    long CurrentProviderCostUsdNanos,
    long TrailingHighPercentileCostUsdNanos,
    long ProviderFxStressCostUsdNanos,
    long ReservedSoftUnits,
    bool Enabled,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt);

public sealed record ReserveLiabilityPosition(
    long OutstandingHardUnits,
    long OutstandingSoftUnits,
    long UnreservedSoftUnits,
    long IrreversibleInFlightProviderCostUsdNanos);

public sealed record ReserveBufferPosition(
    long ChargebackRefundBufferUsdMinor,
    long PayoutSettlementBufferUsdMinor,
    long HardOperatingLiquidityBufferUsdMinor,
    long AdEstimateVarianceBufferUsdNanos,
    long FraudLossBudgetUsdNanos,
    long ProviderFxBufferUsdNanos,
    long SoftOperatingLiquidityBufferUsdNanos);

public sealed record ReserveProposal(
    ReserveVersion Version,
    ReserveVersion? ExpectedActiveVersion,
    PolicyVersion PolicyVersion,
    long AuthorizationEpoch,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    ReserveLiabilityPosition Liabilities,
    ReserveBufferPosition Buffers,
    IReadOnlyCollection<ReserveServiceObservation> Services,
    IReadOnlyCollection<ExternalReserveAsset> AssetAllocations,
    string EvidenceHash);

public sealed record ReserveRequirementSnapshot(
    long HardFaceValueUsdMinor,
    long RequiredHardReserveUsdMinor,
    long SoftFaceValueUsdNanos,
    long StressedExpectedRedemptionCostUsdNanos,
    long RequiredSoftReserveUsdNanos);

public sealed record ReserveHead(
    ReserveVersion Version,
    PolicyVersion PolicyVersion,
    long AuthorizationEpoch,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    ReserveRequirementSnapshot Requirements,
    long HardBackingUsdNanos,
    long SoftBackingUsdNanos,
    ReserveCoverageState Coverage,
    IReadOnlyList<ExternalReserveAsset> AssetAllocations,
    string EvidenceHash);

public sealed record ReservePostingAuthorization
{
    internal ReservePostingAuthorization(
        ReserveVersion version,
        long authorizationEpoch,
        DateTimeOffset lockedAt)
    {
        if (version.Value <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(authorizationEpoch);
        Version = version;
        AuthorizationEpoch = authorizationEpoch;
        LockedAt = lockedAt;
    }

    public ReserveVersion Version { get; }
    public long AuthorizationEpoch { get; }
    public DateTimeOffset LockedAt { get; }
}

public sealed class ReserveInputUnknownException(string message) : InvalidOperationException(message);
public sealed class ReserveVersionConflictException(string message) : InvalidOperationException(message);
public sealed class ReserveAuthorizationEpochException(string message) : InvalidOperationException(message);
public sealed class ReserveAuthorizationException(string message) : InvalidOperationException(message);
public sealed class ReserveShortfallException(string message) : InvalidOperationException(message);
public sealed class DuplicateReserveAssetException(string message) : InvalidOperationException(message);
