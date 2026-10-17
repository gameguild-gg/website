using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Handles subscription query and external-ID operations (read-only + external ID management).
/// </summary>
public class SubscriptionQueryAndExternalIdService(
    ISubscriptionRepository repository,
    ISubscriptionPlanService planService,
    ILogger<SubscriptionQueryAndExternalIdService> logger) : ISubscriptionQueryService, ISubscriptionExternalIdService, ISubscriptionPaymentContextService
{
    private readonly ISubscriptionRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly ISubscriptionPlanService _planService = planService ?? throw new ArgumentNullException(nameof(planService));
    private readonly ILogger<SubscriptionQueryAndExternalIdService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private async Task<Subscription> GetRequiredAsync(Guid subscriptionId, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(subscriptionId, ct).ConfigureAwait(false);
        return subscription ?? throw new SubscriptionNotFoundException(subscriptionId);
    }

    private async Task<SubscriptionPlan> GetRequiredPlanAsync(Guid planId, CancellationToken ct)
    {
        var plan = await _planService.GetByIdAsync(planId, ct).ConfigureAwait(false);
        return plan ?? throw new InvalidOperationException($"Subscription plan {planId} not found");
    }

    #region ISubscriptionQueryService

    public async Task<Subscription?> GetByIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Subscription?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByExternalIdAsync(externalId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsSubscriptionActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var subscription = await _repository.GetActiveTenantSubscriptionAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return subscription != null && subscription.Status == SubscriptionStatus.Active;
    }

    public async Task<Subscription?> GetActiveTenantSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetActiveTenantSubscriptionAsync(tenantId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Subscription>> GetTenantSubscriptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Subscription>> GetTenantSubscriptionHistoryAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Subscription>> GetExpiringSoonAsync(int days, CancellationToken cancellationToken = default)
    {
        return await _repository.GetExpiringSoonAsync(days, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Subscription>> GetDueForRenewalAsync(int days, CancellationToken cancellationToken = default)
    {
        return await _repository.GetDueForRenewalAsync(days, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Subscription>> GetTrialsExpiringSoonAsync(int days, CancellationToken cancellationToken = default)
    {
        return await _repository.GetTrialsExpiringSoonAsync(days, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SubscriptionLimitValidationResult> ValidateSubscriptionLimitsAsync(Guid tenantId, int userCount, long storageMb, long apiCallsPerMonth, CancellationToken cancellationToken = default)
    {
        var subscription = await _repository.GetActiveTenantSubscriptionAsync(tenantId, cancellationToken).ConfigureAwait(false);

        if (subscription == null)
            return SubscriptionLimitValidationResult.Invalid(
                new List<LimitCheckResult> { new ConcreteLimitCheckResult { LimitName = "Subscription", CurrentUsage = 0, MaxAllowed = 1, Passed = false } },
                "Please subscribe to a plan"
            );

        var plan = await GetRequiredPlanAsync(subscription.PlanId, cancellationToken).ConfigureAwait(false);
        var failedChecks = new List<LimitCheckResult>();

        if (plan.MaxUsers.HasValue && userCount > plan.MaxUsers.Value)
        {
            failedChecks.Add(new ConcreteLimitCheckResult
            {
                LimitName = "Users",
                CurrentUsage = userCount,
                MaxAllowed = plan.MaxUsers.Value,
                Passed = false
            });
        }

        if (plan.MaxStorageMb.HasValue && storageMb > plan.MaxStorageMb.Value)
        {
            failedChecks.Add(new ConcreteLimitCheckResult
            {
                LimitName = "Storage",
                CurrentUsage = storageMb,
                MaxAllowed = plan.MaxStorageMb.Value,
                Passed = false
            });
        }

        if (plan.MaxApiCallsPerMonth.HasValue && apiCallsPerMonth > plan.MaxApiCallsPerMonth.Value)
        {
            failedChecks.Add(new ConcreteLimitCheckResult
            {
                LimitName = "API Calls",
                CurrentUsage = apiCallsPerMonth,
                MaxAllowed = plan.MaxApiCallsPerMonth.Value,
                Passed = false
            });
        }

        return failedChecks.Count > 0
            ? SubscriptionLimitValidationResult.Invalid(failedChecks, "Consider upgrading your plan")
            : SubscriptionLimitValidationResult.Valid();
    }

    public async Task<SubscriptionUsageStatistics> GetUsageStatisticsAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        var plan = await GetRequiredPlanAsync(subscription.PlanId, cancellationToken).ConfigureAwait(false);

        return new ConcreteSubscriptionUsageStatistics
        {
            SubscriptionId = subscriptionId,
            UserCount = 0,
            StorageUsedMb = 0,
            ApiCallsThisMonth = 0,
            PlanLimits = new SubscriptionPlanLimits
            {
                MaxUsers = plan.MaxUsers ?? int.MaxValue,
                MaxStorageMb = plan.MaxStorageMb ?? long.MaxValue,
                MaxApiCallsPerMonth = plan.MaxApiCallsPerMonth ?? long.MaxValue,
                UnlimitedUsers = !plan.MaxUsers.HasValue,
                UnlimitedStorage = !plan.MaxStorageMb.HasValue,
                UnlimitedApiCalls = !plan.MaxApiCallsPerMonth.HasValue
            },
            PeriodStart = subscription.CurrentPeriodStart,
            PeriodEnd = subscription.CurrentPeriodEnd,
            LastUpdated = SystemClock.UtcNow
        };
    }

    public async Task<RevenueAnalytics> GetRevenueAnalyticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var subscriptions = await _repository.GetByDateRangeAsync(startDate, endDate, cancellationToken).ConfigureAwait(false);
        var subscriptionList = subscriptions.ToList();

        var totalRevenue = subscriptionList
            .Where(s => s.Status == SubscriptionStatus.Active || s.LastPaymentAt.HasValue)
            .Sum(s => s.Amount.Amount);

        var cycleBreakdown = subscriptionList
            .GroupBy(s => s.BillingCycle)
            .ToDictionary(g => g.Key, g => new Money(g.Sum(s => s.Amount.Amount), "USD"));

        var planBreakdown = subscriptionList
            .GroupBy(s => s.PlanId)
            .ToDictionary(g => g.Key, g => new Money(g.Sum(s => s.Amount.Amount), "USD"));

        var newSubCount = subscriptionList.Count(s => s.CreatedAt >= startDate);
        var averageAmount = subscriptionList.FirstOrDefault()?.Amount.Amount ?? 0m;

        return new ConcreteRevenueAnalytics
        {
            TotalRevenue = new Money(totalRevenue, "USD"),
            NewSubscriptionRevenue = new Money(newSubCount * averageAmount, "USD"),
            RenewalRevenue = Money.Zero(),
            UpgradeRevenue = Money.Zero(),
            AddOnRevenue = Money.Zero(),
            RefundAmount = Money.Zero(),
            PeriodStart = startDate,
            PeriodEnd = endDate,
            BillingCycleBreakdown = cycleBreakdown,
            PlanBreakdown = planBreakdown,
            TransactionCount = subscriptionList.Count
        };
    }

    public async Task<SubscriptionAnalytics> GetSubscriptionAnalyticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var statusCounts = await _repository.GetCountByStatusAsync(cancellationToken).ConfigureAwait(false);
        var subscriptions = await _repository.GetByDateRangeAsync(startDate, endDate, cancellationToken).ConfigureAwait(false);
        var subscriptionList = subscriptions.ToList();

        var total = statusCounts.Values.Sum();
        var active = statusCounts.GetValueOrDefault(SubscriptionStatus.Active);
        var trialing = statusCounts.GetValueOrDefault(SubscriptionStatus.Trialing);
        var cancelled = statusCounts.GetValueOrDefault(SubscriptionStatus.Cancelled);
        var suspended = statusCounts.GetValueOrDefault(SubscriptionStatus.Suspended);

        var newInPeriod = subscriptionList.Count(s => s.CreatedAt >= startDate);
        var cancelledInPeriod = subscriptionList.Count(s => s.CancelledAt.HasValue && s.CancelledAt >= startDate);

        var churnRate = active > 0 ? (decimal)cancelledInPeriod / active * 100 : 0;
        var growthRate = active > 0 ? (decimal)(newInPeriod - cancelledInPeriod) / active * 100 : 0;

        var activeSubscriptions = subscriptionList.Where(s => s.Status == SubscriptionStatus.Active).ToList();
        var monthlyRevenue = activeSubscriptions.Sum(s => s.Amount.Amount);

        return new ConcreteSubscriptionAnalytics
        {
            TotalSubscriptions = total,
            ActiveSubscriptions = active,
            TrialingSubscriptions = trialing,
            CancelledSubscriptions = cancelled,
            SuspendedSubscriptions = suspended,
            NewSubscriptions = newInPeriod,
            CancellationsInPeriod = cancelledInPeriod,
            ChurnRate = churnRate,
            GrowthRate = growthRate,
            PeriodStart = startDate,
            PeriodEnd = endDate,
            MonthlyRecurringRevenue = new Money(monthlyRevenue, "USD"),
            AnnualRecurringRevenue = new Money(monthlyRevenue * 12, "USD")
        };
    }

    #endregion

    #region ISubscriptionExternalIdService

    public async Task<Subscription> SetExternalIdsAsync(Guid subscriptionId, string? externalSubscriptionId, string? externalCustomerId, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        subscription.SetExternalIds(externalSubscriptionId, externalCustomerId);
        return await _repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
    }

    Task<Subscription?> ISubscriptionExternalIdService.GetByExternalIdAsync(string externalId, CancellationToken cancellationToken)
    {
        return _repository.GetByExternalIdAsync(externalId, cancellationToken);
    }

    public async Task<SubscriptionPaymentContext?> GetPaymentContextAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await _repository.GetByIdAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        if (subscription == null)
        {
            return null;
        }

        var tenantId = subscription.TenantId
                       ?? throw new InvalidOperationException(
                           "TenantId is required for subscription entities but was null. This indicates a data integrity issue.");

        var billingCycleNumber = subscription.LastProcessedBillingCycle + 1;
        var billingPeriodStart = billingCycleNumber == 1
            ? subscription.CurrentPeriodStart
            : subscription.NextBillingDate;
        var billingPeriodEnd = billingCycleNumber == 1
            ? subscription.CurrentPeriodEnd
            : subscription.BillingCycle.CalculateNextBillingDate(billingPeriodStart).AddDays(-1);

        return new SubscriptionPaymentContext(
            subscription.Id,
            tenantId,
            subscription.Amount.Amount,
            subscription.Amount.Currency,
            subscription.ExternalCustomerId,
            billingCycleNumber,
            billingPeriodStart,
            billingPeriodEnd,
            subscription.CreatedByUserId);
    }

    public async Task SetExternalCustomerIdAsync(Guid subscriptionId, string externalCustomerId, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        subscription.SetExternalIds(subscription.ExternalId, externalCustomerId);
        await _repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
    }

    #endregion
}
