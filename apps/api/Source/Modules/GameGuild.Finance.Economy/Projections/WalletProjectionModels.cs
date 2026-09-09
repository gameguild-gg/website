using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.Projections;

public sealed record PendingFundingClaim
{
    public PendingFundingClaim(WalletId walletId, CoinAmount amount, SourceConfirmationState state)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount.Units);
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        WalletId = walletId;
        Amount = amount;
        State = state;
    }

    public WalletId WalletId { get; }
    public CoinAmount Amount { get; }
    public SourceConfirmationState State { get; }
}

public sealed record FragmentReservation
{
    public FragmentReservation(CreditLotId lotId, CoinAmount amount, bool active)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount.Units);
        LotId = lotId;
        Amount = amount;
        Active = active;
    }

    public CreditLotId LotId { get; }
    public CoinAmount Amount { get; }
    public bool Active { get; }
}

public sealed record WalletProjectionRebuildInput(
    WalletId WalletId,
    IReadOnlyList<PendingFundingClaim> PendingClaims,
    IReadOnlyList<CreditLot> CreditLots,
    IReadOnlyList<FragmentConsumption> Consumptions,
    IReadOnlyList<FragmentRetirement> Retirements,
    IReadOnlyList<HoldContract> Holds,
    IReadOnlyList<FragmentReservation> Reservations,
    IReadOnlyCollection<CreditLotId> DisputedOrFrozenLots,
    DateTimeOffset AsOf);

public sealed record WalletBalanceProjection
{
    public WalletBalanceProjection(
        long pendingHard,
        long pendingSoft,
        long purchasedHard,
        long earnedHard,
        long restrictedHard,
        long soft,
        long immatureEarnedHard,
        long heldHard,
        long heldSoft,
        long availableHardToSpend,
        long availableSoftToSpend,
        long withdrawableHard)
    {
        PendingHard = pendingHard;
        PendingSoft = pendingSoft;
        PurchasedHard = purchasedHard;
        EarnedHard = earnedHard;
        RestrictedHard = restrictedHard;
        Soft = soft;
        ImmatureEarnedHard = immatureEarnedHard;
        HeldHard = heldHard;
        HeldSoft = heldSoft;
        AvailableHardToSpend = availableHardToSpend;
        AvailableSoftToSpend = availableSoftToSpend;
        WithdrawableHard = withdrawableHard;
    }

    public long PendingHard { get; }
    public long PendingSoft { get; }
    public long PurchasedHard { get; }
    public long EarnedHard { get; }
    public long RestrictedHard { get; }
    public long Soft { get; }
    public long ImmatureEarnedHard { get; }
    public long HeldHard { get; }
    public long HeldSoft { get; }
    public long AvailableHardToSpend { get; }
    public long AvailableSoftToSpend { get; }
    public long WithdrawableHard { get; }
    public long HardConfirmed => checked(PurchasedHard + EarnedHard + RestrictedHard);
    public long HardTotal => checked(HardConfirmed + PendingHard);
    public long SoftTotal => checked(Soft + PendingSoft);
}

public sealed class ProjectionCorruptionException : InvalidOperationException
{
    public ProjectionCorruptionException(string message) : base(message)
    {
    }
}
