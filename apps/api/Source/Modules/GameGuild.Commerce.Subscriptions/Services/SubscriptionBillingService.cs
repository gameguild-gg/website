using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Handles subscription billing operations: renewals, payments, reminders.
/// </summary>
public class SubscriptionBillingService(
    ISubscriptionRepository repository,
    ISubscriptionPlanService planService,
    ISubscriptionNotificationService notificationService,
    ILogger<SubscriptionBillingService> logger) : ISubscriptionBillingService
{
    private readonly ISubscriptionRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly ISubscriptionPlanService _planService = planService ?? throw new ArgumentNullException(nameof(planService));
    private readonly ISubscriptionNotificationService _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    private readonly ILogger<SubscriptionBillingService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private async Task<Subscription> GetRequiredAsync(Guid subscriptionId, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(subscriptionId, ct).ConfigureAwait(false);
        return subscription ?? throw new SubscriptionNotFoundException(subscriptionId);
    }

    private static string GenerateIdempotencyKey(Guid subscriptionId, int billingCycle, DateTime periodStart)
        => $"{subscriptionId}:{billingCycle}:{periodStart:yyyyMMdd}";

    public async Task<SubscriptionRenewalResult> ProcessRenewalAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);

        var idempotencyKey = GenerateIdempotencyKey(
            subscriptionId,
            subscription.LastProcessedBillingCycle + 1,
            subscription.NextBillingDate
        );

        return subscription.PrepareRenewal(idempotencyKey);
    }

    public async Task<Subscription> RecordPaymentAsync(Guid subscriptionId, decimal amount, string currency, DateTime paymentDate, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);

        var idempotencyKey = $"payment:{subscriptionId}:{paymentDate:yyyyMMddHHmmss}:{amount}";

        // A provider-confirmed payment settles the subscription's next unprocessed cycle. The cycle must be
        // explicit (the entity rejects cycle-less recordings), and re-confirming the same cycle is idempotent.
        var result = subscription.RecordPayment(amount, currency, paymentDate, idempotencyKey, subscription.LastProcessedBillingCycle + 1);

        if (!result.IsSuccess && !result.IsAlreadyProcessed)
        {
            throw new InvalidOperationException($"Failed to record payment: {result.Message}");
        }

        return await _repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Subscription> RecordPaymentFailureAsync(Guid subscriptionId, string reason, DateTime failureDate, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        subscription.RecordPaymentFailure(reason, failureDate);
        return await _repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BulkRenewalResult> ProcessBulkRenewalsAsync(IEnumerable<Guid> subscriptionIds, CancellationToken cancellationToken = default)
    {
        var attempts = new List<RenewalAttempt>();
        var totalRevenue = Money.Zero();
        var successCount = 0;
        var failCount = 0;

        foreach (var subscriptionId in subscriptionIds)
        {
            try
            {
                var result = await ProcessRenewalAsync(subscriptionId, cancellationToken).ConfigureAwait(false);

                if (result.Success)
                {
                    successCount++;
                    if (result.ChargedAmount != null)
                    {
                        totalRevenue += result.ChargedAmount;
                    }
                    attempts.Add(new ConcreteRenewalAttempt
                    {
                        SubscriptionId = subscriptionId,
                        Success = true,
                        Amount = result.ChargedAmount
                    });
                }
                else
                {
                    failCount++;
                    attempts.Add(new ConcreteRenewalAttempt
                    {
                        SubscriptionId = subscriptionId,
                        Success = false,
                        Amount = Money.Zero(),
                        ErrorMessage = result.FailureReason
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Operation");
                throw;
            }
        }

        return new ConcreteBulkRenewalResult
        {
            TotalProcessed = successCount + failCount,
            SuccessfulRenewals = successCount,
            FailedRenewals = failCount,
            TotalRevenue = totalRevenue,
            RenewalAttempts = attempts,
            ProcessedAt = SystemClock.UtcNow
        };
    }

    public async Task SendRenewalRemindersAsync(int daysBeforeRenewal, CancellationToken cancellationToken = default)
    {
        var subscriptions = await _repository.GetDueForRenewalAsync(daysBeforeRenewal, cancellationToken).ConfigureAwait(false);
        var subscriptionList = subscriptions.ToList();

        _logger.LogInformation("Sending renewal reminders to {Count} subscriptions due for renewal in {Days} days",
            subscriptionList.Count, daysBeforeRenewal);

        foreach (var subscription in subscriptionList)
        {
            try
            {
                await _notificationService.SendRenewalReminderAsync(subscription, daysBeforeRenewal, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send renewal reminder for subscription {SubscriptionId}", subscription.Id);
                throw;
            }
        }
    }

    public async Task SendTrialExpirationRemindersAsync(int daysBeforeExpiration, CancellationToken cancellationToken = default)
    {
        var subscriptions = await _repository.GetTrialsExpiringSoonAsync(daysBeforeExpiration, cancellationToken).ConfigureAwait(false);
        var subscriptionList = subscriptions.ToList();

        _logger.LogInformation("Sending trial expiration reminders to {Count} subscriptions with trials expiring in {Days} days",
            subscriptionList.Count, daysBeforeExpiration);

        foreach (var subscription in subscriptionList)
        {
            try
            {
                await _notificationService.SendTrialExpirationReminderAsync(subscription, daysBeforeExpiration, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send trial expiration reminder for subscription {SubscriptionId}", subscription.Id);
                throw;
            }
        }
    }
}
