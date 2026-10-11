using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Approves a pending refund request (issue #419) and executes it through the gateway.
///     Guarded by the <c>payments:refunds:approve</c> permission; the approver identity comes
///     from the actor context, never from the request. Guarded + audited: the decision is
///     recorded on the refund row and an audit-trail entry is written.
/// </summary>
/// <param name="RefundId">Refund awaiting the approval decision.</param>
/// <param name="Notes">Optional approval notes recorded with the decision.</param>
[AuthorizeRequest(PaymentsPermission.Keys.RefundApprovals)]
public sealed record ApproveRefundCommand(Guid RefundId, string? Notes = null) : ICommand<RefundDecisionResult>;

/// <summary>Rejects a pending refund request (issue #419); no money moves. Same guard as approval.</summary>
/// <param name="RefundId">Refund awaiting the decision.</param>
/// <param name="Notes">Optional rejection notes recorded with the decision.</param>
[AuthorizeRequest(PaymentsPermission.Keys.RefundApprovals)]
public sealed record RejectRefundCommand(Guid RefundId, string? Notes = null) : ICommand<RefundDecisionResult>;

/// <summary>Result of an approval or rejection decision on a refund request.</summary>
public sealed record RefundDecisionResult
{
    /// <summary>Whether the decision was applied.</summary>
    public required bool Success { get; init; }

    /// <summary>The refund the decision was applied to, when successful.</summary>
    public RefundRecord? Refund { get; init; }

    /// <summary>Failure reason when <see cref="Success"/> is false.</summary>
    public string? FailureReason { get; init; }
}
