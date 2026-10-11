using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Accepts a payment dispute (issue #419): closes it as lost at the gateway (when the
///     dispute has a gateway identity) and automatically issues the refund through the same
///     approval-aware path used by direct refund requests, so the manual-approval threshold
///     applies uniformly. Requires the <c>payments:admin</c> permission; the acting identity
///     comes from the actor context.
/// </summary>
/// <param name="DisputeId">Dispute to accept.</param>
/// <param name="Notes">Optional resolution notes recorded with the acceptance.</param>
/// <param name="IssueRefund">Whether to issue the refund for the disputed amount (default true).</param>
[AuthorizeRequest(PaymentsPermission.Keys.Admin)]
public sealed record AcceptDisputeCommand(Guid DisputeId, string? Notes = null, bool IssueRefund = true)
    : ICommand<DisputeActionResult>;

/// <summary>
///     Challenges a payment dispute (issue #419): submits the merchant evidence to the gateway
///     to contest the chargeback. Requires the <c>payments:admin</c> permission.
/// </summary>
/// <param name="DisputeId">Dispute to challenge.</param>
/// <param name="EvidenceSummary">Free-text evidence submitted with the challenge.</param>
[AuthorizeRequest(PaymentsPermission.Keys.Admin)]
public sealed record ChallengeDisputeCommand(Guid DisputeId, string? EvidenceSummary = null)
    : ICommand<DisputeActionResult>;

/// <summary>Result of an accept or challenge action on a dispute.</summary>
public sealed record DisputeActionResult
{
    /// <summary>Whether the action was applied.</summary>
    public required bool Success { get; init; }

    /// <summary>The dispute the action was applied to.</summary>
    public required Guid DisputeId { get; init; }

    /// <summary>Refund created by accepting the dispute, when applicable.</summary>
    public RefundRecord? Refund { get; init; }

    /// <summary>Failure reason when <see cref="Success"/> is false.</summary>
    public string? FailureReason { get; init; }
}
