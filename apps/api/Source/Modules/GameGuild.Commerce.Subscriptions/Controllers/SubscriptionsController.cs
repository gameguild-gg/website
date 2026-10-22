using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Subscriptions CRUD Controller - RESTful API for subscription resource operations.
///     Handles creation, retrieval, update, and deletion of subscriptions.
///     All endpoints require authentication to protect sensitive subscription data.
///     Rate limiting is applied to prevent DoS attacks and enumeration:
///     - ExpensiveOperations policy for mutations (create, update, delete)
///     - Api policy for query endpoints
/// </summary>
[ApiVersion("1.0")]
[Route("api")]
[Microsoft.AspNetCore.Http.Tags("commerce/subscriptions")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
public sealed class SubscriptionsController(ISender sender, IActorContextAccessor actorContextAccessor) : BaseApiController
{
    #region Collection Operations - /v1/subscriptions

    /// <summary>
    ///     Create a new subscription
    /// </summary>
    /// <param name="body">Subscription creation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created subscription</returns>
    [HttpPost("v{version:apiVersion}/subscriptions")]
    [EndpointSummary("Create a new subscription")]
    [EndpointDescription("Creates a new subscription with the provided information.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateSubscription([FromBody] CreateSubscriptionRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        // SECURITY: Validate TenantId from authenticated context (prevents cross-tenant attack)
        var validationError = ValidateTenantAccess(body.TenantId, "create subscription");
        if (validationError != null) return validationError;

        var id = await sender.Send(new CreateSubscriptionCommand(
            body.TenantId,
            body.PlanId,
            body.CreatedByUserId,
            body.BillingCycle,
            body.Amount,
            FulfilledOrderId: body.FulfilledOrderId,
            StartDate: body.StartDate,
            TrialDays: body.TrialDays,
            PromoCodes: body.PromoCodes,
            PromoCodeIdempotencyKey: body.PromoCodeIdempotencyKey,
            ProductId: body.ProductId), ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetSubscriptionById), new { subscriptionId = id }, new { id });
    }

    /// <summary>
    ///     Get subscriptions with pagination, search, and filtering
    /// </summary>
    /// <param name="page">Page number for pagination (default: 1)</param>
    /// <param name="pageSize">Number of subscriptions per page (default: 20, max: 100)</param>
    /// <param name="status">Filter by status</param>
    /// <param name="tenantId">Filter by tenant ID</param>
    /// <param name="planId">Filter by plan ID</param>
    /// <param name="expiring">Filter for expiring subscriptions (within specified days)</param>
    /// <param name="expiringDays">Number of days to look ahead for expiring subscriptions (default: 30)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of subscriptions</returns>
    [HttpGet("v{version:apiVersion}/subscriptions")]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("Get subscriptions with pagination, search, and filtering")]
    [EndpointDescription("Retrieves a paginated list of subscriptions with optional filtering. Use query parameters: status (active, trialing, cancelled, etc.), tenantId, planId, and expiring=true for expiring subscriptions.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscriptions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] SubscriptionStatus? status = null,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? planId = null,
        [FromQuery] bool? expiring = null,
        [FromQuery] int expiringDays = 30,
        CancellationToken ct = default
    )
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var actorContext = actorContextAccessor.ActorContext;
        if (actorContext.IsAuthenticated)
        {
            if (!actorContext.TenantId.HasValue && !actorContext.IsSystemAdmin)
            {
                return BadRequest(new { error = "Tenant context is required" });
            }

            if (!actorContext.IsSystemAdmin &&
                tenantId.HasValue &&
                actorContext.TenantId.HasValue &&
                tenantId.Value != actorContext.TenantId.Value)
            {
                return Forbid();
            }

            tenantId ??= actorContext.TenantId;
        }

        // If expiring filter is set, delegate to expiring subscriptions query
        if (expiring == true)
        {
            var expiringResult = await sender.Send(new GetExpiringSubscriptionsQuery(expiringDays), ct).ConfigureAwait(false);
            return Ok(expiringResult);
        }

        var result = await sender.Send(new GetPagedSubscriptionsQuery(page, pageSize, status, tenantId, planId), ct).ConfigureAwait(false);
        return Ok(result);
    }

    #endregion

    #region Individual Item Operations - /v1/subscriptions/{subscriptionId}

    /// <summary>
    ///     Check if subscription exists by ID
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>200 if exists, 404 if not</returns>
    [HttpHead("v{version:apiVersion}/subscriptions/{subscriptionId:guid}")]
    [EndpointSummary("Check if subscription exists by ID")]
    [EndpointDescription("Checks if a subscription exists by ID without returning the body.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckSubscriptionExistsById(Guid subscriptionId, CancellationToken ct)
    {
        var subscription = await sender.Send(new GetSubscriptionByIdQuery(subscriptionId), ct).ConfigureAwait(false);
        if (subscription is null) return NotFound();

        var actorContext = actorContextAccessor.ActorContext;
        if (actorContext.IsAuthenticated)
        {
            if (!actorContext.TenantId.HasValue)
            {
                return BadRequest(new { error = "Tenant context is required" });
            }

            if (subscription.TenantId != actorContext.TenantId.Value)
            {
                return NotFound();
            }
        }

        return Ok();
    }

    /// <summary>
    ///     Get subscription by ID
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Subscription details</returns>
    [HttpGet("v{version:apiVersion}/subscriptions/{subscriptionId:guid}")]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("Get subscription by ID")]
    [EndpointDescription("Retrieves detailed information for a specific subscription.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubscriptionById(Guid subscriptionId, CancellationToken ct)
    {
        var subscription = await sender.Send(new GetSubscriptionByIdQuery(subscriptionId), ct).ConfigureAwait(false);
        if (subscription is null) return NotFound();

        var actorContext = actorContextAccessor.ActorContext;
        if (actorContext.IsAuthenticated)
        {
            if (!actorContext.TenantId.HasValue)
            {
                return BadRequest(new { error = "Tenant context is required" });
            }

            if (subscription.TenantId != actorContext.TenantId.Value)
            {
                return NotFound();
            }
        }

        return Ok(subscription);
    }

    /// <summary>
    ///     Partially update a subscription
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="body">Partial update request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPatch("v{version:apiVersion}/subscriptions/{subscriptionId:guid}")]
    [EndpointSummary("Partially update subscription")]
    [EndpointDescription("Updates specific fields of a subscription. Only provided fields are updated.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PatchSubscription(Guid subscriptionId, [FromBody] PatchSubscriptionRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new PatchSubscriptionCommand(
            subscriptionId,
            body.BillingCycle,
            body.AutoRenew,
            body.ExternalSubscriptionId,
            body.ExternalCustomerId,
            body.Metadata), ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    ///     Full update of a subscription
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="body">Full update request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPut("v{version:apiVersion}/subscriptions/{subscriptionId:guid}")]
    [EndpointSummary("Full update subscription")]
    [EndpointDescription("Performs a full replacement of subscription data. All fields will be updated.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PutSubscription(Guid subscriptionId, [FromBody] PutSubscriptionRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new UpdateSubscriptionCommand(
            subscriptionId,
            body.PlanId,
            body.BillingCycle,
            body.Amount,
            body.AutoRenew,
            body.ExternalSubscriptionId,
            body.ExternalCustomerId), ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    ///     Delete a subscription
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("v{version:apiVersion}/subscriptions/{subscriptionId:guid}")]
    [EndpointSummary("Delete subscription")]
    [EndpointDescription("Permanently deletes a subscription. Use cancel action for soft removal.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSubscription(Guid subscriptionId, CancellationToken ct)
    {
        await sender.Send(new DeleteSubscriptionCommand(subscriptionId), ct).ConfigureAwait(false);
        return NoContent();
    }

    #endregion

    #region Request DTOs

    // POST /subscriptions
    /// <summary>Request to create a subscription</summary>
    /// <param name="TenantId">The tenant ID</param>
    /// <param name="PlanId">The subscription plan ID</param>
    /// <param name="CreatedByUserId">The user who created the subscription</param>
    /// <param name="BillingCycle">The billing cycle (Monthly, Yearly, etc.)</param>
    /// <param name="Amount">The subscription amount</param>
    /// <param name="Currency">The currency code</param>
    /// <param name="FulfilledOrderId">Optional Order ID that triggered this subscription (Economic Model: Order→Subscription causality)</param>
    /// <param name="StartDate">Optional start date</param>
    /// <param name="TrialDays">Optional trial period in days</param>
    /// <param name="PromoCodes">Optional promo codes applied at creation (issue #418)</param>
    /// <param name="PromoCodeIdempotencyKey">Optional replay-protection key for the promo application</param>
    /// <param name="ProductId">Optional product context for product-scoped promo codes</param>
    public sealed record CreateSubscriptionRequest(
        Guid TenantId,
        Guid PlanId,
        Guid CreatedByUserId,
        BillingCycle BillingCycle,
        decimal Amount,
        string Currency,
        Guid? FulfilledOrderId = null,
        DateTime? StartDate = null,
        int? TrialDays = null,
        IReadOnlyList<string>? PromoCodes = null,
        string? PromoCodeIdempotencyKey = null,
        Guid? ProductId = null);

    /// <summary>Request to partially update a subscription</summary>
    public sealed record PatchSubscriptionRequest(
        BillingCycle? BillingCycle = null,
        bool? AutoRenew = null,
        string? ExternalSubscriptionId = null,
        string? ExternalCustomerId = null,
        string? Metadata = null);

    /// <summary>Request to fully update a subscription</summary>
    public sealed record PutSubscriptionRequest(
        Guid PlanId,
        BillingCycle BillingCycle,
        decimal Amount,
        bool AutoRenew,
        string? ExternalSubscriptionId = null,
        string? ExternalCustomerId = null);

    #endregion

    #region Private Methods

    /// <summary>
    ///     Validates that the authenticated user has access to the specified tenant.
    ///     Uses shared TenantValidationExtensions for DRY compliance.
    /// </summary>
    private IActionResult? ValidateTenantAccess(Guid requestedTenantId, string operation)
        => actorContextAccessor.ValidateTenantAccessAsActionResult(requestedTenantId, operation);

    #endregion
}
