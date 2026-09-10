namespace GameGuild.Finance.Economy.Contracts;

public readonly record struct WalletId
{
    public WalletId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Wallet ID cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public static WalletId New() => new(Guid.NewGuid());
}

public readonly record struct SourceStampId
{
    public SourceStampId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Source stamp ID cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public static SourceStampId New() => new(Guid.NewGuid());
}

public readonly record struct CreditLotId
{
    public CreditLotId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Credit lot ID cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public static CreditLotId New() => new(Guid.NewGuid());
}

public readonly record struct HoldId
{
    public HoldId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Hold ID cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public static HoldId New() => new(Guid.NewGuid());
}

public readonly record struct IdempotencyKey
{
    public IdempotencyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        value = value.Trim();
        if (value.Length > 128) throw new ArgumentException("Idempotency key cannot exceed 128 characters.", nameof(value));
        Value = value;
    }

    public string Value { get; }
}

public readonly record struct PostingId
{
    public PostingId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Posting ID cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public static PostingId New() => new(Guid.NewGuid());
}

public readonly record struct ReserveVersion
{
    public ReserveVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public long Value { get; }
}

public readonly record struct PolicyVersion
{
    public PolicyVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public long Value { get; }
}
