using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameGuild.Commerce.Subscriptions;
using GameGuild.Identity.Tenants;

namespace GameGuild.API.Controllers;

/// <summary>
///     Cross-module platform-level KPIs computed from real domain state
///     (Active subscriptions, recent cancellations, active tenants).
///     Lives in the API composition root to keep Analytics, Subscriptions, and
///     Tenants modules independent of one another.
/// </summary>
[Authorize]
[ApiController]
[Route("api/analytics/platform-kpis")]
[Tags("analytics")]
public sealed class PlatformKpisController(
    ISubscriptionRepository subscriptionRepository,
    ITenantRepository tenantRepository
) : ControllerBase
{
    /// <summary>
    ///     Returns top-level platform KPIs for the dashboard.
    /// </summary>
    /// <returns>
    ///     <c>mrr</c> (USD-equivalent monthly recurring revenue derived from active subscription
    ///     amounts and billing cycle), <c>churnRate</c> (cancellations in the last 30 days divided
    ///     by active + cancelled, expressed as a percentage), and <c>activeTenants</c> count.
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<PlatformKpisResponse>> Get(CancellationToken ct)
    {
        var activeSubs = (await subscriptionRepository
            .GetByStatusAsync(SubscriptionStatus.Active, ct)
            .ConfigureAwait(false)).ToList();

        var mrr = activeSubs.Sum(s => NormalizeToMonthly(s.Amount.Amount, s.BillingCycle));

        var since = DateTime.UtcNow.AddDays(-30);
        var cancelledLast30 = (await subscriptionRepository
            .GetByStatusAsync(SubscriptionStatus.Cancelled, ct)
            .ConfigureAwait(false))
            .Count(s => s.CancelledAt.HasValue && s.CancelledAt.Value >= since);

        var denominator = activeSubs.Count + cancelledLast30;
        var churnRate = denominator == 0 ? 0d : (double)cancelledLast30 / denominator * 100d;

        var activeTenants = (await tenantRepository
            .GetActiveTenantsAsync(ct)
            .ConfigureAwait(false)).Count();

        return Ok(new PlatformKpisResponse(
            Mrr: decimal.Round(mrr, 2),
            ChurnRate: Math.Round(churnRate, 2),
            ActiveTenants: activeTenants,
            CalculatedAt: DateTime.UtcNow));
    }

    private static decimal NormalizeToMonthly(decimal amount, BillingCycle cycle) => cycle switch
    {
        BillingCycle.Weekly       => amount * 52m / 12m,
        BillingCycle.Monthly      => amount,
        BillingCycle.Quarterly    => amount / 3m,
        BillingCycle.SemiAnnually => amount / 6m,
        BillingCycle.Annually     => amount / 12m,
        BillingCycle.Biannually   => amount / 24m,
        _ => amount,
    };
}

public sealed record PlatformKpisResponse(
    decimal Mrr,
    double ChurnRate,
    int ActiveTenants,
    DateTime CalculatedAt);
