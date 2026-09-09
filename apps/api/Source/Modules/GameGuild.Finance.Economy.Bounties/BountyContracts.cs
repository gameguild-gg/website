using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Bounties;

public readonly record struct BountyId
{
    public BountyId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Bounty ID cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
    public static BountyId New() => new(Guid.NewGuid());
}

public enum BountyStatus
{
    Open = 1,
    Expired = 2,
    Claimed = 3,
    Reclaimed = 4
}

public sealed record BountyEligibilityRequirements
{
    public BountyEligibilityRequirements(
        bool requiresPrerequisite,
        int minimumReputation,
        bool requiresInstructorVerification)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumReputation);
        RequiresPrerequisite = requiresPrerequisite;
        MinimumReputation = minimumReputation;
        RequiresInstructorVerification = requiresInstructorVerification;
    }

    public bool RequiresPrerequisite { get; }
    public int MinimumReputation { get; }
    public bool RequiresInstructorVerification { get; }
    public static BountyEligibilityRequirements None { get; } = new(false, 0, false);
}

public sealed record BountyEligibilitySnapshot(
    Guid ClaimantId,
    bool PrerequisiteCompleted,
    int Reputation,
    bool InstructorVerified,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt);

public sealed class BountyEscrowFragment
{
    public BountyEscrowFragment(CreditLot parentLot, FragmentSelection selection)
    {
        ParentLot = parentLot ?? throw new ArgumentNullException(nameof(parentLot));
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.ParentLotId != parentLot.Id || selection.Amount.Currency != parentLot.Amount.Currency)
            throw new ArgumentException("Escrow selection must match its parent lot.", nameof(selection));
        Selection = selection;
    }

    public CreditLot ParentLot { get; }
    public FragmentSelection Selection { get; }
    public CoinAmount Amount => Selection.Amount;
    public IReadOnlyList<RootTraceRange> SelectedRanges => Selection.SelectedRanges;
}

public sealed class BountyEscrowPosition
{
    private readonly IReadOnlyList<BountyEscrowFragment> _escrowFragments;

    internal BountyEscrowPosition(
        BountyId id,
        Guid posterId,
        WalletId posterWalletId,
        WalletId escrowWalletId,
        CoinAmount amount,
        IReadOnlyCollection<BountyEscrowFragment> escrowFragments,
        BountyEligibilityRequirements eligibility,
        int reclaimFeePpm,
        DateTimeOffset postedAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        PosterId = posterId;
        PosterWalletId = posterWalletId;
        EscrowWalletId = escrowWalletId;
        Amount = amount;
        _escrowFragments = Array.AsReadOnly(escrowFragments.ToArray());
        Eligibility = eligibility;
        ReclaimFeePpm = reclaimFeePpm;
        PostedAt = postedAt;
        ExpiresAt = expiresAt;
    }

    public BountyId Id { get; }
    public Guid PosterId { get; }
    public WalletId PosterWalletId { get; }
    public WalletId EscrowWalletId { get; }
    public CoinAmount Amount { get; }
    public IReadOnlyList<BountyEscrowFragment> EscrowFragments => _escrowFragments;
    public BountyEligibilityRequirements Eligibility { get; }
    public int ReclaimFeePpm { get; }
    public DateTimeOffset PostedAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public BountyStatus Status { get; internal set; } = BountyStatus.Open;
}

public sealed record PostBountyCommand(
    BountyId Id,
    Guid PosterId,
    WalletId PosterWalletId,
    WalletId EscrowWalletId,
    CoinAmount Amount,
    IReadOnlyList<CreditLot> AvailableLots,
    BountyEligibilityRequirements Eligibility,
    int ReclaimFeePpm,
    DateTimeOffset PostedAt,
    DateTimeOffset ExpiresAt,
    IdempotencyKey IdempotencyKey);

public sealed record BountyClaimRiskApproval(
    RiskDecisionSnapshot Decision,
    ProtectedOperationContext Context,
    EntityRiskCluster EntityCluster,
    IReadOnlyCollection<AggregateRiskLimit> Limits,
    Guid CounterReservationId);

public sealed record ClaimBountyCommand(
    BountyId BountyId,
    Guid ClaimantId,
    WalletId ClaimantWalletId,
    BountyEligibilitySnapshot Eligibility,
    BountyClaimRiskApproval Risk,
    long JournalSequence,
    DateTimeOffset ClaimedAt,
    IdempotencyKey IdempotencyKey);

public sealed record ReclaimBountyCommand(
    BountyId BountyId,
    Guid PosterId,
    WalletId PosterWalletId,
    WalletId FeeWalletId,
    long FirstJournalSequence,
    DateTimeOffset ReclaimedAt,
    IdempotencyKey IdempotencyKey);

/// <summary>
/// Server-side claim command. The workflow obtains all spendable fragments from the persisted
/// bounty escrow; callers can never nominate ledger lots or root trace ranges.
/// </summary>
public sealed record DurableBountyClaimRequest(
    BountyId BountyId,
    Guid ClaimantId,
    WalletId ClaimantWalletId,
    DateTimeOffset ClaimedAt,
    IdempotencyKey IdempotencyKey,
    string EvidenceHash,
    RegisteredPostingAuthority Authority,
    ReserveVersion ReserveVersion,
    PolicyVersion PolicyVersion,
    string? DispatchSnapshotHash = null);

/// <summary>
/// Server-side reclaim command. The workflow reads the escrowed fragments itself and restores
/// their original provenance, confirmation and maturity metadata. Callers nominate no lots.
/// </summary>
public sealed record DurableBountyReclaimRequest(
    BountyId BountyId,
    Guid PosterId,
    WalletId PosterWalletId,
    DateTimeOffset ReclaimedAt,
    IdempotencyKey IdempotencyKey,
    RegisteredPostingAuthority Authority,
    ReserveVersion ReserveVersion,
    PolicyVersion PolicyVersion,
    string? DispatchSnapshotHash = null);

public sealed class BountyClaimResult
{
    internal BountyClaimResult(
        BountyId bountyId,
        SourceStampContract proceedsSource,
        CreditLot proceedsLot,
        IReadOnlyCollection<ParentFragmentLineage> fundingParents)
    {
        BountyId = bountyId;
        ProceedsSource = proceedsSource;
        ProceedsLot = proceedsLot;
        FundingParents = Array.AsReadOnly(fundingParents.ToArray());
    }

    public BountyId BountyId { get; }
    public BountyStatus Status => BountyStatus.Claimed;
    public SourceStampContract ProceedsSource { get; }
    public CreditLot ProceedsLot { get; }
    public IReadOnlyList<ParentFragmentLineage> FundingParents { get; }
}

public sealed class BountyReclaimResult
{
    internal BountyReclaimResult(
        BountyId bountyId,
        CoinAmount returnedAmount,
        CoinAmount feeAmount,
        IReadOnlyCollection<CreditLot> restoredLots,
        IReadOnlyCollection<CreditLot> feeLots)
    {
        BountyId = bountyId;
        ReturnedAmount = returnedAmount;
        FeeAmount = feeAmount;
        RestoredLots = Array.AsReadOnly(restoredLots.ToArray());
        FeeLots = Array.AsReadOnly(feeLots.ToArray());
    }

    public BountyId BountyId { get; }
    public BountyStatus Status => BountyStatus.Reclaimed;
    public CoinAmount ReturnedAmount { get; }
    public CoinAmount FeeAmount { get; }
    public IReadOnlyList<CreditLot> RestoredLots { get; }
    public IReadOnlyList<CreditLot> FeeLots { get; }
}

public sealed class BountyIdempotencyConflictException(string message) : InvalidOperationException(message);
public sealed class BountyTerminalConflictException(string message) : InvalidOperationException(message);
public sealed class BountyExpiredException(string message) : InvalidOperationException(message);
public sealed class BountyNotExpiredException(string message) : InvalidOperationException(message);
public sealed class BountyOwnershipException(string message) : InvalidOperationException(message);
public sealed class BountyClaimIneligibleException(string message) : InvalidOperationException(message);
public sealed class BountyRiskExposureException(string message) : InvalidOperationException(message);
