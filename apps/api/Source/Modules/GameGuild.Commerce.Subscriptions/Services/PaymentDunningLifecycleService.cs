using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Commerce.Payments;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Payment-recovery bridge into the subscription lifecycle (issue #415). When a payment
///     exhausts its retry budget the subscription is suspended through the standard
///     <see cref="SubscriptionLifecycleService" /> with the
///     <see cref="PaymentDunningReasons.RetryExhausted" /> reason and the final-notice dunning
///     notification fires; when a recovery payment later succeeds, only subscriptions suspended
///     for that dunning reason are reactivated.
/// </summary>
public sealed class PaymentDunningLifecycleService(
    ISubscriptionRepository subscriptionRepository,
    ISubscriptionLifecycleService lifecycleService,
    IPublisher publisher,
    ILogger<PaymentDunningLifecycleService> logger) : IPaymentDunningLifecycleService
{
    public async Task<bool> SuspendForRetryExhaustionAsync(
        Guid subscriptionId,
        Guid tenantId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            logger.LogWarning(
                "Subscription {SubscriptionId} not found while suspending for retry exhaustion; skipping suspension.",
                subscriptionId);
            return false;
        }

        if (subscription.Status is SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled)
        {
            logger.LogInformation(
                "Subscription {SubscriptionId} is already {Status}; retry-exhaustion suspension is skipped.",
                subscriptionId,
                subscription.Status);
            return false;
        }

        if (subscription.Status is not (SubscriptionStatus.Active or SubscriptionStatus.PastDue))
        {
            logger.LogWarning(
                "Subscription {SubscriptionId} in status {Status} cannot be suspended for retry exhaustion; skipping.",
                subscriptionId,
                subscription.Status);
            return false;
        }

        await lifecycleService.SuspendAsync(subscriptionId, PaymentDunningReasons.RetryExhausted, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Subscription {SubscriptionId} suspended for payment retry exhaustion ({Reason}).",
            subscriptionId,
            reason);

        // Final dunning notice: the existing renewal-failed dunning handler renders the
        // FinalNotice ladder step for this event in the API composition root.
        await publisher.Publish(
            new SubscriptionRenewalFailedEvent(subscriptionId, tenantId, reason, SystemClock.UtcNow),
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> TryReactivateAfterRecoveryAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            logger.LogWarning(
                "Subscription {SubscriptionId} not found while reactivating after payment recovery; skipping.",
                subscriptionId);
            return false;
        }

        if (subscription.Status != SubscriptionStatus.Suspended)
        {
            return false;
        }

        if (!WasSuspendedForRetryExhaustion(subscription.Metadata))
        {
            logger.LogInformation(
                "Subscription {SubscriptionId} is suspended for a non-dunning reason; automatic reactivation is not allowed.",
                subscriptionId);
            return false;
        }

        await lifecycleService.ReactivateAsync(subscriptionId, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Subscription {SubscriptionId} reactivated after a successful recovery payment.",
            subscriptionId);

        return true;
    }

    /// <summary>
    ///     <see cref="Subscription.Suspend" /> records the suspension reason in the metadata JSON
    ///     (<c>{ "suspensionReason": ... }</c>); the guard only trusts that recorded reason.
    /// </summary>
    private static bool WasSuspendedForRetryExhaustion(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(metadata);
            return document.RootElement.TryGetProperty("suspensionReason", out var reason)
                   && reason.ValueKind == JsonValueKind.String
                   && string.Equals(reason.GetString(), PaymentDunningReasons.RetryExhausted, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
