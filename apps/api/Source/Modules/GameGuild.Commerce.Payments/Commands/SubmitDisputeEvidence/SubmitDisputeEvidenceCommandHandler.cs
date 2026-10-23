using System.Text;
using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for <see cref="SubmitDisputeEvidenceCommand"/> (issue #419 "submit evidence bundle
///     to gateway" / "track submission status"). Guarded (permission + authenticated actor +
///     tenant match) and audited. Assembles every evidence row attached to the dispute — uploaded
///     files and the automatically collected invoice/receipt/subscription records — into one
///     bundle, delivers it through the gateway's dispute-challenge surface, and records the
///     submission on both the dispute and each evidence row. Fails closed when the dispute has no
///     gateway identity or carries no evidence.
/// </summary>
public sealed class SubmitDisputeEvidenceCommandHandler(
    IApplicationDbContext context,
    IPaymentRepository paymentRepository,
    IPaymentGateway paymentGateway,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor,
    ILogger<SubmitDisputeEvidenceCommandHandler> logger) : ICommandHandler<SubmitDisputeEvidenceCommand, DisputeActionResult>
{
    public async Task<DisputeActionResult> Handle(SubmitDisputeEvidenceCommand request, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var actorId = actor.SubjectIdAsGuid
            ?? throw new UnauthorizedAccessException("An authenticated administrator is required to submit dispute evidence.");

        var dispute = await context.Set<PaymentDispute>()
            .Include(d => d.Evidence)
            .FirstOrDefaultAsync(d => d.Id == request.DisputeId, cancellationToken)
            .ConfigureAwait(false);
        if (dispute is null)
        {
            return Failure(request.DisputeId, $"Dispute {request.DisputeId} not found");
        }

        if (dispute.GatewayDisputeId is null)
        {
            return Failure(request.DisputeId, "Only disputes reconciled from a gateway webhook can have evidence submitted to the gateway.");
        }

        var payment = await paymentRepository.GetByIdAsync(dispute.PaymentId, cancellationToken).ConfigureAwait(false);
        if (payment is null)
        {
            return Failure(request.DisputeId, $"Payment {dispute.PaymentId} for the dispute no longer exists");
        }

        if (!actor.IsSystemAdmin && payment.TenantId != actor.TenantId)
        {
            throw new UnauthorizedAccessException("The dispute does not belong to the actor's tenant.");
        }

        if (dispute.Status is DisputeStatus.Resolved or DisputeStatus.Won or DisputeStatus.Lost or DisputeStatus.Cancelled)
        {
            return Failure(request.DisputeId, $"Dispute {request.DisputeId} is already resolved ({dispute.Status})");
        }

        var evidenceRows = dispute.Evidence
            .OrderBy(evidence => evidence.SubmittedAt)
            .ToList();
        if (evidenceRows.Count == 0)
        {
            return Failure(request.DisputeId, "Dispute has no evidence attached; attach evidence before submitting the bundle.");
        }

        var bundle = ComposeBundle(request.Summary, evidenceRows);
        var gatewayResult = await paymentGateway.ChallengeDisputeAsync(
                new GatewayDisputeChallengeRequest(
                    IdempotencyKey: $"dispute_evidence_{dispute.Id}",
                    ExternalDisputeId: dispute.GatewayDisputeId,
                    EvidenceSummary: bundle,
                    ProviderId: payment.Provider),
                cancellationToken)
            .ConfigureAwait(false);

        if (!gatewayResult.Success)
        {
            logger.LogWarning(
                "Gateway refused the evidence bundle for dispute {DisputeId} ({GatewayDisputeId}): {ErrorMessage}",
                dispute.Id,
                dispute.GatewayDisputeId,
                gatewayResult.ErrorMessage);
            return Failure(request.DisputeId, $"The gateway refused the evidence bundle: {gatewayResult.ErrorMessage}");
        }

        var submittedAt = SystemClock.UtcNow;
        var statusBefore = dispute.Status;

        // Submission tracking (issue #419): every evidence row keeps the timestamp of when it
        // first reached the gateway; re-submissions never rewrite that history.
        foreach (var evidence in evidenceRows)
        {
            evidence.GatewaySubmittedAt ??= submittedAt;
        }

        dispute.MarkEvidenceSubmitted(submittedAt);
        if (dispute.Status == DisputeStatus.Submitted)
        {
            dispute.MoveToReview();
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await revenueAuditService.RecordAuditTrailAsync(
            entityType: nameof(PaymentDispute),
            entityId: dispute.Id,
            action: "DisputeEvidenceSubmitted",
            changedBy: actorId,
            oldValue: JsonSerializer.Serialize(new
            {
                status = statusBefore.ToString(),
                evidenceSubmittedAt = (DateTime?)null
            }),
            newValue: JsonSerializer.Serialize(new
            {
                status = dispute.Status.ToString(),
                evidenceSubmittedAt = dispute.EvidenceSubmittedAt,
                evidenceCount = evidenceRows.Count
            }),
            reason: $"Evidence bundle of {evidenceRows.Count} item(s) submitted to gateway {dispute.GatewayName}:{dispute.GatewayDisputeId}",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Evidence bundle of {Count} item(s) submitted to gateway for dispute {DisputeId} by {ActorId}",
            evidenceRows.Count,
            dispute.Id,
            actorId);

        return new DisputeActionResult { Success = true, DisputeId = dispute.Id };
    }

    /// <summary>
    ///     Assembles the evidence bundle (issue #419 evidence package): an optional summary line
    ///     followed by one entry per evidence row — type, title, description and the file
    ///     reference when present — so the gateway receives the complete merchant evidence in a
    ///     single submission.
    /// </summary>
    internal static string ComposeBundle(string? summary, IReadOnlyList<DisputeEvidence> evidenceRows)
    {
        var bundle = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(summary))
        {
            bundle.AppendLine(summary.Trim());
        }

        bundle.Append($"Evidence bundle: {evidenceRows.Count} item(s).");
        foreach (var evidence in evidenceRows)
        {
            bundle.AppendLine();
            bundle.Append($"[{evidence.EvidenceType}] {evidence.Title}");
            if (!string.IsNullOrWhiteSpace(evidence.Description))
            {
                bundle.Append($" — {evidence.Description.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(evidence.FileName) || !string.IsNullOrWhiteSpace(evidence.FileUrl))
            {
                bundle.Append($" (file: {evidence.FileName ?? "n/a"}, {evidence.FileUrl ?? "n/a"})");
            }
        }

        return bundle.ToString();
    }

    private static DisputeActionResult Failure(Guid disputeId, string reason) => new()
    {
        Success = false,
        DisputeId = disputeId,
        FailureReason = reason
    };
}
