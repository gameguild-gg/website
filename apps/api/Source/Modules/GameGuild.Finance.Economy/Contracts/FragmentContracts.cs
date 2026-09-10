namespace GameGuild.Finance.Economy.Contracts;

public sealed record RootMintContract
{
    public RootMintContract(
        SourceStampContract source,
        CreditLotId lotId,
        WalletId walletId,
        CoinAmount amount,
        ProvenanceKind provenance,
        DateTimeOffset confirmedAt,
        DateTimeOffset? maturesAt)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.State != SourceConfirmationState.Confirmed)
            throw new ArgumentException("Root mints require confirmed source evidence.", nameof(source));
        if (amount.Units == 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (!Enum.IsDefined(provenance)) throw new ArgumentOutOfRangeException(nameof(provenance));
        if (source.ConfirmedAt!.Value != confirmedAt)
            throw new ArgumentException("Root mint confirmation must match its source stamp.", nameof(confirmedAt));
        if (maturesAt < confirmedAt)
            throw new ArgumentException("Maturity cannot precede confirmation.", nameof(maturesAt));

        Source = source;
        LotId = lotId;
        WalletId = walletId;
        Amount = amount;
        Provenance = provenance;
        ConfirmedAt = confirmedAt;
        MaturesAt = maturesAt;
    }

    public SourceStampContract Source { get; }
    public CreditLotId LotId { get; }
    public WalletId WalletId { get; }
    public CoinAmount Amount { get; }
    public ProvenanceKind Provenance { get; }
    public DateTimeOffset ConfirmedAt { get; }
    public DateTimeOffset? MaturesAt { get; }
}

public readonly record struct FragmentAllocationContract
{
    public FragmentAllocationContract(CreditLotId parentLotId, long start, long length, WalletId destinationWalletId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        _ = checked(start + length);
        ParentLotId = parentLotId;
        Start = start;
        Length = length;
        DestinationWalletId = destinationWalletId;
    }

    public CreditLotId ParentLotId { get; }
    public long Start { get; }
    public long Length { get; }
    public WalletId DestinationWalletId { get; }
    public long EndExclusive => checked(Start + Length);
}

public sealed record FragmentLineageContract
{
    public FragmentLineageContract(CreditLotId outputLotId, IReadOnlyCollection<FragmentAllocationContract> parents)
    {
        ArgumentNullException.ThrowIfNull(parents);
        if (parents.Count == 0) throw new ArgumentException("Lineage requires at least one parent fragment.", nameof(parents));
        OutputLotId = outputLotId;
        Parents = parents.ToArray();
    }

    public CreditLotId OutputLotId { get; }
    public IReadOnlyList<FragmentAllocationContract> Parents { get; }
}
