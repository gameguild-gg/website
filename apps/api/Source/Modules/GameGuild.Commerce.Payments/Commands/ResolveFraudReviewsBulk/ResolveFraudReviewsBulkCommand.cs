using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Resolves a batch of pending manual fraud reviews with one decision (issue #409 bulk
///     actions). Applies the same transition as <see cref="ResolveFraudReviewCommand"/> to
///     every listed payment; items that are not pending reviews are skipped with a reason
///     instead of failing the batch. Requires the <c>payments:admin</c> permission; the
///     reviewer identity comes from the actor context, never the request.
/// </summary>
/// <param name="PaymentIds">Payments whose reviews are resolved (2-100 items).</param>
/// <param name="Approve">Whether the reviews are approved (true) or declined (false).</param>
/// <param name="Notes">Optional administrator notes recorded with every resolution.</param>
[AuthorizeRequest(PaymentsPermission.Keys.Admin)]
public sealed record ResolveFraudReviewsBulkCommand(IReadOnlyList<Guid> PaymentIds, bool Approve, string? Notes = null)
    : ICommand<FraudReviewsBulkResolutionResult>;

/// <summary>Aggregate outcome of a bulk fraud-review resolution.</summary>
public sealed record FraudReviewsBulkResolutionResult
{
    /// <summary>Number of payments the batch requested.</summary>
    public required int Requested { get; init; }

    /// <summary>Number of reviews actually resolved.</summary>
    public required int Resolved { get; init; }

    /// <summary>Number of items skipped (missing payment or no pending review).</summary>
    public required int Skipped { get; init; }

    /// <summary>Administrator who resolved the batch.</summary>
    public required Guid ReviewedByUserId { get; init; }

    /// <summary>Per-item outcomes, in request order.</summary>
    public required IReadOnlyList<FraudReviewsBulkItemResult> Items { get; init; }
}

/// <summary>Outcome of one payment in a bulk fraud-review resolution.</summary>
public sealed record FraudReviewsBulkItemResult
{
    /// <summary>Payment the item refers to.</summary>
    public required Guid PaymentId { get; init; }

    /// <summary>Whether the item's review was resolved.</summary>
    public required bool Success { get; init; }

    /// <summary>Skip reason when <see cref="Success"/> is false.</summary>
    public string? FailureReason { get; init; }
}
