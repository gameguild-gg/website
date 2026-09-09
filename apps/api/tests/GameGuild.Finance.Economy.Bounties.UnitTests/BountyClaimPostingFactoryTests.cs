using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.Bounties.UnitTests;

public sealed class BountyClaimPostingFactoryTests
{
    private static readonly DateTimeOffset ClaimedAt = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_BindsEveryPersistedEscrowLotToTheClaimDebit()
    {
        var claimantId = Guid.NewGuid();
        var claim = new DurableBountyClaimRequest(
            BountyId.New(),
            claimantId,
            WalletId.New(),
            ClaimedAt,
            new IdempotencyKey("bounty-claim"),
            "claim-evidence",
            Authority(claimantId),
            new ReserveVersion(2),
            new PolicyVersion(3));
        var escrow = CreateEscrow(claim.BountyId);

        var posting = BountyClaimPostingFactory.Create(escrow, claim);

        posting.Posting.Template.Kind.Should().Be(PostingTemplateKind.BountyClaim);
        posting.Posting.Authority.Should().Be(PostingAuthority.EscrowCoordinator);
        posting.Posting.Lines.Should().SatisfyRespectively(
            line =>
            {
                line.Side.Should().Be(EntrySide.Debit);
                line.Account.Should().Be(EconomyAccountCode.HardCoinEscrow);
                line.WalletId.Should().BeNull();
                line.Amount.Should().Be(escrow.Amount);
            },
            line =>
            {
                line.Side.Should().Be(EntrySide.Credit);
                line.Account.Should().Be(EconomyAccountCode.EarnedHardLiability);
                line.WalletId.Should().Be(claim.ClaimantWalletId);
                line.Provenance.Should().Be(ProvenanceKind.EarnedHard);
                line.Amount.Should().Be(escrow.Amount);
            });
        posting.Allocations.Should().HaveCount(2);
        posting.Allocations.Select(allocation => allocation.ParentLotId)
            .Should().BeEquivalentTo(escrow.Fragments.Select(fragment => fragment.EscrowLotId!.Value));
        posting.Allocations.Select(allocation => allocation.AmountUnits).Sum().Should().Be(escrow.Amount.Units);
    }

    [Fact]
    public void Create_RejectsBountyWithoutMaterializedEscrowLots()
    {
        var claimantId = Guid.NewGuid();
        var claim = new DurableBountyClaimRequest(
            BountyId.New(),
            claimantId,
            WalletId.New(),
            ClaimedAt,
            new IdempotencyKey("bounty-claim"),
            "claim-evidence",
            Authority(claimantId),
            new ReserveVersion(2),
            new PolicyVersion(3));
        var escrow = CreateEscrow(claim.BountyId) with
        {
            Fragments = [CreateFragment(5, null)]
        };

        FluentActions.Invoking(() => BountyClaimPostingFactory.Create(escrow, claim))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*materialized escrow lot*");
    }

    private static PersistedBountyEscrow CreateEscrow(BountyId bountyId) => new(
        bountyId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        WalletId.New(),
        WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 10),
        BountyEligibilityRequirements.None,
        0,
        BountyStatus.Open,
        new IdempotencyKey("bounty-post"),
        "post-hash",
        ClaimedAt.AddDays(-1),
        ClaimedAt.AddDays(1),
        1,
        [CreateFragment(5, CreditLotId.New()), CreateFragment(5, CreditLotId.New())]);

    private static PersistedBountyEscrowFragment CreateFragment(long units, CreditLotId? escrowLotId) => new(
        CreditLotId.New(),
        escrowLotId,
        new CoinAmount(CurrencyCode.HardCoin, units),
        ProvenanceKind.PurchasedHard,
        CurrencyTraceScale.HardCoinTraceUnitsPerCoin,
        [new RootTraceRange(SourceStampId.New(), 0, units * CurrencyTraceScale.HardCoinTraceUnitsPerCoin, 0)]);

    private static RegisteredPostingAuthority Authority(Guid actorId) => new(
        Guid.NewGuid(), actorId, Guid.NewGuid(), Guid.NewGuid(), "bounty-claim", 1);
}
