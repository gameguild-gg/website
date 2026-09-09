using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.Bounties;

/// <summary>
/// Builds an escrow position from server-authoritative lots. Persistence workflows use this
/// same selection policy as the legacy in-memory coordinator, without owning any state.
/// </summary>
public static class BountyEscrowPositionFactory
{
    public static BountyEscrowPosition Create(PostBountyCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.AvailableLots);
        ArgumentNullException.ThrowIfNull(command.Eligibility);
        if (command.PosterId == Guid.Empty)
            throw new ArgumentException("Poster ID cannot be empty.", nameof(command));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.Amount.Units);
        if (command.PosterWalletId == command.EscrowWalletId)
            throw new ArgumentException("Poster and escrow wallets must be distinct.", nameof(command));
        if (command.ExpiresAt <= command.PostedAt)
            throw new ArgumentException("Bounty expiry must follow posting.", nameof(command));
        if (command.AvailableLots.GroupBy(lot => lot.Id).Any(group => group.Count() > 1))
            throw new ArgumentException("Available bounty lots must have unique identities.", nameof(command));
        _ = BountyFeePolicy.Calculate(command.Amount.Units, command.ReclaimFeePpm);

        var posterLots = command.AvailableLots
            .Where(lot => lot.WalletId == command.PosterWalletId && lot.ConfirmedAt <= command.PostedAt)
            .ToArray();
        var selection = FifoFragmentSelector.Select(posterLots, command.Amount);
        var parentLots = posterLots.ToDictionary(lot => lot.Id);
        var fragments = selection.Selections
            .Select(item => new BountyEscrowFragment(parentLots[item.ParentLotId], item))
            .ToArray();

        return new BountyEscrowPosition(
            command.Id,
            command.PosterId,
            command.PosterWalletId,
            command.EscrowWalletId,
            command.Amount,
            fragments,
            command.Eligibility,
            command.ReclaimFeePpm,
            command.PostedAt,
            command.ExpiresAt);
    }
}
