using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Commerce.Subscriptions;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing/subscriptions")]
[Microsoft.AspNetCore.Http.Tags("billing/subscriptions")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
public sealed class BillingSubscriptionsController(ISender sender, IActorContextAccessor actorContextAccessor) : BaseApiController
{
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("List billing subscriptions")]
    [EndpointDescription("Compatibility billing endpoint backed by the subscription query model.")]
    [ProducesResponseType<PagedResult<Subscription>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBillingSubscriptions(
        [FromQuery] Guid? tenantId,
        [FromQuery] SubscriptionStatus? status,
        [FromQuery] Guid? planId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        NormalizePaging(ref page, ref pageSize);

        var tenantValidation = NormalizeTenantFilter(ref tenantId);
        if (tenantValidation != null) return tenantValidation;

        var result = await sender.Send(new GetPagedSubscriptionsQuery(page, pageSize, status, tenantId, planId), ct).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{subscriptionId:guid}", Name = "GetBillingSubscriptionById")]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("Get billing subscription")]
    [ProducesResponseType<Subscription>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBillingSubscriptionById(Guid subscriptionId, CancellationToken ct)
    {
        var subscription = await sender.Send(new GetSubscriptionByIdQuery(subscriptionId), ct).ConfigureAwait(false);
        return subscription is null ? NotFound() : Ok(subscription);
    }

    [HttpPost]
    [EndpointSummary("Create billing subscription")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateBillingSubscription([FromBody] CreateBillingSubscriptionRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (body.TenantId == Guid.Empty) return BadRequest(new { error = "TenantId cannot be empty" });
        if (body.PlanId == Guid.Empty) return BadRequest(new { error = "PlanId cannot be empty" });
        if (body.CreatedByUserId == Guid.Empty) return BadRequest(new { error = "CreatedByUserId cannot be empty" });
        if (body.Amount < 0) return BadRequest(new { error = "Amount cannot be negative" });

        var validationError = actorContextAccessor.ValidateTenantAccessAsActionResult(body.TenantId, "create billing subscription");
        if (validationError != null) return validationError;

        var id = await sender.Send(
            new CreateSubscriptionCommand(
                body.TenantId,
                body.PlanId,
                body.CreatedByUserId,
                body.BillingCycle,
                body.Amount,
                body.FulfilledOrderId,
                body.StartDate,
                body.TrialDays,
                body.PromoCodes,
                body.PromoCodeIdempotencyKey,
                body.ProductId),
            ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetBillingSubscriptionById), new { subscriptionId = id }, new { id });
    }

    [HttpPost("{subscriptionId:guid}:renew")]
    [EndpointSummary("Renew billing subscription")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RenewBillingSubscription(Guid subscriptionId, CancellationToken ct)
    {
        await sender.Send(new ProcessSubscriptionRenewalCommand(subscriptionId), ct).ConfigureAwait(false);
        return Accepted();
    }

    [HttpPost("{subscriptionId:guid}:cancel")]
    [EndpointSummary("Cancel billing subscription")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CancelBillingSubscription(Guid subscriptionId, [FromBody] CancelBillingSubscriptionRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new CancelSubscriptionCommand(subscriptionId, body.Reason, body.Note, body.EffectiveDate), ct).ConfigureAwait(false);
        return NoContent();
    }

    private IActionResult? NormalizeTenantFilter(ref Guid? tenantId)
    {
        var actorContext = actorContextAccessor.ActorContext;
        if (!actorContext.IsAuthenticated) return null;

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
        return null;
    }

    private static void NormalizePaging(ref int page, ref int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;
    }

    public sealed record CreateBillingSubscriptionRequest(
        Guid TenantId,
        Guid PlanId,
        Guid CreatedByUserId,
        BillingCycle BillingCycle,
        decimal Amount,
        Guid? FulfilledOrderId = null,
        DateTime? StartDate = null,
        int? TrialDays = null,
        IReadOnlyList<string>? PromoCodes = null,
        string? PromoCodeIdempotencyKey = null,
        Guid? ProductId = null);

    public sealed record CancelBillingSubscriptionRequest(CancellationReason Reason, string? Note = null, DateTime? EffectiveDate = null);
}
