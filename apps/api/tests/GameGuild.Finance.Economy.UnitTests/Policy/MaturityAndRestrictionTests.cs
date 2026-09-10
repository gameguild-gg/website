using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Policy;

namespace GameGuild.Finance.Economy.UnitTests.Policy;

public sealed class MaturityAndRestrictionTests
{
    private static readonly DateTimeOffset ObservedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ConfirmedAt = ObservedAt.AddDays(1);

    [Fact]
    public void EarnedHardUsesAnExactIndependentOneHundredTwentyDayClock()
    {
        var first = Confirmed("earned-1", ConfirmedAt);
        var second = Confirmed("earned-2", ConfirmedAt.AddDays(10));

        var firstLot = ConfirmedCreditFactory.CreateRootLot(
            CreditLotId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 10),
            ProvenanceKind.EarnedHard, first, 1);
        var secondLot = ConfirmedCreditFactory.CreateRootLot(
            CreditLotId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 10),
            ProvenanceKind.EarnedHard, second, 2);

        firstLot.OriginalMaturesAt.Should().Be(ConfirmedAt.AddDays(120));
        secondLot.OriginalMaturesAt.Should().Be(ConfirmedAt.AddDays(130));
        secondLot.OriginalMaturesAt.Should().NotBe(firstLot.OriginalMaturesAt);
    }

    [Fact]
    public void RootLotCreationRequiresConfirmedSourceAndExactEarnedMaturity()
    {
        var observed = SourceEvidence.Observe(SourceStampId.New(), "platform", "earned-observed", "evidence", ObservedAt);
        FluentActions.Invoking(() => ConfirmedCreditFactory.CreateRootLot(
                CreditLotId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 1),
                ProvenanceKind.EarnedHard, observed, 1))
            .Should().Throw<InvalidOperationException>();

        var confirmed = observed.Confirm(ConfirmedAt);
        FluentActions.Invoking(() => ConfirmedCreditFactory.CreateRootLot(
                CreditLotId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 1),
                ProvenanceKind.EarnedHard, confirmed, ConfirmedAt.AddDays(121), 1))
            .Should().Throw<ArgumentException>().WithMessage("*exactly 120 days*");

        CreditLotMaturity.Assign(CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, ConfirmedAt)
            .Should().Be(ConfirmedAt);
        CreditLotMaturity.Assign(CurrencyCode.SoftCoin, ProvenanceKind.AdRewardSoft, ConfirmedAt)
            .Should().Be(ConfirmedAt);
        FluentActions.Invoking(() => CreditLotMaturity.EnsureExactEarnedHard(
                CurrencyCode.HardCoin, ProvenanceKind.EarnedHard, ConfirmedAt,
                ConfirmedAt.Add(EconomyParity.EarnedHardMaturity)))
            .Should().NotThrow();
        FluentActions.Invoking(() => CreditLotMaturity.EnsureExactEarnedHard(
                CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, ConfirmedAt, ConfirmedAt))
            .Should().NotThrow();
        FluentActions.Invoking(() => CreditLotMaturity.EnsureExactEarnedHard(
                CurrencyCode.SoftCoin, ProvenanceKind.EarnedHard, ConfirmedAt, ConfirmedAt))
            .Should().NotThrow();
    }

    [Fact]
    public void PayoutEligibilityUsesExactBoundaryAndRejectsPurchasedOrSoftLotsForever()
    {
        var wallet = WalletId.New();
        var earned = Lot(wallet, CurrencyCode.HardCoin, ProvenanceKind.EarnedHard, ConfirmedAt.AddDays(120));
        var purchased = Lot(wallet, CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, ConfirmedAt);
        var soft = Lot(wallet, CurrencyCode.SoftCoin, ProvenanceKind.AdRewardSoft, ConfirmedAt);
        var active = new WalletRestrictionSnapshot(wallet, WalletLifecycleState.Active, 0);

        PayoutEligibilityEvaluator.Evaluate(earned, ConfirmedAt.AddDays(120).AddTicks(-1), [], active)
            .IsEligible.Should().BeFalse();
        PayoutEligibilityEvaluator.Evaluate(earned, ConfirmedAt.AddDays(120), [], active)
            .IsEligible.Should().BeTrue();
        PayoutEligibilityEvaluator.Evaluate(purchased, DateTimeOffset.MaxValue, [], active)
            .Reasons.Should().Contain(PayoutIneligibilityReason.NonCashableProvenance);
        PayoutEligibilityEvaluator.Evaluate(soft, DateTimeOffset.MaxValue, [], active)
            .Reasons.Should().Contain(PayoutIneligibilityReason.NonCashableCurrency);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void ActiveHoldBlocksPayoutBeforeAtAndAfterMaturity(int dayOffset)
    {
        var wallet = WalletId.New();
        var lot = Lot(wallet, CurrencyCode.HardCoin, ProvenanceKind.EarnedHard, ConfirmedAt.AddDays(120));
        var hold = new HoldContract(
            HoldId.New(), wallet, new CoinAmount(CurrencyCode.HardCoin, 1), HoldReason.RiskReview,
            HoldStatus.Active, ConfirmedAt.AddDays(100), null);

        PayoutEligibilityEvaluator.Evaluate(
                lot,
                ConfirmedAt.AddDays(120 + dayOffset),
                [hold],
                new WalletRestrictionSnapshot(wallet, WalletLifecycleState.Active, 0))
            .Reasons.Should().Contain(PayoutIneligibilityReason.ActiveHold);
    }

    [Fact]
    public void FutureHoldDoesNotBlockAnEarlierEligiblePayoutDecision()
    {
        var wallet = WalletId.New();
        var lot = Lot(wallet, CurrencyCode.HardCoin, ProvenanceKind.EarnedHard, ConfirmedAt);
        var future = new HoldContract(
            HoldId.New(), wallet, new CoinAmount(CurrencyCode.HardCoin, 1), HoldReason.RiskReview,
            HoldStatus.Active, ConfirmedAt.AddDays(1), null);

        PayoutEligibilityEvaluator.Evaluate(
                lot,
                ConfirmedAt,
                [future],
                new WalletRestrictionSnapshot(wallet, WalletLifecycleState.Active, 0))
            .IsEligible.Should().BeTrue();
    }

    [Fact]
    public void FreezeAndDebtDenyEveryProtectedValueMovement()
    {
        var wallet = WalletId.New();
        var frozen = new WalletRestrictionSnapshot(wallet, WalletLifecycleState.Frozen, 0);
        var indebted = new WalletRestrictionSnapshot(wallet, WalletLifecycleState.Active, 1);

        foreach (var operation in Enum.GetValues<ProtectedValueOperation>())
        {
            WalletRestrictionEvaluator.Evaluate(frozen, operation).IsAllowed.Should().BeFalse();
            WalletRestrictionEvaluator.Evaluate(indebted, operation).IsAllowed.Should().BeFalse();
        }

        WalletRestrictionEvaluator.Evaluate(
            new WalletRestrictionSnapshot(wallet, WalletLifecycleState.Active, 0),
            ProtectedValueOperation.Spend).IsAllowed.Should().BeTrue();
        typeof(CreditLotMaturity).GetMethods().Select(method => method.Name)
            .Should().NotContain(name => name.Contains("Accelerate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RestrictionAndPayoutContractsRejectInvalidOrMismatchedState()
    {
        var wallet = WalletId.New();
        FluentActions.Invoking(() => new WalletRestrictionSnapshot(wallet, (WalletLifecycleState)999, 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new WalletRestrictionSnapshot(wallet, WalletLifecycleState.Active, -1))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => WalletRestrictionEvaluator.Evaluate(null!, ProtectedValueOperation.Spend))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => WalletRestrictionEvaluator.Evaluate(
                new WalletRestrictionSnapshot(wallet, WalletLifecycleState.Active, 0),
                (ProtectedValueOperation)999))
            .Should().Throw<ArgumentOutOfRangeException>();

        var lot = Lot(wallet, CurrencyCode.HardCoin, ProvenanceKind.EarnedHard, ConfirmedAt);
        FluentActions.Invoking(() => PayoutEligibilityEvaluator.Evaluate(
                lot,
                ConfirmedAt,
                [],
                new WalletRestrictionSnapshot(WalletId.New(), WalletLifecycleState.Active, 0)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PayoutEligibilityReportsTerminalLotAccountAndDebtRestrictions()
    {
        var wallet = WalletId.New();
        var scale = CurrencyTraceScale.For(CurrencyCode.HardCoin);
        var heldLot = new CreditLot(
            CreditLotId.New(), wallet, new CoinAmount(CurrencyCode.HardCoin, 10), ProvenanceKind.EarnedHard,
            ConfirmedAt, ConfirmedAt, 1, CreditLotState.Held,
            [new RootTraceRange(SourceStampId.New(), 0, 10 * scale, 0)], scale);

        var decision = PayoutEligibilityEvaluator.Evaluate(
            heldLot,
            ConfirmedAt,
            [],
            new WalletRestrictionSnapshot(wallet, WalletLifecycleState.UnderReview, 5));

        decision.Reasons.Should().Contain(PayoutIneligibilityReason.LotNotActive);
        decision.Reasons.Should().Contain(PayoutIneligibilityReason.AccountRestricted);
        decision.Reasons.Should().Contain(PayoutIneligibilityReason.OutstandingDebt);
    }

    private static SourceEvidence Confirmed(string reference, DateTimeOffset confirmedAt) =>
        SourceEvidence.Observe(SourceStampId.New(), "platform", reference, "evidence", ObservedAt)
            .Confirm(confirmedAt);

    private static CreditLot Lot(
        WalletId wallet,
        CurrencyCode currency,
        ProvenanceKind provenance,
        DateTimeOffset maturesAt)
    {
        var scale = CurrencyTraceScale.For(currency);
        return new CreditLot(
            CreditLotId.New(), wallet, new CoinAmount(currency, 10), provenance,
            ConfirmedAt, maturesAt, 1, CreditLotState.Active,
            [new RootTraceRange(SourceStampId.New(), 0, 10 * scale, 0)], scale);
    }
}
