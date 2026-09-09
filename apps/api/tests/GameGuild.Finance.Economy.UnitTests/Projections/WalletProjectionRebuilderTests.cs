using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Projections;

namespace GameGuild.Finance.Economy.UnitTests.Projections;

public sealed class WalletProjectionRebuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RebuildDerivesEveryBalanceDimensionFromImmutableFacts()
    {
        var wallet = WalletId.New();
        var purchased = Lot(wallet, 100, CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, Now.AddDays(-200), Now.AddDays(-200));
        var immatureEarned = Lot(wallet, 50, CurrencyCode.HardCoin, ProvenanceKind.EarnedHard, Now.AddDays(-10), Now.AddDays(110));
        var matureEarned = Lot(wallet, 30, CurrencyCode.HardCoin, ProvenanceKind.EarnedHard, Now.AddDays(-130), Now.AddDays(-10));
        var heldEarned = Lot(wallet, 10, CurrencyCode.HardCoin, ProvenanceKind.EarnedHard, Now.AddDays(-130), Now.AddDays(-10), CreditLotState.Held);
        var soft = Lot(wallet, 200, CurrencyCode.SoftCoin, ProvenanceKind.AdRewardSoft, Now.AddDays(-5), Now.AddDays(-5));
        var disputedSoft = Lot(wallet, 40, CurrencyCode.SoftCoin, ProvenanceKind.ConvertedSoft, Now.AddDays(-5), Now.AddDays(-5));
        var consumption = new FragmentConsumption(
            PostingId.New(),
            purchased.Id,
            new CoinAmount(CurrencyCode.HardCoin, 20),
            [new RootTraceRange(purchased.Ranges[0].Root, 0, 20, 0)]);
        var retirement = new FragmentRetirement(
            PostingId.New(),
            new CoinAmount(CurrencyCode.HardCoin, 5),
            [new ParentFragmentLineage(purchased.Id, new CoinAmount(CurrencyCode.HardCoin, 5), [])]);
        var activeHold = new HoldContract(
            HoldId.New(), wallet, new CoinAmount(CurrencyCode.HardCoin, 5),
            HoldReason.RiskReview, HoldStatus.Active, Now.AddDays(-1), null);
        var releasedHold = new HoldContract(
            HoldId.New(), wallet, new CoinAmount(CurrencyCode.HardCoin, 99),
            HoldReason.Dispute, HoldStatus.Released, Now.AddDays(-2), Now.AddDays(-1));
        var input = new WalletProjectionRebuildInput(
            wallet,
            [
                new PendingFundingClaim(wallet, new CoinAmount(CurrencyCode.HardCoin, 5), SourceConfirmationState.Observed),
                new PendingFundingClaim(wallet, new CoinAmount(CurrencyCode.SoftCoin, 7), SourceConfirmationState.Observed),
                new PendingFundingClaim(wallet, new CoinAmount(CurrencyCode.HardCoin, 90), SourceConfirmationState.Failed)
            ],
            [purchased, immatureEarned, matureEarned, heldEarned, soft, disputedSoft],
            [consumption],
            [retirement],
            [activeHold, releasedHold],
            [new FragmentReservation(purchased.Id, new CoinAmount(CurrencyCode.HardCoin, 10), true)],
            [disputedSoft.Id],
            Now);

        var projection = WalletProjectionRebuilder.Rebuild(input);

        projection.PendingHard.Should().Be(5);
        projection.PendingSoft.Should().Be(7);
        projection.PurchasedHard.Should().Be(75);
        projection.EarnedHard.Should().Be(90);
        projection.RestrictedHard.Should().Be(0);
        projection.Soft.Should().Be(240);
        projection.HardConfirmed.Should().Be(165);
        projection.HardTotal.Should().Be(170);
        projection.SoftTotal.Should().Be(247);
        projection.ImmatureEarnedHard.Should().Be(50);
        projection.HeldHard.Should().Be(15);
        projection.HeldSoft.Should().Be(40);
        projection.AvailableHardToSpend.Should().Be(140);
        projection.AvailableSoftToSpend.Should().Be(200);
        projection.WithdrawableHard.Should().Be(25);
    }

    [Fact]
    public void PendingClaimsAreVisibleButNeverSpendableAndTerminalLotsAreExcluded()
    {
        var wallet = WalletId.New();
        var reversed = Lot(wallet, 50, CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, Now, Now, CreditLotState.Reversed);
        var input = Empty(wallet) with
        {
            PendingClaims =
            [
                new PendingFundingClaim(wallet, new CoinAmount(CurrencyCode.HardCoin, 25), SourceConfirmationState.Observed)
            ],
            CreditLots = [reversed]
        };

        var projection = WalletProjectionRebuilder.Rebuild(input);

        projection.HardTotal.Should().Be(25);
        projection.HardConfirmed.Should().Be(0);
        projection.AvailableHardToSpend.Should().Be(0);
        projection.WithdrawableHard.Should().Be(0);
    }

    [Fact]
    public void RebuildClassifiesRestrictedHardAndIgnoresInactiveReservations()
    {
        var wallet = WalletId.New();
        var lot = Lot(
            wallet,
            12,
            CurrencyCode.HardCoin,
            ProvenanceKind.RefundRestoration,
            Now.AddDays(-130),
            Now.AddDays(-10));

        var projection = WalletProjectionRebuilder.Rebuild(Empty(wallet) with
        {
            CreditLots = [lot],
            Reservations = [new FragmentReservation(lot.Id, new CoinAmount(CurrencyCode.HardCoin, 8), false)]
        });

        projection.RestrictedHard.Should().Be(12);
        projection.AvailableHardToSpend.Should().Be(12);
        projection.WithdrawableHard.Should().Be(0);
    }

    [Fact]
    public void RebuildRejectsProjectionArithmeticOverflow()
    {
        var wallet = WalletId.New();
        var first = Lot(wallet, long.MaxValue, CurrencyCode.SoftCoin, ProvenanceKind.AdRewardSoft, Now, Now);
        var second = Lot(wallet, 1, CurrencyCode.SoftCoin, ProvenanceKind.AdRewardSoft, Now, Now);

        FluentActions.Invoking(() => WalletProjectionRebuilder.Rebuild(Empty(wallet) with
            {
                CreditLots = [first, second]
            }))
            .Should().Throw<ProjectionCorruptionException>()
            .WithMessage("Projection arithmetic overflowed:*");
    }

    [Fact]
    public void RebuildRejectsOverConsumedReservedOrWrongCurrencyFacts()
    {
        var wallet = WalletId.New();
        var lot = Lot(wallet, 5, CurrencyCode.HardCoin, ProvenanceKind.PurchasedHard, Now, Now);

        FluentActions.Invoking(() => WalletProjectionRebuilder.Rebuild(Empty(wallet) with
            {
                CreditLots = [lot],
                Consumptions =
                [
                    new FragmentConsumption(
                        PostingId.New(), lot.Id, new CoinAmount(CurrencyCode.HardCoin, 6),
                        [new RootTraceRange(lot.Ranges[0].Root, 0, 5, 0)])
                ]
            }))
            .Should().Throw<ProjectionCorruptionException>();
        FluentActions.Invoking(() => WalletProjectionRebuilder.Rebuild(Empty(wallet) with
            {
                CreditLots = [lot],
                Reservations = [new FragmentReservation(lot.Id, new CoinAmount(CurrencyCode.HardCoin, 6), true)]
            }))
            .Should().Throw<ProjectionCorruptionException>();
        FluentActions.Invoking(() => WalletProjectionRebuilder.Rebuild(Empty(wallet) with
            {
                CreditLots = [lot],
                Reservations = [new FragmentReservation(lot.Id, new CoinAmount(CurrencyCode.SoftCoin, 1), true)]
            }))
            .Should().Throw<ProjectionCorruptionException>();
    }

    [Fact]
    public void ContractsRejectInvalidInputAndExposeNoMutableBalanceSetter()
    {
        var wallet = WalletId.New();

        FluentActions.Invoking(() => new PendingFundingClaim(
                wallet, new CoinAmount(CurrencyCode.HardCoin, 0), SourceConfirmationState.Observed))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new PendingFundingClaim(
                wallet, new CoinAmount(CurrencyCode.HardCoin, 1), (SourceConfirmationState)999))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new FragmentReservation(
                CreditLotId.New(), new CoinAmount(CurrencyCode.HardCoin, 0), true))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => WalletProjectionRebuilder.Rebuild(null!))
            .Should().Throw<ArgumentNullException>();

        typeof(WalletBalanceProjection).GetProperties()
            .Should().OnlyContain(property => property.SetMethod == null);
        typeof(WalletBalanceProjection).GetProperty("Balance").Should().BeNull();
    }

    private static WalletProjectionRebuildInput Empty(WalletId wallet) =>
        new(wallet, [], [], [], [], [], [], [], Now);

    private static CreditLot Lot(
        WalletId wallet,
        long units,
        CurrencyCode currency,
        ProvenanceKind provenance,
        DateTimeOffset confirmedAt,
        DateTimeOffset maturesAt,
        CreditLotState state = CreditLotState.Active)
    {
        var scale = CurrencyTraceScale.For(currency);
        return new CreditLot(
            CreditLotId.New(), wallet, new CoinAmount(currency, units), provenance,
            confirmedAt, maturesAt, units, state,
            [new RootTraceRange(SourceStampId.New(), 0, units * scale, 0)], scale);
    }
}
