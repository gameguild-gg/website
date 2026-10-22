using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Submits the dispute evidence bundle to the gateway (issue #419): assembles every
///     <see cref="DisputeEvidence"/> row attached to the dispute into one evidence bundle and
///     delivers it through the gateway's challenge surface, then records the submission status —
///     the dispute's <see cref="PaymentDispute.EvidenceSubmittedAt"/> and each evidence row's
///     <see cref="DisputeEvidence.GatewaySubmittedAt"/>. Requires the <c>payments:admin</c>
///     permission; the acting identity comes from the actor context.
/// </summary>
/// <param name="DisputeId">Dispute whose evidence bundle is submitted.</param>
/// <param name="Summary">Optional free-text summary prepended to the assembled bundle.</param>
[AuthorizeRequest(PaymentsPermission.Keys.Admin)]
public sealed record SubmitDisputeEvidenceCommand(Guid DisputeId, string? Summary = null)
    : ICommand<DisputeActionResult>;
