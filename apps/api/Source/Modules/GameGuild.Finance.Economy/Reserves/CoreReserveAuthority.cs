using System.Numerics;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Reserves;

public sealed class CoreReserveAuthority
{
    private const long UsdNanosPerCent = 10_000_000;
    private const long UsdNanosPerSoftCoin = 10_000;
    private readonly object _gate = new();
    private ReserveHead? _activeHead;

    public ReserveHead? ActiveHead
    {
        get
        {
            lock (_gate) return _activeHead;
        }
    }

    public ReserveHead ValidateAndActivate(ReserveProposal proposal, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        lock (_gate)
        {
            ValidateVersion(proposal);
            ValidateEpoch(proposal.AuthorizationEpoch);
            ValidateProposalWindow(proposal, now);
            var assets = ValidateAssets(proposal.AssetAllocations);
            var requirements = CalculateRequirements(proposal, now);
            var hardBacking = SumBacking(assets, ReserveBackingPurpose.HardCoin);
            var softBacking = SumBacking(assets, ReserveBackingPurpose.SoftCoin);
            var covered = (BigInteger)requirements.RequiredHardReserveUsdMinor * UsdNanosPerCent <= hardBacking &&
                          requirements.RequiredSoftReserveUsdNanos <= softBacking;

            var head = new ReserveHead(
                proposal.Version,
                proposal.PolicyVersion,
                proposal.AuthorizationEpoch,
                proposal.ObservedAt,
                proposal.ExpiresAt,
                requirements,
                hardBacking,
                softBacking,
                covered ? ReserveCoverageState.Covered : ReserveCoverageState.Shortfall,
                assets,
                proposal.EvidenceHash.Trim());
            _activeHead = head;
            return head;
        }
    }

    public ReservePostingAuthorization Authorize(
        ReserveVersion version,
        long authorizationEpoch,
        DateTimeOffset now)
    {
        lock (_gate)
        {
            _ = AuthorizedHead(version, authorizationEpoch, now);
            return new ReservePostingAuthorization(version, authorizationEpoch, now);
        }
    }

    public ReservePostingAuthorization AuthorizeIssuance(
        ReserveVersion version,
        long authorizationEpoch,
        CoinAmount liabilityIncrease,
        DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(liabilityIncrease.Units);
        lock (_gate)
        {
            var head = AuthorizedHead(version, authorizationEpoch, now);
            EnsureIssuanceHeadroom(head, liabilityIncrease);
            return new ReservePostingAuthorization(version, authorizationEpoch, now);
        }
    }

    private ReserveHead AuthorizedHead(ReserveVersion version, long authorizationEpoch, DateTimeOffset now)
    {
        var head = _activeHead ??
                   throw new ReserveAuthorizationException("No authoritative reserve head is active.");
        if (head.ObservedAt > now || head.ExpiresAt <= now)
            throw new ReserveInputUnknownException("The active reserve head is stale.");
        if (head.Version != version)
            throw new ReserveAuthorizationException("The requested reserve version is not active.");
        if (head.AuthorizationEpoch != authorizationEpoch)
            throw new ReserveAuthorizationEpochException("The requested reserve authorization epoch is not active.");
        if (head.Coverage != ReserveCoverageState.Covered)
            throw new ReserveShortfallException("The active reserve head does not cover required liabilities and buffers.");
        return head;
    }

    private static void EnsureIssuanceHeadroom(ReserveHead head, CoinAmount liabilityIncrease)
    {
        var required = liabilityIncrease.Currency == CurrencyCode.HardCoin
            ? ((BigInteger)head.Requirements.RequiredHardReserveUsdMinor + liabilityIncrease.Units) * UsdNanosPerCent
            : (BigInteger)head.Requirements.RequiredSoftReserveUsdNanos +
              (BigInteger)liabilityIncrease.Units * UsdNanosPerSoftCoin;
        var backing = liabilityIncrease.Currency == CurrencyCode.HardCoin
            ? head.HardBackingUsdNanos
            : head.SoftBackingUsdNanos;
        if (required > backing)
            throw new ReserveShortfallException("The active reserve head lacks incremental issuance headroom.");
    }

    private void ValidateVersion(ReserveProposal proposal)
    {
        if (_activeHead is null)
        {
            if (proposal.ExpectedActiveVersion is not null)
                throw new ReserveVersionConflictException("The expected reserve version does not match the empty head.");
            return;
        }

        if (proposal.ExpectedActiveVersion != _activeHead.Version)
            throw new ReserveVersionConflictException("The expected reserve version is stale.");
        if (proposal.Version.Value <= _activeHead.Version.Value)
            throw new ReserveVersionConflictException("Reserve versions must increase monotonically.");
    }

    private void ValidateEpoch(long authorizationEpoch)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(authorizationEpoch);
        if (_activeHead is not null && authorizationEpoch <= _activeHead.AuthorizationEpoch)
            throw new ReserveAuthorizationEpochException("Reserve authorization epochs must increase monotonically.");
    }

    private static void ValidateProposalWindow(ReserveProposal proposal, DateTimeOffset now)
    {
        if (proposal.ObservedAt > now || proposal.ExpiresAt <= now || proposal.ExpiresAt <= proposal.ObservedAt)
            throw new ReserveInputUnknownException("Reserve proposal evidence is stale or has an invalid window.");
        ArgumentException.ThrowIfNullOrWhiteSpace(proposal.EvidenceHash);
        ArgumentNullException.ThrowIfNull(proposal.Liabilities);
        ArgumentNullException.ThrowIfNull(proposal.Buffers);
        ArgumentNullException.ThrowIfNull(proposal.Services);
    }

    private static ExternalReserveAsset[] ValidateAssets(IReadOnlyCollection<ExternalReserveAsset> allocations)
    {
        ArgumentNullException.ThrowIfNull(allocations);
        var assets = new Dictionary<string, ExternalReserveAsset>(StringComparer.Ordinal);
        foreach (var asset in allocations)
        {
            if (asset is null || string.IsNullOrWhiteSpace(asset.AssetKey) ||
                !Enum.IsDefined(asset.Purpose) || asset.EligibleUsdNanos <= 0)
                throw new ReserveInputUnknownException("External reserve asset allocation is invalid.");
            var normalized = asset with { AssetKey = asset.AssetKey.Trim() };
            if (!assets.TryAdd(normalized.AssetKey, normalized))
                throw new DuplicateReserveAssetException(
                    $"External reserve asset {normalized.AssetKey} cannot back more than one reserve pool.");
        }

        return [.. assets.Values.OrderBy(asset => asset.AssetKey, StringComparer.Ordinal)];
    }

    private static ReserveRequirementSnapshot CalculateRequirements(ReserveProposal proposal, DateTimeOffset now)
    {
        var liabilities = proposal.Liabilities;
        var buffers = proposal.Buffers;
        var hardFace = ReserveFormula.HardFaceValueUsdMinor(liabilities.OutstandingHardUnits);
        var requiredHard = ReserveFormula.RequiredHardReserveUsdMinor(
            hardFace,
            buffers.ChargebackRefundBufferUsdMinor,
            buffers.PayoutSettlementBufferUsdMinor,
            buffers.HardOperatingLiquidityBufferUsdMinor);
        var softFace = ReserveFormula.SoftFaceValueUsdNanos(liabilities.OutstandingSoftUnits);
        var stressed = ReserveFormula.StressedExpectedRedemptionCostUsdNanos(
            liabilities.OutstandingSoftUnits,
            liabilities.UnreservedSoftUnits,
            liabilities.IrreversibleInFlightProviderCostUsdNanos,
            proposal.Services,
            now);
        var requiredSoft = ReserveFormula.RequiredSoftReserveUsdNanos(
            softFace,
            stressed,
            buffers.AdEstimateVarianceBufferUsdNanos,
            buffers.FraudLossBudgetUsdNanos,
            buffers.ProviderFxBufferUsdNanos,
            buffers.SoftOperatingLiquidityBufferUsdNanos);
        return new ReserveRequirementSnapshot(hardFace, requiredHard, softFace, stressed, requiredSoft);
    }

    private static long SumBacking(
        IReadOnlyCollection<ExternalReserveAsset> assets,
        ReserveBackingPurpose purpose)
    {
        var total = assets.Where(asset => asset.Purpose == purpose)
            .Aggregate(BigInteger.Zero, (current, asset) => current + asset.EligibleUsdNanos);
        if (total > long.MaxValue)
            throw new OverflowException("Reserve backing exceeded the supported unit range.");
        return (long)total;
    }
}
