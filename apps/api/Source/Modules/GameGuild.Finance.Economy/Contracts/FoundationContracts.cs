using GameGuild.Finance.Economy.Money;

namespace GameGuild.Finance.Economy.Contracts;

public sealed record WalletContract
{
    public WalletContract(WalletId id, Guid ownerId, Guid tenantId, WalletLifecycleState state, DateTimeOffset createdAt)
    {
        if (ownerId == Guid.Empty) throw new ArgumentException("Wallet owner ID cannot be empty.", nameof(ownerId));
        if (tenantId == Guid.Empty) throw new ArgumentException("Wallet tenant ID cannot be empty.", nameof(tenantId));
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        Id = id;
        OwnerId = ownerId;
        TenantId = tenantId;
        State = state;
        CreatedAt = createdAt;
    }

    public WalletId Id { get; }
    public Guid OwnerId { get; }
    public Guid TenantId { get; }
    public WalletLifecycleState State { get; }
    public DateTimeOffset CreatedAt { get; }
}

public sealed record ReserveSnapshotContract
{
    public ReserveSnapshotContract(
        ReserveVersion version,
        DateTimeOffset observedAt,
        DateTimeOffset expiresAt,
        HardCoinAmount hardHeadroom,
        SoftCoinAmount softHeadroom,
        string evidenceHash)
    {
        if (expiresAt <= observedAt) throw new ArgumentException("Reserve expiry must follow observation.", nameof(expiresAt));
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceHash);
        Version = version;
        ObservedAt = observedAt;
        ExpiresAt = expiresAt;
        HardHeadroom = hardHeadroom;
        SoftHeadroom = softHeadroom;
        EvidenceHash = evidenceHash.Trim();
    }

    public ReserveVersion Version { get; }
    public DateTimeOffset ObservedAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public HardCoinAmount HardHeadroom { get; }
    public SoftCoinAmount SoftHeadroom { get; }
    public string EvidenceHash { get; }
}

public sealed record MonetaryPolicyContract
{
    public MonetaryPolicyContract(
        PolicyVersion version,
        DateTimeOffset effectiveAt,
        DateTimeOffset? endsAt,
        int conversionFeePpm,
        int minimumMarginPpm)
    {
        if (endsAt <= effectiveAt) throw new ArgumentException("Policy end must follow its effective time.", nameof(endsAt));
        if (conversionFeePpm is < 0 or >= 1_000_000) throw new ArgumentOutOfRangeException(nameof(conversionFeePpm));
        if (minimumMarginPpm is < 0 or >= 1_000_000) throw new ArgumentOutOfRangeException(nameof(minimumMarginPpm));
        Version = version;
        EffectiveAt = effectiveAt;
        EndsAt = endsAt;
        ConversionFeePpm = conversionFeePpm;
        MinimumMarginPpm = minimumMarginPpm;
    }

    public PolicyVersion Version { get; }
    public DateTimeOffset EffectiveAt { get; }
    public DateTimeOffset? EndsAt { get; }
    public int ConversionFeePpm { get; }
    public int MinimumMarginPpm { get; }
}
