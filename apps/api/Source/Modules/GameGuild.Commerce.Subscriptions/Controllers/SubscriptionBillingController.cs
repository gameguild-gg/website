using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Subscription Billing Controller - handles billing, invoices, usage, and metrics.
///     Provides read-only endpoints for subscription financial data and analytics.
///     All endpoints require authentication.
///     Rate limiting uses Api policy for query endpoints.
/// </summary>
[ApiVersion("1.0")]
[Route("api")]
[Microsoft.AspNetCore.Http.Tags("commerce/subscriptions")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Api)]
public sealed class SubscriptionBillingController(ISender sender) : BaseApiController
{
    /// <summary>
    ///     Get subscription metrics
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Subscription metrics</returns>
    [HttpGet("v{version:apiVersion}/subscriptions:get-metrics")]
    [EndpointSummary("Get subscription metrics")]
    [EndpointDescription("Retrieves subscription metrics and analytics.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscriptionMetrics(CancellationToken ct)
    {
        // Get metrics via repository queries
        var statusCounts = await sender.Send(new GetSubscriptionStatusCountsQuery(), ct).ConfigureAwait(false);
        var now = SystemClock.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Calculate metrics from status counts
        var totalSubscriptions = statusCounts.Values.Sum();
        var activeSubscriptions = statusCounts.GetValueOrDefault(SubscriptionStatus.Active, 0);
        var trialingSubscriptions = statusCounts.GetValueOrDefault(SubscriptionStatus.Trialing, 0);
        var pastDueSubscriptions = statusCounts.GetValueOrDefault(SubscriptionStatus.PastDue, 0);
        var cancelledSubscriptions = statusCounts.GetValueOrDefault(SubscriptionStatus.Cancelled, 0);

        return Ok(new
        {
            TotalSubscriptions = totalSubscriptions,
            ActiveSubscriptions = activeSubscriptions,
            TrialingSubscriptions = trialingSubscriptions,
            PastDueSubscriptions = pastDueSubscriptions,
            CancelledSubscriptions = cancelledSubscriptions,
            StatusBreakdown = statusCounts,
            ReportGeneratedAt = SystemClock.UtcNow
        });
    }

    /// <summary>
    ///     Get subscription invoices
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20, max: 100)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of invoices</returns>
    [HttpGet("v{version:apiVersion}/subscriptions/{subscriptionId:guid}/invoices")]
    [EndpointSummary("Get subscription invoices")]
    [EndpointDescription("Retrieves the invoice history for a specific subscription.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubscriptionInvoices(
        Guid subscriptionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var invoices = await sender.Send(new GetSubscriptionInvoicesQuery(subscriptionId, page, pageSize), ct).ConfigureAwait(false);
        return Ok(invoices);
    }

    /// <summary>
    ///     Get subscription usage and limits
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Subscription usage information</returns>
    [HttpGet("v{version:apiVersion}/subscriptions/{subscriptionId:guid}/usage")]
    [EndpointSummary("Get subscription usage and limits")]
    [EndpointDescription("Retrieves usage information and limits for a specific subscription.")]
    [ProducesResponseType<SubscriptionUsageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubscriptionUsage(Guid subscriptionId, CancellationToken ct)
    {
        var usage = await sender.Send(new GetSubscriptionUsageQuery(subscriptionId), ct).ConfigureAwait(false);
        return Ok(usage);
    }

    /// <summary>
    ///     Get subscription billing history
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Billing history for the subscription</returns>
    [HttpGet("v{version:apiVersion}/subscriptions/{subscriptionId:guid}/billing-history")]
    [EndpointSummary("Get subscription billing history")]
    [EndpointDescription("Retrieves billing history for a specific subscription.")]
    [ProducesResponseType<IEnumerable<BillingHistoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscriptionBillingHistory(Guid subscriptionId, CancellationToken ct)
    {
        var history = await sender.Send(new GetSubscriptionBillingHistoryQuery(subscriptionId), ct).ConfigureAwait(false);
        return Ok(history);
    }

    /// <summary>
    ///     Get the current price breakdown of a subscription (issue #417)
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="promoCode">Optional promo code to include in the breakdown</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Itemized price breakdown from the unified pricing engine</returns>
    [HttpGet("v{version:apiVersion}/subscriptions/{subscriptionId:guid}/breakdown")]
    [EndpointSummary("Get subscription price breakdown")]
    [EndpointDescription("Returns the itemized price breakdown of a subscription's current plan through the unified pricing engine: base plan, discounts, totals and a calculation explanation.")]
    [ProducesResponseType(typeof(GameGuild.Commerce.Payments.PricingCalculationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubscriptionPriceBreakdown(Guid subscriptionId, [FromQuery] string? promoCode, CancellationToken ct)
    {
        var promoCodes = string.IsNullOrWhiteSpace(promoCode) ? null : new[] { promoCode.Trim() };
        var breakdown = await sender.Send(new GetSubscriptionPricingBreakdownQuery(subscriptionId, promoCodes), ct).ConfigureAwait(false);
        return Ok(breakdown);
    }

    /// <summary>
    ///     Preview a plan change for a subscription before finalizing (issue #417)
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="request">Preview request with the new plan id and optional promo codes</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Immediate prorated charge plus next-renewal preview</returns>
    [HttpPost("v{version:apiVersion}/subscriptions/{subscriptionId:guid}/pricing:preview")]
    [NoBusinessMutationEndpoint("Preview is a read-only quote; the plan change itself is a separate command.")]
    [EndpointSummary("Preview a subscription plan-change price")]
    [EndpointDescription("Previews what switching this subscription to a new plan costs before anything is applied: the immediate prorated charge and the next renewal amount, using the subscription's real billing period.")]
    [ProducesResponseType(typeof(GameGuild.Commerce.Payments.PricingPreviewResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewSubscriptionPlanChange(Guid subscriptionId, [FromBody] PreviewSubscriptionPlanChangeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var preview = await sender.Send(new PreviewSubscriptionPlanChangeQuery(subscriptionId, request.NewPlanId, request.PromoCodes), ct).ConfigureAwait(false);
        return Ok(preview);
    }
}

/// <summary>Request body for the subscription plan-change preview (issue #417).</summary>
/// <param name="NewPlanId">Plan being considered.</param>
/// <param name="PromoCodes">Optional promo codes to preview.</param>
public sealed record PreviewSubscriptionPlanChangeRequest(
    Guid NewPlanId,
    IReadOnlyList<string>? PromoCodes = null);
