using System.Text.Json;
using GameGuild.Identity.Context.Actors;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>Input for creating a refund request through the approval-aware core (issue #419).</summary>
/// <param name="PaymentId">Payment to refund.</param>
/// <param name="Amount">Refund amount; null refunds the full remaining amount.</param>
/// <param name="Reason">Short machine-readable reason (e.g. <c>duplicate</c>).</param>
/// <param name="ReasonDetails">Optional human-readable details.</param>
/// <param name="ClientIdempotencyKey">Optional client idempotency key; replays return the recorded refund.</param>
/// <param name="RequestedByUserId">Optional explicit requester; defaults to the actor context subject.</param>
/// <param name="DisputeId">Dispute that triggered this refund, when applicable.</param>
/// <param name="ExecuteAtGateway">
///     When false (accept-dispute flow), the money already moved at the gateway (chargeback), so
///     the refund row, payment aggregate and accounting are recorded without a new gateway call.
/// </param>
public sealed record RefundRequestInput(
    Guid PaymentId,
    decimal? Amount,
    string Reason,
    string? ReasonDetails = null,
    string? ClientIdempotencyKey = null,
    Guid? RequestedByUserId = null,
    Guid? DisputeId = null,
    bool ExecuteAtGateway = true);

/// <summary>Outcome of a refund request.</summary>
public sealed record RefundRequestOutcome
{
    public required bool Success { get; init; }

    /// <summary>Whether the request is waiting for a manual approval decision.</summary>
    public required bool RequiresApproval { get; init; }

    public required Refund? Refund { get; init; }

    /// <summary>Failure reason when <see cref="Success"/> is false.</summary>
    public string? FailureReason { get; init; }
}

/// <summary>Outcome of executing a refund through the gateway.</summary>
public sealed record RefundExecutionOutcome
{
    public required bool Success { get; init; }

    public required Refund Refund { get; init; }

    /// <summary>Failure reason when <see cref="Success"/> is false.</summary>
    public string? FailureReason { get; init; }
}

/// <summary>
///     Approval-aware refund core (issue #419). Every refund — the immediate
///     <see cref="ProcessRefundCommand"/> path, the <see cref="RequestRefundCommand"/> workflow and
///     the accept-dispute flow — funnels through this service so that one <see cref="Refund"/> row
///     is recorded per attempt, the manual-approval threshold is applied uniformly, and the
///     accounting (revenue event + balancing ledger entry + audit trail) is emitted exactly once.
/// </summary>
public interface IRefundProcessingService
{
    /// <summary>
    ///     Validates the request, applies the manual-approval threshold, records the
    ///     <see cref="Refund"/> row and — unless manual approval is required — executes it.
    /// </summary>
    Task<RefundRequestOutcome> RequestRefundAsync(RefundRequestInput input, CancellationToken cancellationToken = default);

    /// <summary>Executes a Pending/Approved/Failed refund through the gateway and completes the accounting.</summary>
    Task<RefundExecutionOutcome> ExecuteAsync(Refund refund, CancellationToken cancellationToken = default);
}

/// <summary>
///     Default <see cref="IRefundProcessingService"/> implementation (issue #419).
/// </summary>
public sealed class RefundProcessingService(
    IRefundRepository refundRepository,
    IPaymentRepository paymentRepository,
    IPaymentGateway paymentGateway,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor,
    IOptions<RefundApprovalOptions> options,
    INotificationService notificationService,
    ILogger<RefundProcessingService> logger) : IRefundProcessingService
{
    private readonly RefundApprovalOptions _options = options.Value;

    public async Task<RefundRequestOutcome> RequestRefundAsync(RefundRequestInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Reason);

        var payment = await paymentRepository.GetByIdAsync(input.PaymentId, cancellationToken).ConfigureAwait(false);
        if (payment is null)
        {
            return Failure($"Payment {input.PaymentId} not found");
        }

        if (payment.Status is not (PaymentStatus.Succeeded or PaymentStatus.Disputed or PaymentStatus.PartiallyRefunded))
        {
            return Failure($"Payment in status {payment.Status} cannot be refunded");
        }

        var remainingRefundable = payment.Amount - payment.RefundedAmount;
        var amount = input.Amount ?? remainingRefundable;
        if (amount <= 0)
        {
            return Failure("Nothing left to refund on this payment");
        }

        if (amount > remainingRefundable)
        {
            return Failure($"Refund amount {amount} exceeds maximum refundable {remainingRefundable}");
        }

        // Retry-safe when the client supplies a key: replays resolve to the recorded refund row
        // instead of issuing a second gateway refund. Synthesized keys are unique per deliberate
        // request (no timestamp embedding on the recorded entity identity).
        var idempotencyKey = string.IsNullOrWhiteSpace(input.ClientIdempotencyKey)
            ? $"refund_{payment.Id}_{Guid.NewGuid():N}"
            : $"refund_{payment.Id}_{input.ClientIdempotencyKey.Trim()}";

        var existing = await refundRepository
            .GetByPaymentAndIdempotencyKeyAsync(payment.Id, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            logger.LogInformation(
                "Refund request for payment {PaymentId} replayed against existing refund {RefundId}",
                payment.Id,
                existing.Id);
            return new RefundRequestOutcome
            {
                Success = true,
                RequiresApproval = existing.Status == RefundStatus.Pending && existing.RequiresApproval,
                Refund = existing
            };
        }

        var requiresApproval = _options.ManualApprovalThresholdAmount is { } threshold && amount >= threshold;
        var requestedBy = input.RequestedByUserId
                          ?? actorContextAccessor.ActorContext.SubjectIdAsGuid
                          ?? throw new UnauthorizedAccessException("An authenticated requester is required to request a refund.");

        var refund = Refund.Create(
            payment.TenantId,
            payment.Id,
            amount,
            payment.Currency,
            input.Reason,
            input.ReasonDetails,
            requestedBy,
            requiresApproval,
            idempotencyKey,
            input.DisputeId);
        refund = await refundRepository.AddAsync(refund, cancellationToken).ConfigureAwait(false);

        if (refund.IsTerminal || refund.Status != RefundStatus.Pending)
        {
            // A concurrent request with the same key already drove this refund forward.
            return new RefundRequestOutcome
            {
                Success = true,
                RequiresApproval = false,
                Refund = refund
            };
        }

        if (requiresApproval)
        {
            await NotifyApproversAsync(refund, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Refund {RefundId} for payment {PaymentId} queued for manual approval (amount {Amount} >= threshold {Threshold})",
                refund.Id,
                payment.Id,
                amount,
                _options.ManualApprovalThresholdAmount);
            return new RefundRequestOutcome
            {
                Success = true,
                RequiresApproval = true,
                Refund = refund
            };
        }

        if (!input.ExecuteAtGateway)
        {
            // Accept-dispute flow: the funds already moved at the gateway through the chargeback;
            // record the refund row, payment aggregate and accounting without another gateway call.
            var recorded = await RecordGatewaySettledRefundAsync(refund, payment, cancellationToken).ConfigureAwait(false);
            return new RefundRequestOutcome
            {
                Success = recorded.Success,
                RequiresApproval = false,
                Refund = recorded.Refund,
                FailureReason = recorded.FailureReason
            };
        }

        var execution = await ExecuteAsync(refund, cancellationToken).ConfigureAwait(false);
        return new RefundRequestOutcome
        {
            Success = execution.Success,
            RequiresApproval = false,
            Refund = execution.Refund,
            FailureReason = execution.FailureReason
        };
    }

    public async Task<RefundExecutionOutcome> ExecuteAsync(Refund refund, CancellationToken cancellationToken = default)    {
        ArgumentNullException.ThrowIfNull(refund);

        if (refund.Status is not (RefundStatus.Pending or RefundStatus.Approved or RefundStatus.Failed))
        {
            return new RefundExecutionOutcome
            {
                Success = false,
                Refund = refund,
                FailureReason = $"Refund in status {refund.Status} cannot be executed"
            };
        }

        var payment = await paymentRepository.GetByIdAsync(refund.PaymentId, cancellationToken).ConfigureAwait(false);
        if (payment is null)
        {
            return new RefundExecutionOutcome
            {
                Success = false,
                Refund = refund,
                FailureReason = $"Payment {refund.PaymentId} not found"
            };
        }

        var statusBeforeRefund = payment.Status;

        refund.MarkProcessing();
        await refundRepository.UpdateAsync(refund, cancellationToken).ConfigureAwait(false);

        // Pinned to the gateway that owns the captured payment (issue #413): refunds
        // never reroute to another gateway.
        var gatewayRequest = new GatewayRefundRequest(
            IdempotencyKey: refund.IdempotencyKey,
            OriginalTransactionId: payment.ExternalTransactionId ?? payment.ExternalPaymentId ?? payment.Id.ToString(),
            Amount: refund.Amount,
            Reason: refund.Reason,
            ProviderId: payment.Provider,
            Currency: payment.Currency);

        var gatewayResult = await paymentGateway.ProcessRefundAsync(gatewayRequest, cancellationToken).ConfigureAwait(false);

        if (!gatewayResult.Success)
        {
            refund.MarkFailed(gatewayResult.ErrorMessage ?? "The gateway rejected the refund.");
            await refundRepository.UpdateAsync(refund, cancellationToken).ConfigureAwait(false);
            logger.LogWarning(
                "Refund {RefundId} for payment {PaymentId} failed at the gateway: {ErrorMessage}",
                refund.Id,
                payment.Id,
                gatewayResult.ErrorMessage);
            return new RefundExecutionOutcome
            {
                Success = false,
                Refund = refund,
                FailureReason = gatewayResult.ErrorMessage
            };
        }

        var gatewayRefundId = gatewayResult.RefundId ?? Guid.NewGuid().ToString();
        refund.MarkCompleted(gatewayRefundId, paymentGateway.ProviderId);
        await refundRepository.UpdateAsync(refund, cancellationToken).ConfigureAwait(false);

        payment.ProcessRefund(refund.Amount, gatewayRefundId, refund.Reason);
        await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        await RecordRefundAccountingAsync(payment, refund, statusBeforeRefund, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Refund {RefundId} of {Amount} {Currency} completed for payment {PaymentId}",
            refund.Id,
            refund.Amount,
            refund.Currency,
            payment.Id);

        return new RefundExecutionOutcome { Success = true, Refund = refund };
    }

    /// <summary>
    ///     Records a refund whose money already moved at the gateway (accepted chargeback):
    ///     completes the refund row and payment aggregate and emits the accounting without a
    ///     new gateway call, so accepted disputes are not refunded twice.
    /// </summary>
    private async Task<RefundRequestOutcome> RecordGatewaySettledRefundAsync(
        Refund refund,
        Payment payment,
        CancellationToken cancellationToken)
    {
        var statusBeforeRefund = payment.Status;

        refund.MarkProcessing();
        refund.MarkCompleted($"chargeback_{refund.Id}", payment.Provider);
        await refundRepository.UpdateAsync(refund, cancellationToken).ConfigureAwait(false);

        payment.ProcessRefund(refund.Amount, refund.GatewayRefundId!, refund.Reason);
        await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        await RecordRefundAccountingAsync(payment, refund, statusBeforeRefund, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Chargeback-settled refund {RefundId} of {Amount} {Currency} recorded for payment {PaymentId}",
            refund.Id,
            refund.Amount,
            refund.Currency,
            payment.Id);

        return new RefundRequestOutcome { Success = true, RequiresApproval = false, Refund = refund };
    }

    private async Task RecordRefundAccountingAsync(
        Payment payment,
        Refund refund,
        PaymentStatus statusBeforeRefund,
        CancellationToken cancellationToken)
    {
        var actorId = actorContextAccessor.ActorContext.SubjectIdAsGuid ?? refund.RequestedByUserId;

        var metadata = JsonSerializer.Serialize(new
        {
            paymentId = payment.Id.ToString(),
            refundId = refund.GatewayRefundId,
            localRefundId = refund.Id.ToString(),
            reason = refund.Reason,
            paymentStatusAfterRefund = payment.Status.ToString(),
            cumulativeRefundedAmount = payment.RefundedAmount
        });

        var revenueEvent = await revenueAuditService.RecordRevenueEventAsync(
            eventType: RevenueEventType.RefundProcessed,
            amount: refund.Amount,
            currency: payment.Currency,
            source: payment.SubscriptionId.HasValue ? RevenueSource.Subscription : RevenueSource.OneTimePayment,
            referenceId: payment.Id.ToString(),
            userId: actorId,
            metadata: metadata,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await revenueAuditService.CreateLedgerEntryAsync(
            entryType: LedgerEntryType.Refund,
            debitAccount: LedgerAccount.RefundsAndChargebacks.ToAccountCode(),
            creditAccount: LedgerAccount.Cash.ToAccountCode(),
            amount: refund.Amount,
            currency: payment.Currency,
            description: $"Refund {refund.GatewayRefundId} for payment {payment.Id}",
            revenueEventId: revenueEvent.Id,
            referenceNumber: refund.GatewayRefundId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await revenueAuditService.RecordAuditTrailAsync(
            entityType: "Payment",
            entityId: payment.Id,
            action: "StatusChanged",
            changedBy: actorId,
            oldValue: JsonSerializer.Serialize(new { status = statusBeforeRefund.ToString(), refundedAmount = payment.RefundedAmount - refund.Amount }),
            newValue: JsonSerializer.Serialize(new { status = payment.Status.ToString(), refundedAmount = payment.RefundedAmount }),
            reason: $"Refund {refund.GatewayRefundId} of {refund.Amount} {payment.Currency} processed: {refund.Reason}",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task NotifyApproversAsync(Refund refund, CancellationToken cancellationToken)
    {
        if (_options.ApproverNotificationEmails.Count == 0)
        {
            return;
        }

        foreach (var email in _options.ApproverNotificationEmails)
        {
            try
            {
                await notificationService.SendAsync(
                    recipientId: null,
                    type: NotificationType.Billing,
                    title: "Refund approval required",
                    message:
                        $"Refund {refund.Id} of {refund.Amount} {refund.Currency} " +
                        $"(reason: {refund.Reason}) is waiting for approval.",
                    channel: NotificationChannel.Email,
                    tenantId: refund.TenantId,
                    priority: NotificationPriority.High,
                    referenceEntityId: refund.Id,
                    referenceEntityType: nameof(Refund),
                    recipientEmail: email,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "Approver notification for refund {RefundId} to {Email} failed; the refund stays queued. Error type: {ErrorType}",
                    refund.Id,
                    LogRedaction.MaskEmail(email),
                    exception.GetType().FullName);
            }
        }
    }

    private static RefundRequestOutcome Failure(string reason) => new()
    {
        Success = false,
        RequiresApproval = false,
        Refund = null,
        FailureReason = reason
    };
}
