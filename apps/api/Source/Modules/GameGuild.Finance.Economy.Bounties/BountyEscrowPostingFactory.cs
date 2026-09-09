using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Posting;

namespace GameGuild.Finance.Economy.Bounties;

/// <summary>
/// Builds the one immutable ledger posting that moves a posted bounty into the platform escrow
/// account. Every debit stays partitioned by provenance, so mixed FIFO inputs never lose their
/// source-account or root-range identity.
/// </summary>
public static class BountyEscrowPostingFactory
{
    public static RegisteredPostingRequest Create(
        BountyEscrowPosition position,
        PostingId postingId,
        RegisteredPostingAuthority authority,
        ReserveVersion reserveVersion,
        PolicyVersion policyVersion,
        string? dispatchSnapshotHash = null)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(authority);
        if (authority.ActorId != position.PosterId)
            throw new ArgumentException("The bounty escrow authority must be the posting user.", nameof(authority));

        var groups = position.EscrowFragments
            .GroupBy(fragment => fragment.ParentLot.Provenance)
            .OrderBy(group => (int)group.Key)
            .ToArray();
        if (groups.Length == 0)
            throw new ArgumentException("Bounty escrow requires at least one FIFO fragment.", nameof(position));

        var lines = new List<PostingLine>(groups.Length + 1);
        var allocations = new List<RegisteredPostingAllocation>();
        var sequence = 1;
        foreach (var group in groups)
        {
            var amount = new CoinAmount(
                position.Amount.Currency,
                group.Sum(fragment => fragment.Amount.Units));
            lines.Add(new PostingLine(
                sequence,
                EntrySide.Debit,
                LiabilityFor(position.Amount.Currency, group.Key),
                amount,
                position.PosterWalletId,
                null,
                group.Key));

            foreach (var fragment in group)
            {
                allocations.Add(new RegisteredPostingAllocation(
                    sequence,
                    fragment.ParentLot.Id,
                    fragment.Amount.Units,
                    fragment.SelectedRanges));
            }

            sequence++;
        }

        lines.Add(new PostingLine(
            sequence,
            EntrySide.Credit,
            EscrowFor(position.Amount.Currency),
            position.Amount,
            null,
            null,
            null));

        var posting = new PostingRequest(
            postingId,
            new PostingTemplate(PostingTemplateKind.BountyEscrow, PostingTemplate.CurrentVersion),
            new IdempotencyKey($"{position.Id.Value:N}:escrow"),
            PostingAuthority.WalletOwner,
            reserveVersion,
            policyVersion,
            null,
            position.PostedAt,
            lines);
        PostingMatrix.EnsureValid(posting);
        return new RegisteredPostingRequest(authority, posting, allocations, dispatchSnapshotHash);
    }

    private static EconomyAccountCode LiabilityFor(CurrencyCode currency, ProvenanceKind provenance) =>
        currency switch
        {
            CurrencyCode.HardCoin when provenance == ProvenanceKind.EarnedHard => EconomyAccountCode.EarnedHardLiability,
            CurrencyCode.HardCoin => EconomyAccountCode.PurchasedHardLiability,
            CurrencyCode.SoftCoin => EconomyAccountCode.SoftCoinLiability,
            _ => throw new ArgumentOutOfRangeException(nameof(currency))
        };

    private static EconomyAccountCode EscrowFor(CurrencyCode currency) =>
        currency switch
        {
            CurrencyCode.HardCoin => EconomyAccountCode.HardCoinEscrow,
            CurrencyCode.SoftCoin => EconomyAccountCode.SoftCoinEscrow,
            _ => throw new ArgumentOutOfRangeException(nameof(currency))
        };
}
