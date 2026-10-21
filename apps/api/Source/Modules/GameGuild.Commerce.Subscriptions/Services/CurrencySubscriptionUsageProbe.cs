using GameGuild.Commerce.Payments;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Data-backed <see cref="ICurrencySubscriptionUsageProbe"/> (issue #412 spec A): counts
///     non-terminal subscriptions for the tenant whose authoritative price is denominated in the
///     queried currency. Implements the Payments-owned cross-module seam the same way
/// <see cref="SubscriptionPlanPricingResolver"/> implements IPlanPricingResolver.
/// </summary>
public sealed class CurrencySubscriptionUsageProbe(IApplicationDbContext context) : ICurrencySubscriptionUsageProbe
{
    /// <inheritdoc />
    public async Task<int?> CountActiveSubscriptionsUsingCurrencyAsync(
        Guid tenantId,
        string currencyCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        var normalized = currencyCode.Trim().ToUpperInvariant();

        return await context.Set<Subscription>()
            .AsNoTracking()
            .CountAsync(
                subscription => subscription.TenantId == tenantId
                                && subscription.Amount.Currency == normalized
                                && subscription.Status != SubscriptionStatus.Cancelled
                                && subscription.Status != SubscriptionStatus.Expired,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
