namespace GameGuild.Finance.Economy.Contracts;

public readonly record struct PostingTemplate
{
    public const int CurrentVersion = 1;

    public PostingTemplate(PostingTemplateKind kind, int version)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        Kind = kind;
        Version = version;
    }

    public PostingTemplateKind Kind { get; }
    public int Version { get; }
}

public sealed record PostingLine
{
    public PostingLine(
        int sequence,
        EntrySide side,
        EconomyAccountCode account,
        CoinAmount amount,
        WalletId? walletId,
        CreditLotId? lotId,
        ProvenanceKind? provenance)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        if (!Enum.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));
        if (!Enum.IsDefined(account)) throw new ArgumentOutOfRangeException(nameof(account));
        if (amount.Units == 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (provenance.HasValue && !Enum.IsDefined(provenance.Value)) throw new ArgumentOutOfRangeException(nameof(provenance));
        Sequence = sequence;
        Side = side;
        Account = account;
        Amount = amount;
        WalletId = walletId;
        LotId = lotId;
        Provenance = provenance;
    }

    public int Sequence { get; init; }
    public EntrySide Side { get; init; }
    public EconomyAccountCode Account { get; init; }
    public CoinAmount Amount { get; init; }
    public WalletId? WalletId { get; init; }
    public CreditLotId? LotId { get; init; }
    public ProvenanceKind? Provenance { get; init; }
}

public sealed record PostingRequest
{
    public PostingRequest(
        PostingId id,
        PostingTemplate template,
        IdempotencyKey idempotencyKey,
        PostingAuthority authority,
        ReserveVersion reserveVersion,
        PolicyVersion policyVersion,
        SourceStampContract? source,
        DateTimeOffset requestedAt,
        IReadOnlyCollection<PostingLine> lines)
    {
        if (!Enum.IsDefined(authority)) throw new ArgumentOutOfRangeException(nameof(authority));
        ArgumentNullException.ThrowIfNull(lines);
        Id = id;
        Template = template;
        IdempotencyKey = idempotencyKey;
        Authority = authority;
        ReserveVersion = reserveVersion;
        PolicyVersion = policyVersion;
        Source = source;
        RequestedAt = requestedAt;
        Lines = lines.ToArray();
    }

    public PostingId Id { get; init; }
    public PostingTemplate Template { get; init; }
    public IdempotencyKey IdempotencyKey { get; init; }
    public PostingAuthority Authority { get; init; }
    public ReserveVersion ReserveVersion { get; init; }
    public PolicyVersion PolicyVersion { get; init; }
    public SourceStampContract? Source { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public IReadOnlyList<PostingLine> Lines { get; init; }
}

public sealed record PostedLineResult(int Sequence, Guid JournalLineId);

public sealed record PostingResult(
    PostingId PostingId,
    PostingStatus Status,
    string Hash,
    DateTimeOffset RecordedAt,
    IReadOnlyList<PostedLineResult> Lines);
