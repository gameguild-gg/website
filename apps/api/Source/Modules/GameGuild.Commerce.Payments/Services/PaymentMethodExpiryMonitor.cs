using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Runs one pass of payment-method expiry monitoring (issue #414): auto-expires cards
///     whose expiry month has passed and issues 30/14/7-day notices through the Notifications
///     module. Horizon notices are idempotent per payment method.
/// </summary>
public interface IPaymentMethodExpiryMonitor
{
    /// <summary>
    ///     Executes one monitoring pass.
    /// </summary>
    /// <returns>Number of expiry notices issued during the pass.</returns>
    Task<int> RunPassAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public class PaymentMethodExpiryMonitor(
    IPaymentMethodRepository repository,
    INotificationService notificationService,
    ILogger<PaymentMethodExpiryMonitor> logger) : IPaymentMethodExpiryMonitor
{
    /// <inheritdoc />
    public async Task<int> RunPassAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var candidates = await repository.ListExpiringCandidatesAsync(cancellationToken).ConfigureAwait(false);
        var noticesIssued = 0;

        foreach (var paymentMethod in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                noticesIssued += await ProcessAsync(paymentMethod, nowUtc, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Expiry monitoring failed for payment method {PaymentMethodId}; will retry on the next pass.",
                    paymentMethod.Id);
            }
        }

        if (noticesIssued > 0)
        {
            logger.LogInformation("Payment method expiry pass issued {Notices} notice(s).", noticesIssued);
        }

        return noticesIssued;
    }

    private async Task<int> ProcessAsync(PaymentMethod paymentMethod, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var noticesIssued = 0;
        var daysLeft = paymentMethod.GetDaysUntilExpiry(nowUtc);

        if (daysLeft is not null && daysLeft <= 0)
        {
            if (paymentMethod.Status == PaymentMethodStatus.Active)
            {
                paymentMethod.MarkExpired();
                await repository.UpdateAsync(paymentMethod, cancellationToken).ConfigureAwait(false);
                logger.LogInformation(
                    "Payment method {PaymentMethodId} auto-expired (expiry {ExpiresAt:yyyy-MM-dd}).",
                    paymentMethod.Id, paymentMethod.ExpiresAt);
            }

            return noticesIssued;
        }

        if (daysLeft is null)
        {
            return noticesIssued;
        }

        foreach (var horizon in PaymentMethodExpiryOptions.Horizons)
        {
            if (daysLeft > horizon || paymentMethod.HasExpiryNotice(horizon))
            {
                continue;
            }

            var sent = await SendExpiryNoticeAsync(paymentMethod, daysLeft.Value, horizon, cancellationToken).ConfigureAwait(false);
            paymentMethod.RecordExpiryNotice(horizon, nowUtc);
            await repository.UpdateAsync(paymentMethod, cancellationToken).ConfigureAwait(false);
            if (sent)
            {
                noticesIssued++;
            }
        }

        return noticesIssued;
    }

    private async Task<bool> SendExpiryNoticeAsync(
        PaymentMethod paymentMethod,
        int daysLeft,
        int horizon,
        CancellationToken cancellationToken)
    {
        var brand = string.IsNullOrWhiteSpace(paymentMethod.CardBrand)
            ? "card"
            : paymentMethod.CardBrand!;
        var last4 = paymentMethod.CardLast4 is null ? string.Empty : $" ending in {paymentMethod.CardLast4}";
        var expiryLabel = paymentMethod.ExpiresAt is { } expiresAt
            ? expiresAt.ToString("MM/yyyy")
            : "soon";

        var result = await notificationService.SendAsync(
            recipientId: paymentMethod.UserId,
            type: NotificationType.PaymentMethodExpiring,
            title: $"Your {brand} card{last4} expires soon",
            message:
                $"Your {brand} card{last4} expires on {expiryLabel} (in {daysLeft} day(s)). " +
                "Update your payment method to keep your subscriptions active and avoid failed charges.",
            channel: NotificationChannel.Email,
            tenantId: paymentMethod.TenantId,
            priority: NotificationPriority.Normal,
            referenceEntityId: paymentMethod.Id,
            referenceEntityType: nameof(PaymentMethod),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            // Preference enforcement (mute/digest/quiet-hours) intentionally produced no row.
            logger.LogInformation(
                "Expiry notice (horizon {Horizon}d) for payment method {PaymentMethodId} was not delivered: {Reason}",
                horizon, paymentMethod.Id, result.Error?.Description ?? "preference decision");
            return false;
        }

        return true;
    }
}
