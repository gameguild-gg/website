using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.Bounties.UnitTests;

public sealed class BountyReclaimPostingFactoryTests
{
    private static readonly DateTimeOffset ReclaimedAt = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_PreservesHardProvenanceAndPartitionsTheFeeIntoExactEscrowRanges()
    {
        var posterId = Guid.NewGuid();
        var posterWalletId = WalletId.New();
        var request = new DurableBountyReclaimRequest(
            BountyId.New(),
            posterId,
            posterWalletId,
            ReclaimedAt,
            new IdempotencyKey("bounty-reclaim"),
            Authority(posterId),
            new ReserveVersion(2),
            new PolicyVersion(3));
        var escrow = CreateEscrow(request.BountyId, posterId, posterWalletId, CurrencyCode.HardCoin, 200_000,
            [Fragment(5, ProvenanceKind.PurchasedHard), Fragment(5, ProvenanceKind.EarnedHard)]);

        var posting = BountyReclaimPostingFactory.Create(escrow, request);

        posting.Posting.Template.Kind.Should().Be(PostingTemplateKind.BountyReclaim);
        posting.Posting.Authority.Should().Be(PostingAuthority.EscrowCoordinator);
        posting.Posting.Lines.Where(line => line.Side == EntrySide.Debit)
            .Select(line => line.Amount.Units).Should().Equal(5, 3, 2);
        posting.Posting.Lines.Where(line => line.Side == EntrySide.Credit && line.WalletId == posterWalletId)
            .Should().HaveCount(2).And.OnlyContain(line =>
                (line.Account == EconomyAccountCode.PurchasedHardLiability ||
                 line.Account == EconomyAccountCode.EarnedHardLiability) &&
                (line.Provenance == ProvenanceKind.PurchasedHard ||
                 line.Provenance == ProvenanceKind.EarnedHard));
        posting.Posting.Lines.Where(line => line.Side == EntrySide.Credit && line.WalletId == posterWalletId)
            .Sum(line => line.Amount.Units).Should().Be(8);
        posting.Posting.Lines.Should().ContainSingle(line =>
            line.Side == EntrySide.Credit && line.Account == EconomyAccountCode.FeeRevenueHard &&
            line.Amount.Units == 2 && line.WalletId == null && line.Provenance == null);
        posting.Allocations.Select(item => item.AmountUnits).Sum().Should().Be(10);
        posting.Allocations.Should().HaveCount(3);
        posting.Allocations.Last().RootRanges.Should().ContainSingle().Which.Length
            .Should().Be(2 * CurrencyTraceScale.HardCoinTraceUnitsPerCoin);
    }

    [Fact]
    public void Create_BurnsSoftFeeAndRestoresTheOriginalSoftProvenance()
    {
        var posterId = Guid.NewGuid();
        var posterWalletId = WalletId.New();
        var request = new DurableBountyReclaimRequest(
            BountyId.New(),
            posterId,
            posterWalletId,
            ReclaimedAt,
            new IdempotencyKey("soft-bounty-reclaim"),
            Authority(posterId),
            new ReserveVersion(2),
            new PolicyVersion(3));
        var escrow = CreateEscrow(request.BountyId, posterId, posterWalletId, CurrencyCode.SoftCoin, 100_000,
            [Fragment(10, ProvenanceKind.AdRewardSoft)]);

        var posting = BountyReclaimPostingFactory.Create(escrow, request);

        posting.Posting.Lines.Should().SatisfyRespectively(
            line => AssertDebit(line, EconomyAccountCode.SoftCoinEscrow, 9),
            line => AssertReturn(line, posterWalletId, EconomyAccountCode.SoftCoinLiability,
                ProvenanceKind.AdRewardSoft, 9),
            line => AssertDebit(line, EconomyAccountCode.SoftCoinEscrow, 1),
            line =>
            {
                line.Side.Should().Be(EntrySide.Credit);
                line.Account.Should().Be(EconomyAccountCode.SoftCoinReserve);
                line.Amount.Units.Should().Be(1);
                line.WalletId.Should().BeNull();
                line.Provenance.Should().BeNull();
            });
    }

    [Fact]
    public void Create_AllowsMultipleFeePairsAfterAllReturnPairs()
    {
        var posterId = Guid.NewGuid();
        var posterWalletId = WalletId.New();
        var request = new DurableBountyReclaimRequest(
            BountyId.New(), posterId, posterWalletId, ReclaimedAt,
            new IdempotencyKey("multi-fee-bounty-reclaim"), Authority(posterId),
            new ReserveVersion(2), new PolicyVersion(3));
        var escrow = CreateEscrow(
            request.BountyId,
            posterId,
            posterWalletId,
            CurrencyCode.HardCoin,
            200_000,
            Enumerable.Range(0, 10).Select(_ => Fragment(1, ProvenanceKind.PurchasedHard)).ToArray());

        var posting = BountyReclaimPostingFactory.Create(escrow, request);

        posting.Posting.Lines.Should().HaveCount(20);
        posting.Posting.Lines
            .Where(line => line.Side == EntrySide.Credit && line.WalletId == posterWalletId)
            .Should().HaveCount(8);
        posting.Posting.Lines
            .Where(line => line.Side == EntrySide.Credit && line.Account == EconomyAccountCode.FeeRevenueHard)
            .Should().HaveCount(2);
    }

    [Fact]
    public void Create_RejectsAReclaimBeforeTheBountyExpires()
    {
        var posterId = Guid.NewGuid();
        var posterWalletId = WalletId.New();
        var request = new DurableBountyReclaimRequest(
            BountyId.New(), posterId, posterWalletId, ReclaimedAt,
            new IdempotencyKey("premature-reclaim"), Authority(posterId),
            new ReserveVersion(2), new PolicyVersion(3));
        var escrow = CreateEscrow(request.BountyId, posterId, posterWalletId, CurrencyCode.HardCoin, 0,
            [Fragment(10, ProvenanceKind.PurchasedHard)]) with { ExpiresAt = ReclaimedAt.AddMinutes(1) };

        FluentActions.Invoking(() => BountyReclaimPostingFactory.Create(escrow, request))
            .Should().Throw<BountyNotExpiredException>();
    }

    private static PersistedBountyEscrow CreateEscrow(
        BountyId bountyId,
        Guid posterId,
        WalletId posterWalletId,
        CurrencyCode currency,
        int feePpm,
        IReadOnlyList<PersistedBountyEscrowFragment> fragments) => new(
        bountyId,
        Guid.NewGuid(),
        posterId,
        posterWalletId,
        WalletId.New(),
        new CoinAmount(currency, fragments.Sum(item => item.Amount.Units)),
        BountyEligibilityRequirements.None,
        feePpm,
        BountyStatus.Open,
        new IdempotencyKey($"post-{bountyId.Value:N}"),
        "post-hash",
        ReclaimedAt.AddDays(-2),
        ReclaimedAt.AddDays(-1),
        1,
        fragments);

    private static PersistedBountyEscrowFragment Fragment(long units, ProvenanceKind provenance) => new(
        CreditLotId.New(),
        CreditLotId.New(),
        new CoinAmount(provenance is ProvenanceKind.PurchasedHard or ProvenanceKind.EarnedHard
            ? CurrencyCode.HardCoin
            : CurrencyCode.SoftCoin, units),
        provenance,
        provenance is ProvenanceKind.PurchasedHard or ProvenanceKind.EarnedHard
            ? CurrencyTraceScale.HardCoinTraceUnitsPerCoin
            : CurrencyTraceScale.SoftCoinTraceUnitsPerCoin,
        [new RootTraceRange(
            SourceStampId.New(),
            0,
            units * (provenance is ProvenanceKind.PurchasedHard or ProvenanceKind.EarnedHard
                ? CurrencyTraceScale.HardCoinTraceUnitsPerCoin
                : CurrencyTraceScale.SoftCoinTraceUnitsPerCoin),
            0)]);

    private static RegisteredPostingAuthority Authority(Guid actorId) => new(
        Guid.NewGuid(), actorId, Guid.NewGuid(), Guid.NewGuid(), "bounty-reclaim", 1);

    private static void AssertDebit(PostingLine line, EconomyAccountCode account, long units)
    {
        line.Side.Should().Be(EntrySide.Debit);
        line.Account.Should().Be(account);
        line.Amount.Units.Should().Be(units);
        line.WalletId.Should().BeNull();
        line.Provenance.Should().BeNull();
    }

    private static void AssertReturn(
        PostingLine line,
        WalletId walletId,
        EconomyAccountCode account,
        ProvenanceKind provenance,
        long units)
    {
        line.Side.Should().Be(EntrySide.Credit);
        line.Account.Should().Be(account);
        line.Amount.Units.Should().Be(units);
        line.WalletId.Should().Be(walletId);
        line.Provenance.Should().Be(provenance);
    }
}
