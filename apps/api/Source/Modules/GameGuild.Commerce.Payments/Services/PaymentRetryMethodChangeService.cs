using System.Text.Json;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Default <see cref="IPaymentRetryMethodChangeService" /> (issue #415). Pause clears
///     <see cref="Payment.NextRetryAt" /> on the subscription's failed, retriable payments so the
///     automated retry queue stops picking them up; resume re-arms exactly those payments and
///     re-points them at the newly confirmed method. Both transitions are audit-trailed.
/// </summary>
public sealed class PaymentRetryMethodChangeService(
    IApplicationDbContext context,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor,
    ILogger<PaymentRetryMethodChangeService> logger) : IPaymentRetryMethodChangeService
{
    public async Task<int> PauseForMethodChangeAsync(
        Guid tenantId,
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var paused = 0;
        await foreach (var payment in FailedRetryingPaymentsAsync(tenantId, subscriptionId, cancellationToken)
                           .ConfigureAwait(false))
        {
            if (payment.NextRetryAt is null)
            {
                // Already paused, exhausted or non-retriable: nothing to pause.
                continue;
            }

            payment.PauseRetryQueue();
            await RecordTransitionAsync(payment, "RetryPaused", pausedForMethodChange: true, cancellationToken)
                .ConfigureAwait(false);
            paused++;
        }

        if (paused > 0)
        {
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Paused {Paused} retry schedule(s) for subscription {SubscriptionId} during a payment-method change.",
                paused,
                subscriptionId);
        }

        return paused;
    }

    public async Task<int> ResumeAfterMethodChangeAsync(
        Guid tenantId,
        Guid subscriptionId,
        string confirmedPaymentMethodId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmedPaymentMethodId);

        var resumed = 0;
        await foreach (var payment in FailedRetryingPaymentsAsync(tenantId, subscriptionId, cancellationToken)
                           .ConfigureAwait(false))
        {
            if (payment.NextRetryAt is not null)
            {
                // Still scheduled (not paused): leave the existing schedule untouched.
                continue;
            }

            payment.ApplyPaymentMethodChange(confirmedPaymentMethodId);
            payment.ResumeRetryQueue(SystemClock.UtcNow);
            await RecordTransitionAsync(payment, "RetryResumed", pausedForMethodChange: false, cancellationToken)
                .ConfigureAwait(false);
            resumed++;
        }

        if (resumed > 0)
        {
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Resumed {Resumed} retry schedule(s) for subscription {SubscriptionId} after payment-method confirmation.",
                resumed,
                subscriptionId);
        }

        return resumed;
    }

    /// <summary>
    ///     Failed payments of the subscription that still have retry budget and a retriable
    ///     failure — the only ones pause/resume may touch. Exhausted and hard-declined payments
    ///     are excluded so a method change never resurrects a closed recovery.
    /// </summary>
    private IAsyncEnumerable<Payment> FailedRetryingPaymentsAsync(
        Guid tenantId,
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        return context.Set<Payment>()
            .Where(payment => payment.TenantId == tenantId
                              && payment.SubscriptionId == subscriptionId
                              && payment.Status == PaymentStatus.Failed
                              && payment.IsRetriable
                              && payment.RetryCount < payment.MaxRetries)
            .OrderBy(payment => payment.NextRetryAt)
            .AsAsyncEnumerable();
    }

    private async Task RecordTransitionAsync(
        Payment payment,
        string action,
        bool pausedForMethodChange,
        CancellationToken cancellationToken)
    {
        var newValue = JsonSerializer.Serialize(new
        {
            status = payment.Status.ToString(),
            nextRetryAt = payment.NextRetryAt,
            paymentMethodId = payment.PaymentMethodId,
            pausedForMethodChange
        });

        await revenueAuditService.RecordAuditTrailAsync(
            entityType: "Payment",
            entityId: payment.Id,
            action: action,
            changedBy: RequireActorId(),
            oldValue: null,
            newValue: newValue,
            reason: pausedForMethodChange
                ? "Retry schedule paused for a payment-method change"
                : "Retry schedule resumed after the payment method was confirmed",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private Guid RequireActorId() => actorContextAccessor.ActorContext.SubjectIdAsGuid
        ?? throw new UnauthorizedAccessException("An authenticated actor is required to change retry schedules.");
}
