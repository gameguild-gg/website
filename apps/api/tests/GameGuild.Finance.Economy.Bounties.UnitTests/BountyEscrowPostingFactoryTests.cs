using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Posting;

namespace GameGuild.Finance.Economy.Bounties.UnitTests;

public sealed class BountyEscrowPostingFactoryTests
{
    private static readonly DateTimeOffset PostedAt = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_PartitionsMixedHardCoinProvenanceIntoImmutableDebitLegs()
    {
        var posterId = Guid.NewGuid();
        var posterWallet = WalletId.New();
        var position = CreatePosition(posterId, posterWallet);

        var request = BountyEscrowPostingFactory.Create(
            position,
            PostingId.New(),
            Authority(posterId),
            new ReserveVersion(3),
            new PolicyVersion(7));

        request.Posting.Template.Kind.Should().Be(PostingTemplateKind.BountyEscrow);
        request.Posting.Lines.Should().HaveCount(3);
        request.Posting.Lines.Should().ContainSingle(line =>
            line.Side == EntrySide.Debit &&
            line.Account == EconomyAccountCode.PurchasedHardLiability &&
            line.Provenance == ProvenanceKind.PurchasedHard &&
            line.Amount.Units == 6);
        request.Posting.Lines.Should().ContainSingle(line =>
            line.Side == EntrySide.Debit &&
            line.Account == EconomyAccountCode.EarnedHardLiability &&
            line.Provenance == ProvenanceKind.EarnedHard &&
            line.Amount.Units == 4);
        request.Posting.Lines.Should().ContainSingle(line =>
            line.Side == EntrySide.Credit &&
            line.Account == EconomyAccountCode.HardCoinEscrow &&
            line.Amount.Units == 10);
        request.Allocations.Should().HaveCount(2);
        request.Allocations.Select(allocation => allocation.AmountUnits).Should().BeEquivalentTo([6L, 4L]);
    }

    [Fact]
    public void Create_RejectsAuthorityForAnotherPoster()
    {
        var posterId = Guid.NewGuid();
        var position = CreatePosition(posterId, WalletId.New());

        FluentActions.Invoking(() => BountyEscrowPostingFactory.Create(
                position,
                PostingId.New(),
                Authority(Guid.NewGuid()),
                new ReserveVersion(3),
                new PolicyVersion(7)))
            .Should().Throw<ArgumentException>()
            .WithMessage("*posting user*");
    }

    [Fact]
    public void PostingMatrix_RejectsHardCoinProvenanceOnSoftCoinEscrow()
    {
        var request = new PostingRequest(
            PostingId.New(),
            new PostingTemplate(PostingTemplateKind.BountyEscrow, PostingTemplate.CurrentVersion),
            new IdempotencyKey("soft-bounty-escrow"),
            PostingAuthority.WalletOwner,
            new ReserveVersion(3),
            new PolicyVersion(7),
            null,
            PostedAt,
            [
                new PostingLine(1, EntrySide.Debit, EconomyAccountCode.SoftCoinLiability,
                    new CoinAmount(CurrencyCode.SoftCoin, 100), WalletId.New(), null, ProvenanceKind.PurchasedHard),
                new PostingLine(2, EntrySide.Credit, EconomyAccountCode.SoftCoinEscrow,
                    new CoinAmount(CurrencyCode.SoftCoin, 100), null, null, null)
            ]);

        PostingMatrix.Validate(request).Errors
            .Should().Contain(error => error.Code == PostingErrorCode.InvalidProvenance);
    }

    private static BountyEscrowPosition CreatePosition(Guid posterId, WalletId posterWallet)
    {
        var purchase = Lot(posterWallet, 6, ProvenanceKind.PurchasedHard, 1);
        var earned = Lot(posterWallet, 4, ProvenanceKind.EarnedHard, 2);
        return BountyEscrowPositionFactory.Create(new PostBountyCommand(
            BountyId.New(),
            posterId,
            posterWallet,
            WalletId.New(),
            new CoinAmount(CurrencyCode.HardCoin, 10),
            [purchase, earned],
            BountyEligibilityRequirements.None,
            0,
            PostedAt,
            PostedAt.AddDays(14),
            new IdempotencyKey($"bounty-{Guid.NewGuid():N}")));
    }

    private static RegisteredPostingAuthority Authority(Guid actorId) => new(
        Guid.NewGuid(), actorId, Guid.NewGuid(), Guid.NewGuid(), "bounty-post", 1);

    private static CreditLot Lot(WalletId walletId, long units, ProvenanceKind provenance, long sequence)
    {
        var root = SourceStampId.New();
        return new CreditLot(
            CreditLotId.New(),
            walletId,
            new CoinAmount(CurrencyCode.HardCoin, units),
            provenance,
            PostedAt.AddDays(-10),
            provenance == ProvenanceKind.EarnedHard ? PostedAt.AddDays(110) : PostedAt.AddDays(-10),
            sequence,
            CreditLotState.Active,
            [new RootTraceRange(root, 0, units * CurrencyTraceScale.HardCoinTraceUnitsPerCoin, 0)],
            CurrencyTraceScale.HardCoinTraceUnitsPerCoin);
    }

}
