using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.Projections;

public static class WalletProjectionRebuilder
{
    public static WalletBalanceProjection Rebuild(WalletProjectionRebuildInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureCollections(input);

        var pendingHard = Pending(input, CurrencyCode.HardCoin);
        var pendingSoft = Pending(input, CurrencyCode.SoftCoin);
        var purchasedHard = 0L;
        var earnedHard = 0L;
        var restrictedHard = 0L;
        var soft = 0L;
        var immatureEarned = 0L;
        var lotHeldHard = 0L;
        var lotHeldSoft = 0L;
        var availableHard = 0L;
        var availableSoft = 0L;
        var withdrawableHard = 0L;

        var blockedLots = input.DisputedOrFrozenLots.ToHashSet();
        foreach (var lot in input.CreditLots.Where(lot => lot.WalletId == input.WalletId))
        {
            if (lot.State is CreditLotState.Consumed or CreditLotState.Reversed) continue;

            var remaining = RemainingUnits(lot, input.Consumptions, input.Retirements);
            var reserved = ReservedUnits(lot, remaining, input.Reservations);
            var blocked = lot.State == CreditLotState.Held || blockedLots.Contains(lot.Id);
            var available = blocked ? 0 : remaining - reserved;

            if (lot.Amount.Currency == CurrencyCode.SoftCoin)
            {
                soft = Add(soft, remaining);
                if (blocked) lotHeldSoft = Add(lotHeldSoft, remaining);
                availableSoft = Add(availableSoft, available);
                continue;
            }

            switch (lot.Provenance)
            {
                case ProvenanceKind.PurchasedHard:
                    purchasedHard = Add(purchasedHard, remaining);
                    break;
                case ProvenanceKind.EarnedHard:
                    earnedHard = Add(earnedHard, remaining);
                    if (input.AsOf < lot.OriginalMaturesAt)
                        immatureEarned = Add(immatureEarned, remaining);
                    break;
                default:
                    restrictedHard = Add(restrictedHard, remaining);
                    break;
            }

            if (blocked) lotHeldHard = Add(lotHeldHard, remaining);
            availableHard = Add(availableHard, available);
            if (!blocked && lot.Provenance == ProvenanceKind.EarnedHard && input.AsOf >= lot.OriginalMaturesAt)
                withdrawableHard = Add(withdrawableHard, available);
        }

        var activeHoldHard = ActiveHoldUnits(input, CurrencyCode.HardCoin);
        var activeHoldSoft = ActiveHoldUnits(input, CurrencyCode.SoftCoin);
        var hardConfirmed = Add(Add(purchasedHard, earnedHard), restrictedHard);
        var heldHard = Math.Min(hardConfirmed, Add(lotHeldHard, activeHoldHard));
        var heldSoft = Math.Min(soft, Add(lotHeldSoft, activeHoldSoft));
        availableHard = Math.Max(0, availableHard - activeHoldHard);
        availableSoft = Math.Max(0, availableSoft - activeHoldSoft);
        withdrawableHard = Math.Max(0, withdrawableHard - activeHoldHard);

        return new WalletBalanceProjection(
            pendingHard,
            pendingSoft,
            purchasedHard,
            earnedHard,
            restrictedHard,
            soft,
            immatureEarned,
            heldHard,
            heldSoft,
            availableHard,
            availableSoft,
            withdrawableHard);
    }

    private static void EnsureCollections(WalletProjectionRebuildInput input)
    {
        ArgumentNullException.ThrowIfNull(input.PendingClaims);
        ArgumentNullException.ThrowIfNull(input.CreditLots);
        ArgumentNullException.ThrowIfNull(input.Consumptions);
        ArgumentNullException.ThrowIfNull(input.Retirements);
        ArgumentNullException.ThrowIfNull(input.Holds);
        ArgumentNullException.ThrowIfNull(input.Reservations);
        ArgumentNullException.ThrowIfNull(input.DisputedOrFrozenLots);
    }

    private static long Pending(WalletProjectionRebuildInput input, CurrencyCode currency) =>
        input.PendingClaims
            .Where(claim => claim.WalletId == input.WalletId && claim.State == SourceConfirmationState.Observed &&
                            claim.Amount.Currency == currency)
            .Aggregate(0L, (total, claim) => Add(total, claim.Amount.Units));

    private static long RemainingUnits(
        CreditLot lot,
        IReadOnlyList<FragmentConsumption> consumptions,
        IReadOnlyList<FragmentRetirement> retirements)
    {
        var consumed = consumptions.Where(item => item.ParentLotId == lot.Id)
            .Aggregate(0L, (total, item) =>
            {
                EnsureCurrency(lot, item.Amount.Currency);
                return Add(total, item.Amount.Units);
            });
        var retired = retirements.SelectMany(item => item.Parents)
            .Where(parent => parent.ParentLotId == lot.Id)
            .Aggregate(0L, (total, parent) =>
            {
                EnsureCurrency(lot, parent.Amount.Currency);
                return Add(total, parent.Amount.Units);
            });
        var unavailable = Add(consumed, retired);
        if (unavailable > lot.Amount.Units)
            throw new ProjectionCorruptionException("A credit lot is over-consumed or over-retired.");
        return lot.Amount.Units - unavailable;
    }

    private static long ReservedUnits(
        CreditLot lot,
        long remaining,
        IReadOnlyList<FragmentReservation> reservations)
    {
        var reserved = reservations.Where(item => item.Active && item.LotId == lot.Id)
            .Aggregate(0L, (total, item) =>
            {
                EnsureCurrency(lot, item.Amount.Currency);
                return Add(total, item.Amount.Units);
            });
        if (reserved > remaining)
            throw new ProjectionCorruptionException("A credit lot is over-reserved.");
        return reserved;
    }

    private static long ActiveHoldUnits(WalletProjectionRebuildInput input, CurrencyCode currency) =>
        input.Holds
            .Where(hold => hold.WalletId == input.WalletId && hold.Status == HoldStatus.Active &&
                           hold.Amount.Currency == currency)
            .Aggregate(0L, (total, hold) => Add(total, hold.Amount.Units));

    private static void EnsureCurrency(CreditLot lot, CurrencyCode currency)
    {
        if (lot.Amount.Currency != currency)
            throw new ProjectionCorruptionException("A lot allocation uses a different currency than its parent.");
    }

    private static long Add(long left, long right)
    {
        try
        {
            return checked(left + right);
        }
        catch (OverflowException exception)
        {
            throw new ProjectionCorruptionException($"Projection arithmetic overflowed: {exception.Message}");
        }
    }
}
