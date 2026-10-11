using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Resolves a pending manual fraud review (issue #409). Approving releases the fraud
///     hold so the payment may be retried through the existing manual retry flow; declining
///     keeps it failed and permanently non-retryable. Requires the <c>payments:admin</c>
///     permission; the reviewer identity comes from the actor context, never the request.
/// </summary>
/// <param name="PaymentId">Payment whose review is being resolved.</param>
/// <param name="Approve">Whether the review is approved (true) or declined (false).</param>
/// <param name="Notes">Optional administrator notes recorded with the resolution.</param>
[AuthorizeRequest(PaymentsPermission.Keys.Admin)]
public sealed record ResolveFraudReviewCommand(Guid PaymentId, bool Approve, string? Notes = null)
    : ICommand<FraudReviewResolutionResult>;

/// <summary>Result of resolving a fraud review.</summary>
public sealed record FraudReviewResolutionResult
{
    /// <summary>Whether the review was resolved successfully.</summary>
    public required bool Success { get; init; }

    /// <summary>Payment whose review was resolved.</summary>
    public required Guid PaymentId { get; init; }

    /// <summary>Final review status when successful.</summary>
    public FraudReviewStatus? ReviewStatus { get; init; }

    /// <summary>Administrator who resolved the review.</summary>
    public Guid? ReviewedByUserId { get; init; }

    /// <summary>When the review was resolved.</summary>
    public DateTime? ResolvedAt { get; init; }

    /// <summary>Failure reason when <see cref="Success"/> is false.</summary>
    public string? FailureReason { get; init; }
}
