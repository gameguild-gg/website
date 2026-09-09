using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Payouts;

/// <summary>
/// A user-submitted intent to withdraw earned value. It is deliberately separate from a
/// <see cref="PayoutOperation"/>: no funds are reserved or sent until the request has passed
/// the later KYC, risk, provider, and FIFO reservation steps.
/// </summary>
public enum PayoutRequestState
{
    Submitted = 1,
    Cancelled = 2,
    Approved = 3,
    Rejected = 4,
    AwaitingSecondApproval = 5
}

public sealed record PayoutRequestReviewAuditEvent(
    Guid Id,
    Guid RequestId,
    Guid TenantId,
    Guid ActorId,
    PayoutRequestState Outcome,
    string Reason,
    DateTimeOffset OccurredAt);

public sealed record PayoutRequest(
    Guid Id,
    IdempotencyKey IdempotencyKey,
    string RequestHash,
    Guid PayeeId,
    WalletId WalletId,
    CoinAmount Amount,
    PayoutRequestState State,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? FirstApprovalActorId = null,
    Guid TenantId = default)
{
    public PayoutRequest Cancel(DateTimeOffset occurredAt)
    {
        if (State != PayoutRequestState.Submitted)
        {
            throw new PayoutRequestTransitionException("Only a submitted payout request can be cancelled.");
        }
        if (occurredAt < UpdatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(occurredAt), "Payout request timestamps cannot move backwards.");
        }

        return this with
        {
            State = PayoutRequestState.Cancelled,
            Version = checked(Version + 1),
            UpdatedAt = occurredAt
        };
    }

    public PayoutRequest Review(Guid reviewerId, PayoutRequestState outcome, DateTimeOffset occurredAt)
    {
        if (reviewerId == Guid.Empty)
        {
            throw new ArgumentException("Reviewer ID is required.", nameof(reviewerId));
        }
        if (reviewerId == PayeeId)
        {
            throw new PayoutRequestTransitionException("A payout requester cannot review their own request.");
        }
        if (outcome is not (PayoutRequestState.Approved or PayoutRequestState.Rejected))
        {
            throw new PayoutRequestTransitionException("Payout reviews must approve or reject the request.");
        }
        if (occurredAt < UpdatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(occurredAt), "Payout request timestamps cannot move backwards.");
        }

        return State switch
        {
            PayoutRequestState.Submitted when outcome == PayoutRequestState.Approved => this with
            {
                State = PayoutRequestState.AwaitingSecondApproval,
                Version = checked(Version + 1),
                UpdatedAt = occurredAt,
                FirstApprovalActorId = reviewerId
            },
            PayoutRequestState.Submitted when outcome == PayoutRequestState.Rejected => this with
            {
                State = PayoutRequestState.Rejected,
                Version = checked(Version + 1),
                UpdatedAt = occurredAt
            },
            PayoutRequestState.AwaitingSecondApproval => CompleteSecondApproval(reviewerId, outcome, occurredAt),
            _ => throw new PayoutRequestTransitionException("Only a submitted payout request can be reviewed.")
        };
    }

    private PayoutRequest CompleteSecondApproval(
        Guid reviewerId,
        PayoutRequestState outcome,
        DateTimeOffset occurredAt)
    {
        var firstApproverId = FirstApprovalActorId;
        if (!firstApproverId.HasValue)
        {
            throw new PayoutRequestTransitionException(
                "A payout request awaiting second approval must retain the first approver.");
        }
        if (firstApproverId.Value == reviewerId)
        {
            throw new PayoutRequestTransitionException(
                "The administrator who gave the first approval cannot complete the payout approval.");
        }

        return this with
        {
            State = outcome,
            Version = checked(Version + 1),
            UpdatedAt = occurredAt
        };
    }
}

public interface IPayoutRequestStore
{
    PayoutRequest? FindReplay(Guid tenantId, Guid payeeId, string idempotencyKey, string requestHash);

    void Add(PayoutRequest request);

    PayoutRequest GetForPayee(Guid tenantId, Guid requestId, Guid payeeId);

    PayoutRequest GetForReview(Guid requestId, Guid tenantId);

    IReadOnlyList<PayoutRequest> ListForPayee(Guid tenantId, Guid payeeId, int take);

    IReadOnlyList<PayoutRequest> ListForReview(Guid tenantId, int take);

    IReadOnlyList<PayoutRequestReviewAuditEvent> ListReviewAudit(Guid requestId, Guid tenantId);

    PayoutRequest Update(PayoutRequest request, long expectedVersion);

    PayoutRequest Review(
        PayoutRequest request,
        long expectedVersion,
        Guid tenantId,
        Guid reviewerId,
        PayoutRequestState outcome,
        string reason);
}

public sealed class PayoutRequestReplayConflictException(string message) : InvalidOperationException(message);
public sealed class PayoutRequestStaleCommandException(string message) : InvalidOperationException(message);
public sealed class PayoutRequestWalletUnavailableException(string message) : InvalidOperationException(message);
public sealed class PayoutRequestInsufficientWithdrawableFundsException(string message) : InvalidOperationException(message);
public sealed class PayoutRequestTransitionException(string message) : InvalidOperationException(message);
