using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Funding;

internal static class FundingAuthorizationFixture
{
    internal static ProtectedIssuanceAuthorization Create(
        PostingTemplateKind operation,
        IdempotencyKey idempotencyKey,
        WalletId walletId,
        CoinAmount amount,
        IReadOnlyList<SourceStampId> sourceRoots,
        DateTimeOffset requestedAt,
        CoinAmount? reserveLiabilityIncrease = null)
    {
        var reserve = new CoreReserveAuthority();
        reserve.ValidateAndActivate(new ReserveProposal(
            new ReserveVersion(1),
            null,
            new PolicyVersion(1),
            1,
            requestedAt.AddMinutes(-1),
            requestedAt.AddMinutes(5),
            new ReserveLiabilityPosition(0, 0, 0, 0),
            new ReserveBufferPosition(0, 0, 0, 0, 0, 0, 0),
            [new ReserveServiceObservation(
                "test-service", 1, 1, 1, 1, 0, true,
                requestedAt.AddMinutes(-1), requestedAt.AddMinutes(5))],
            [
                new ExternalReserveAsset("hard", ReserveBackingPurpose.HardCoin, 2_000_000_000_000),
                new ExternalReserveAsset("soft", ReserveBackingPurpose.SoftCoin, 2_000_000_000_000)
            ],
            "test-reserve-evidence"), requestedAt);
        var context = new ProtectedOperationContext(
            idempotencyKey,
            Guid.NewGuid(),
            operation,
            walletId,
            walletId,
            amount,
            [new RiskCurrencyLeg(amount.Currency, amount.Units)],
            sourceRoots,
            "test-provider-reference",
            new PolicyVersion(1),
            new ReserveVersion(1),
            1,
            1,
            1,
            "test-entity-graph",
            1,
            1);
        var decision = RiskDecisionSnapshot.Create(
            Guid.NewGuid(),
            RiskOutcome.Allow,
            context,
            requestedAt.AddSeconds(-1),
            requestedAt.AddMinutes(1),
            [RiskReasonCode.WithinLimits]);
        var limits = new List<AggregateRiskLimit>
        {
            new(
                new RiskLimitKey(RiskLimitDimension.Wallet, walletId.Value.ToString("N")),
                1,
                long.MaxValue,
                TimeSpan.FromDays(1))
        };
        limits.AddRange(sourceRoots.Select(root => new AggregateRiskLimit(
            new RiskLimitKey(RiskLimitDimension.SourceRoot, root.Value.ToString("N")),
            1,
            long.MaxValue,
            TimeSpan.FromDays(1))));
        return new ProtectedIssuanceAuthorizer(
                reserve,
                new CoreProtectedPostingGate(new RiskDecisionAuthorizer()),
                new AggregateRiskCounterStore(),
                new ProtectedChangeCooldownRegistry())
            .Authorize(new ProtectedIssuanceRequest(
                context,
                new RiskDecisionId(decision.Id),
                decision,
                new RiskPersistenceReadiness(true, true),
                Guid.NewGuid(),
                limits,
                context.ActorId,
                requestedAt,
                reserveLiabilityIncrease));
    }
}
