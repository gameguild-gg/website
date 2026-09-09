using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.Policy;

public enum HoldEventKind
{
    Placed = 1,
    Released = 2,
    Consumed = 3
}

public sealed record HoldEvent(
    long Sequence,
    HoldEventKind Kind,
    HoldId HoldId,
    WalletId WalletId,
    CoinAmount Amount,
    HoldReason Reason,
    DateTimeOffset OccurredAt);

public sealed class AppendOnlyHoldLedger
{
    private readonly InMemoryLedgerKernelStore _store;

    public AppendOnlyHoldLedger(InMemoryLedgerKernelStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public IReadOnlyList<HoldEvent> Events
    {
        get => _store.HoldEvents;
    }

    public HoldContract Place(
        HoldId id,
        WalletId walletId,
        CoinAmount amount,
        HoldReason reason,
        DateTimeOffset effectiveAt)
        => _store.Execute(transaction => transaction.PlaceHold(id, walletId, amount, reason, effectiveAt));

    public HoldContract Release(HoldId id, DateTimeOffset releasedAt) =>
        _store.Execute(transaction => transaction.TransitionHold(
            id, HoldStatus.Released, HoldEventKind.Released, releasedAt));

    public HoldContract Consume(HoldId id, DateTimeOffset consumedAt) =>
        _store.Execute(transaction => transaction.TransitionHold(
            id, HoldStatus.Consumed, HoldEventKind.Consumed, consumedAt));

    public HoldContract Current(HoldId id)
        => _store.GetHold(id);

    public IReadOnlyList<HoldContract> ActiveFor(WalletId walletId)
        => _store.GetActiveHolds(walletId);
}
