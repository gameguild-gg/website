namespace GameGuild.Finance.Economy.Contracts;

public sealed record HoldContract
{
    public HoldContract(
        HoldId id,
        WalletId walletId,
        CoinAmount amount,
        HoldReason reason,
        HoldStatus status,
        DateTimeOffset effectiveAt,
        DateTimeOffset? releasedAt)
    {
        if (amount.Units == 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (!Enum.IsDefined(reason)) throw new ArgumentOutOfRangeException(nameof(reason));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (status == HoldStatus.Active && releasedAt is not null)
            throw new ArgumentException("Active holds cannot have a release timestamp.", nameof(releasedAt));
        if (status != HoldStatus.Active && releasedAt is null)
            throw new ArgumentException("Terminal holds require a release timestamp.", nameof(releasedAt));
        if (releasedAt < effectiveAt)
            throw new ArgumentException("A hold cannot be released before it becomes effective.", nameof(releasedAt));

        Id = id;
        WalletId = walletId;
        Amount = amount;
        Reason = reason;
        Status = status;
        EffectiveAt = effectiveAt;
        ReleasedAt = releasedAt;
    }

    public HoldId Id { get; }
    public WalletId WalletId { get; }
    public CoinAmount Amount { get; }
    public HoldReason Reason { get; }
    public HoldStatus Status { get; }
    public DateTimeOffset EffectiveAt { get; }
    public DateTimeOffset? ReleasedAt { get; }
}
