using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Commerce;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Controller for managing dynamic pricing rules (issue #395: dynamic &amp; tiered pricing engine).
/// Reads require <c>products:read</c>; every mutation requires <c>products:pricing:manage</c>
/// here plus the <c>monetization:monetize</c> permission on the CQRS pipeline, mirroring the
/// <see cref="ProductsController.SetProductPricing"/> gate.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/billing/pricing-engine")]
[Microsoft.AspNetCore.Http.Tags("billing-pricing-engine")]
[Authorize]
[RequirePermission(ProductsPermission.Keys.Read)]
public class PricingRulesController(IMediator mediator, IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    /// Get pricing rules (paginated) with optional filters
    /// </summary>
    /// <param name="isActive">Filter by active status</param>
    /// <param name="ruleType">Filter by rule type</param>
    /// <param name="productId">Filter by product (includes global rules)</param>
    /// <param name="searchTerm">Search term for name/description</param>
    /// <param name="skip">Items to skip</param>
    /// <param name="take">Items to take</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PricingRuleDto>>> GetPricingRules(
        [FromQuery] bool? isActive = null,
        [FromQuery] PricingRuleType? ruleType = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPricingRulesQuery(isActive, ruleType, productId, searchTerm, skip, take);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Get a pricing rule by ID, including its volume tiers
    /// </summary>
    /// <param name="ruleId">The rule ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("{ruleId:guid}")]
    public async Task<ActionResult<PricingRuleDto>> GetPricingRuleById(
        Guid ruleId,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPricingRuleByIdQuery(ruleId);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Run the pricing engine for a product: base/sale price, the highest-priority applicable
    /// rule (volume tiers, customer segment, usage-metric gate), the A/B experiment arm for
    /// the bucket key, and promo codes.
    /// </summary>
    /// <param name="request">Calculation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost(":calculate")]
    public async Task<ActionResult<PricingCalculationResult>> CalculatePricing(
        [FromBody] CalculatePricingRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CalculatePricingCommand(
            request.ProductId,
            request.PricingId,
            request.Quantity,
            request.CustomerSegment,
            request.PromoCodes,
            GetUserId(),
            request.UsageMetrics,
            request.ExperimentBucketKey);

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Run one automated pricing scheduling pass immediately (issue #395): opt-in scheduled
    /// rules are activated/deactivated to match their date window and date-bounded
    /// experiments start/complete. The background scheduler runs the same pass periodically.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("scheduling:sweep")]
    [RequirePermission(ProductsPermission.Keys.PricingManage)]
    public async Task<ActionResult<PricingScheduleSweepResult>> RunSchedulingSweep(
        CancellationToken cancellationToken = default)
    {
        if (!TryActor(out _, out _)) return Forbid();

        var result = await mediator.Send(new RunPricingScheduleSweepCommand(), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Create a new pricing rule
    /// </summary>
    /// <param name="request">Creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost]
    [RequirePermission(ProductsPermission.Keys.PricingManage)]
    public async Task<ActionResult<PricingRuleDto>> CreatePricingRule(
        [FromBody] CreatePricingRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryActor(out _, out _)) return Forbid();

        var command = new CreatePricingRuleCommand(
            request.ProductId,
            request.Name,
            request.RuleType,
            request.Description,
            request.Priority,
            request.IsActive,
            request.StartDate,
            request.EndDate,
            request.MinQuantity,
            request.MaxQuantity,
            request.DiscountPercentage,
            request.DiscountAmount,
            request.FixedPrice,
            request.BuyQuantity,
            request.GetQuantity,
            request.TimeStart,
            request.TimeEnd,
            request.DaysOfWeek,
            request.Region,
            request.CustomerSegment,
            request.UsageMetricKey,
            request.UsageMinValue,
            request.UsageMaxValue,
            request.IsScheduled,
            request.Tiers);

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetPricingRuleById), new { ruleId = result.Id }, result);
    }

    /// <summary>
    /// Update a pricing rule (full update; tiers are replaced when provided)
    /// </summary>
    /// <param name="ruleId">The rule ID</param>
    /// <param name="request">Update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPut("{ruleId:guid}")]
    [RequirePermission(ProductsPermission.Keys.PricingManage)]
    public async Task<ActionResult<PricingRuleDto>> UpdatePricingRule(
        Guid ruleId,
        [FromBody] UpdatePricingRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryActor(out _, out _)) return Forbid();

        var command = new UpdatePricingRuleCommand(
            ruleId,
            request.ProductId,
            request.Name,
            request.RuleType,
            request.Description,
            request.Priority,
            request.IsActive,
            request.StartDate,
            request.EndDate,
            request.MinQuantity,
            request.MaxQuantity,
            request.DiscountPercentage,
            request.DiscountAmount,
            request.FixedPrice,
            request.BuyQuantity,
            request.GetQuantity,
            request.TimeStart,
            request.TimeEnd,
            request.DaysOfWeek,
            request.Region,
            request.CustomerSegment,
            request.UsageMetricKey,
            request.UsageMinValue,
            request.UsageMaxValue,
            request.IsScheduled,
            request.Tiers);

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Activate a pricing rule
    /// </summary>
    /// <param name="ruleId">The rule ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("{ruleId:guid}:activate")]
    [RequirePermission(ProductsPermission.Keys.PricingManage)]
    public async Task<ActionResult<PricingRuleDto>> ActivatePricingRule(
        Guid ruleId,
        CancellationToken cancellationToken = default)
    {
        if (!TryActor(out _, out _)) return Forbid();

        var command = new ActivatePricingRuleCommand(ruleId);
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Deactivate a pricing rule
    /// </summary>
    /// <param name="ruleId">The rule ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("{ruleId:guid}:deactivate")]
    [RequirePermission(ProductsPermission.Keys.PricingManage)]
    public async Task<ActionResult<PricingRuleDto>> DeactivatePricingRule(
        Guid ruleId,
        CancellationToken cancellationToken = default)
    {
        if (!TryActor(out _, out _)) return Forbid();

        var command = new DeactivatePricingRuleCommand(ruleId);
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Delete a pricing rule (soft delete)
    /// </summary>
    /// <param name="ruleId">The rule ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpDelete("{ruleId:guid}")]
    [RequirePermission(ProductsPermission.Keys.PricingManage)]
    public async Task<IActionResult> DeletePricingRule(
        Guid ruleId,
        CancellationToken cancellationToken = default)
    {
        if (!TryActor(out _, out _)) return Forbid();

        var command = new DeletePricingRuleCommand(ruleId);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private bool TryActor(out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue || !actor.SubjectIdAsGuid.HasValue)
        {
            return false;
        }

        tenantId = actor.TenantId.Value;
        actorId = actor.SubjectIdAsGuid.Value;
        return true;
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("id")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

/// <summary>
/// Request to calculate a price through the pricing engine.
/// </summary>
/// <param name="ProductId">Product to price</param>
/// <param name="PricingId">Optional specific pricing option</param>
/// <param name="Quantity">Quantity being priced</param>
/// <param name="CustomerSegment">Optional customer segment</param>
/// <param name="PromoCodes">Optional promo codes</param>
/// <param name="UsageMetrics">Optional usage metric values (issue #395 usage-based adjustments)</param>
/// <param name="ExperimentBucketKey">Optional A/B experiment bucket key (defaults to the acting user)</param>
public sealed record CalculatePricingRequest(
    Guid ProductId,
    Guid? PricingId = null,
    int Quantity = 1,
    string? CustomerSegment = null,
    List<string>? PromoCodes = null,
    Dictionary<string, decimal>? UsageMetrics = null,
    string? ExperimentBucketKey = null
);

/// <summary>
/// Request to create a pricing rule.
/// </summary>
public sealed record CreatePricingRuleRequest(
    Guid? ProductId,
    string Name,
    PricingRuleType RuleType,
    string? Description = null,
    int Priority = 0,
    bool IsActive = true,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int? MinQuantity = null,
    int? MaxQuantity = null,
    decimal? DiscountPercentage = null,
    decimal? DiscountAmount = null,
    decimal? FixedPrice = null,
    int? BuyQuantity = null,
    int? GetQuantity = null,
    string? TimeStart = null,
    string? TimeEnd = null,
    string? DaysOfWeek = null,
    string? Region = null,
    string? CustomerSegment = null,
    string? UsageMetricKey = null,
    decimal? UsageMinValue = null,
    decimal? UsageMaxValue = null,
    bool IsScheduled = false,
    IReadOnlyList<PricingRuleTierRequest>? Tiers = null
);

/// <summary>
/// Request to update a pricing rule (full update; tiers are replaced when provided).
/// </summary>
public sealed record UpdatePricingRuleRequest(
    Guid? ProductId,
    string Name,
    PricingRuleType RuleType,
    string? Description = null,
    int Priority = 0,
    bool IsActive = true,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int? MinQuantity = null,
    int? MaxQuantity = null,
    decimal? DiscountPercentage = null,
    decimal? DiscountAmount = null,
    decimal? FixedPrice = null,
    int? BuyQuantity = null,
    int? GetQuantity = null,
    string? TimeStart = null,
    string? TimeEnd = null,
    string? DaysOfWeek = null,
    string? Region = null,
    string? CustomerSegment = null,
    string? UsageMetricKey = null,
    decimal? UsageMinValue = null,
    decimal? UsageMaxValue = null,
    bool IsScheduled = false,
    IReadOnlyList<PricingRuleTierRequest>? Tiers = null
);
