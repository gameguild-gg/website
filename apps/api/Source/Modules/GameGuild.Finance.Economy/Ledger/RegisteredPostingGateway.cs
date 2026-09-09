using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Ledger;

public sealed record RegisteredPostingAuthority
{
    public RegisteredPostingAuthority(
        Guid capabilityId,
        Guid actorId,
        Guid tenantId,
        Guid riskDecisionId,
        string riskOperationFingerprint,
        long expectedCounterVersion)
    {
        if (capabilityId == Guid.Empty) throw new ArgumentException("Capability ID is required.", nameof(capabilityId));
        if (actorId == Guid.Empty) throw new ArgumentException("Actor ID is required.", nameof(actorId));
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (riskDecisionId == Guid.Empty) throw new ArgumentException("Risk decision ID is required.", nameof(riskDecisionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(riskOperationFingerprint);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedCounterVersion);

        CapabilityId = capabilityId;
        ActorId = actorId;
        TenantId = tenantId;
        RiskDecisionId = riskDecisionId;
        RiskOperationFingerprint = riskOperationFingerprint.Trim();
        ExpectedCounterVersion = expectedCounterVersion;
    }

    public Guid CapabilityId { get; }
    public Guid ActorId { get; }
    public Guid TenantId { get; }
    public Guid RiskDecisionId { get; }
    public string RiskOperationFingerprint { get; }
    public long ExpectedCounterVersion { get; }
}

public sealed record RegisteredPostingAllocation
{
    public RegisteredPostingAllocation(
        int lineSequence,
        CreditLotId parentLotId,
        long amountUnits,
        IReadOnlyList<RootTraceRange> rootRanges)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineSequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountUnits);
        ArgumentNullException.ThrowIfNull(rootRanges);
        if (rootRanges.Count == 0)
            throw new ArgumentException("Every allocation must carry its root trace ranges.", nameof(rootRanges));

        LineSequence = lineSequence;
        ParentLotId = parentLotId;
        AmountUnits = amountUnits;
        RootRanges = rootRanges.OrderBy(range => range.Root.Value).ThenBy(range => range.Start).ToArray();
    }

    public int LineSequence { get; }
    public CreditLotId ParentLotId { get; }
    public long AmountUnits { get; }
    public IReadOnlyList<RootTraceRange> RootRanges { get; }
}

public sealed record RegisteredPostingRequest
{
    public RegisteredPostingRequest(
        RegisteredPostingAuthority authority,
        PostingRequest posting,
        IReadOnlyList<RegisteredPostingAllocation>? allocations = null,
        string? dispatchSnapshotHash = null)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(posting);
        if (posting.Lines.Count == 0)
            throw new ArgumentException("A registered posting requires journal lines.", nameof(posting));
        if (posting.Lines.Select(line => line.Sequence).Distinct().Count() != posting.Lines.Count)
            throw new ArgumentException("Journal line sequences must be unique.", nameof(posting));
        if (allocations?.GroupBy(allocation => allocation.LineSequence).Any(group => !posting.Lines.Any(line => line.Sequence == group.Key)) == true)
            throw new ArgumentException("Every allocation must target a posting line.", nameof(allocations));
        if (dispatchSnapshotHash is { Length: > 128 })
            throw new ArgumentException("Dispatch snapshot hashes cannot exceed 128 characters.", nameof(dispatchSnapshotHash));

        Authority = authority;
        Posting = posting;
        Allocations = allocations?.ToArray() ?? [];
        DispatchSnapshotHash = string.IsNullOrWhiteSpace(dispatchSnapshotHash) ? null : dispatchSnapshotHash.Trim();
    }

    public RegisteredPostingAuthority Authority { get; }
    public PostingRequest Posting { get; }
    public IReadOnlyList<RegisteredPostingAllocation> Allocations { get; }
    public string? DispatchSnapshotHash { get; }
}

public sealed record RegisteredPostingReceipt
{
    public RegisteredPostingReceipt(PostingId postingId, long journalSequence, string journalHash, bool isDuplicate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(journalSequence);
        ArgumentException.ThrowIfNullOrWhiteSpace(journalHash);

        PostingId = postingId;
        JournalSequence = journalSequence;
        JournalHash = journalHash.Trim();
        IsDuplicate = isDuplicate;
    }

    public PostingId PostingId { get; }
    public long JournalSequence { get; }
    public string JournalHash { get; }
    public bool IsDuplicate { get; }
}

public interface IRegisteredPostingGateway
{
    RegisteredPostingReceipt Post(RegisteredPostingRequest request);
}

public sealed class RegisteredPostingRejectedException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
