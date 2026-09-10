namespace GameGuild.Finance.Economy.Risk;

public enum RiskReviewStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum RiskReviewEventKind
{
    Submitted = 1,
    ApprovalRecorded = 2,
    Approved = 3,
    Rejected = 4,
    AppealSubmitted = 5
}

public enum RiskManualDecisionCode
{
    EvidenceVerified = 1,
    RiskAccepted = 2,
    PolicyViolation = 3,
    FraudConfirmed = 4
}

public sealed record RiskReviewCase(
    Guid Id,
    Guid DecisionId,
    Guid SubmittedBy,
    RiskReviewStatus Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ResolvedAt,
    Guid? ResolvedBy,
    string? Resolution,
    int RequiredApprovals,
    IReadOnlyList<Guid> Approvers,
    Guid? AppealOf);

public sealed record RiskReviewEvent(
    long Sequence,
    Guid ReviewId,
    RiskReviewEventKind Kind,
    Guid ActorId,
    IReadOnlyList<string> EvidenceHashes,
    string? Resolution,
    RiskManualDecisionCode? DecisionCode,
    DateTimeOffset OccurredAt);

public sealed class RiskReviewLedger
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, RiskReviewCase> _cases = [];
    private readonly List<RiskReviewEvent> _events = [];

    public IReadOnlyList<RiskReviewEvent> Events
    {
        get
        {
            lock (_gate) return [.. _events];
        }
    }

    public RiskReviewCase Submit(
        Guid id,
        RiskDecisionSnapshot decision,
        Guid submittedBy,
        IReadOnlyList<string> evidenceHashes,
        DateTimeOffset submittedAt) =>
        Submit(id, decision, submittedBy, evidenceHashes, submittedAt, 1, null);

    public RiskReviewCase Submit(
        Guid id,
        RiskDecisionSnapshot decision,
        Guid submittedBy,
        IReadOnlyList<string> evidenceHashes,
        DateTimeOffset submittedAt,
        int requiredApprovals,
        Guid? appealOf)
    {
        if (id == Guid.Empty) throw new ArgumentException("Review ID cannot be empty.", nameof(id));
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Outcome != RiskOutcome.Review)
            throw new ArgumentException("Only Review decisions can create a review case.", nameof(decision));
        if (submittedBy == Guid.Empty) throw new ArgumentException("Submitter cannot be empty.", nameof(submittedBy));
        ArgumentNullException.ThrowIfNull(evidenceHashes);
        if (evidenceHashes.Count == 0 || evidenceHashes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Review evidence hashes are required.", nameof(evidenceHashes));
        if (requiredApprovals is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(requiredApprovals));

        lock (_gate)
        {
            if (_cases.ContainsKey(id)) throw new InvalidOperationException("Review case already exists.");
            if (appealOf is not null && (!_cases.TryGetValue(appealOf.Value, out var appealed) ||
                                         appealed.Status != RiskReviewStatus.Rejected))
                throw new InvalidOperationException("Only a rejected review case can be appealed.");
            var review = new RiskReviewCase(
                id, decision.Id, submittedBy, RiskReviewStatus.Pending, submittedAt, null, null, null,
                requiredApprovals, [], appealOf);
            _cases.Add(id, review);
            _events.Add(new RiskReviewEvent(
                _events.Count + 1, id,
                appealOf is null ? RiskReviewEventKind.Submitted : RiskReviewEventKind.AppealSubmitted,
                submittedBy, [.. evidenceHashes], null, null, submittedAt));
            return review;
        }
    }

    public RiskReviewCase Appeal(
        Guid id,
        Guid rejectedReviewId,
        RiskDecisionSnapshot decision,
        Guid submittedBy,
        IReadOnlyList<string> evidenceHashes,
        DateTimeOffset submittedAt,
        int requiredApprovals = 2) =>
        Submit(id, decision, submittedBy, evidenceHashes, submittedAt, requiredApprovals, rejectedReviewId);

    public RiskReviewCase Approve(Guid id, Guid approvedBy, string resolution, DateTimeOffset occurredAt) =>
        Approve(id, approvedBy, RiskManualDecisionCode.EvidenceVerified, resolution, occurredAt);

    public RiskReviewCase Approve(
        Guid id,
        Guid approvedBy,
        RiskManualDecisionCode decisionCode,
        string resolution,
        DateTimeOffset occurredAt) =>
        RecordApproval(id, approvedBy, decisionCode, resolution, occurredAt);

    public RiskReviewCase Reject(Guid id, Guid rejectedBy, string resolution, DateTimeOffset occurredAt) =>
        Reject(id, rejectedBy, RiskManualDecisionCode.PolicyViolation, resolution, occurredAt);

    public RiskReviewCase Reject(
        Guid id,
        Guid rejectedBy,
        RiskManualDecisionCode decisionCode,
        string resolution,
        DateTimeOffset occurredAt) =>
        ResolveRejection(id, rejectedBy, decisionCode, resolution, occurredAt);

    public RiskReviewCase Current(Guid id)
    {
        lock (_gate)
        {
            return _cases.TryGetValue(id, out var review)
                ? review
                : throw new KeyNotFoundException("Risk review case was not found.");
        }
    }

    private RiskReviewCase RecordApproval(
        Guid id,
        Guid actorId,
        RiskManualDecisionCode decisionCode,
        string resolution,
        DateTimeOffset occurredAt)
    {
        ValidateResolution(actorId, decisionCode, resolution);
        lock (_gate)
        {
            var current = Current(id);
            ValidatePendingResolution(current, actorId, occurredAt);
            if (current.Approvers.Contains(actorId))
                throw new InvalidOperationException("A reviewer cannot approve the same case twice.");

            var approvers = current.Approvers.Append(actorId).ToArray();
            var isApproved = approvers.Length >= current.RequiredApprovals;
            var resolved = current with
            {
                Status = isApproved ? RiskReviewStatus.Approved : RiskReviewStatus.Pending,
                ResolvedAt = isApproved ? occurredAt : null,
                ResolvedBy = isApproved ? actorId : null,
                Resolution = isApproved ? resolution.Trim() : null,
                Approvers = approvers
            };
            _cases[id] = resolved;
            _events.Add(new RiskReviewEvent(
                _events.Count + 1, id,
                isApproved ? RiskReviewEventKind.Approved : RiskReviewEventKind.ApprovalRecorded,
                actorId, [], resolution.Trim(), decisionCode, occurredAt));
            return resolved;
        }
    }

    private RiskReviewCase ResolveRejection(
        Guid id,
        Guid actorId,
        RiskManualDecisionCode decisionCode,
        string resolution,
        DateTimeOffset occurredAt)
    {
        ValidateResolution(actorId, decisionCode, resolution);
        lock (_gate)
        {
            var current = Current(id);
            ValidatePendingResolution(current, actorId, occurredAt);
            var resolved = current with
            {
                Status = RiskReviewStatus.Rejected,
                ResolvedAt = occurredAt,
                ResolvedBy = actorId,
                Resolution = resolution.Trim()
            };
            _cases[id] = resolved;
            _events.Add(new RiskReviewEvent(
                _events.Count + 1, id, RiskReviewEventKind.Rejected, actorId, [], resolution.Trim(),
                decisionCode, occurredAt));
            return resolved;
        }
    }

    private static void ValidateResolution(
        Guid actorId,
        RiskManualDecisionCode decisionCode,
        string resolution)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Reviewer cannot be empty.", nameof(actorId));
        if (!Enum.IsDefined(decisionCode)) throw new ArgumentOutOfRangeException(nameof(decisionCode));
        ArgumentException.ThrowIfNullOrWhiteSpace(resolution);
    }

    private static void ValidatePendingResolution(
        RiskReviewCase current,
        Guid actorId,
        DateTimeOffset occurredAt)
    {
        if (current.Status != RiskReviewStatus.Pending)
            throw new InvalidOperationException("Risk review case has already been resolved.");
        if (current.SubmittedBy == actorId)
            throw new InvalidOperationException("The submitter cannot resolve their own risk review.");
        if (occurredAt < current.SubmittedAt)
            throw new ArgumentException("Resolution cannot predate review submission.", nameof(occurredAt));
    }
}
