using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Controller for managing promo code stacking rules (billing-admin policy:
/// all operations require the promo code manage permission).
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/promo-stacking-rules")]
[Microsoft.AspNetCore.Http.Tags("promo-stacking-rules")]
[Authorize]
[RequirePermission(PromoCodesPermission.Keys.Manage)]
public class PromoStackingRulesController(IMediator mediator) : BaseApiController
{
    /// <summary>
    /// List stacking rules ordered by priority (descending)
    /// </summary>
    /// <param name="includeInactive">Whether to include inactive rules</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PromoStackingRuleDto>>> GetRules(
        [FromQuery] bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPromoStackingRulesQuery(includeInactive);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Get a stacking rule by ID
    /// </summary>
    /// <param name="ruleId">Rule ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("{ruleId:guid}")]
    public async Task<ActionResult<PromoStackingRuleDto>> GetRuleById(
        Guid ruleId,
        CancellationToken cancellationToken = default)
    {
        var rules = await mediator.Send(new GetPromoStackingRulesQuery(true), cancellationToken).ConfigureAwait(false);
        var rule = rules.FirstOrDefault(r => r.Id == ruleId);
        return rule is null ? NotFound() : Ok(rule);
    }

    /// <summary>
    /// Create a stacking rule
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PromoStackingRuleDto>> CreateRule(
        [FromBody] CreatePromoStackingRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreatePromoStackingRuleCommand(
            request.Name,
            request.Description,
            request.IsActive,
            request.Priority,
            request.MaxStackableCount,
            request.AllowExclusiveStacking,
            request.MaxTotalDiscountPercentage,
            request.MaxTotalDiscountAmount,
            request.ConflictStrategy);

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetRuleById), new { ruleId = result.Id }, result);
    }

    /// <summary>
    /// Update a stacking rule
    /// </summary>
    /// <param name="ruleId">Rule ID</param>
    /// <param name="request">Update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPut("{ruleId:guid}")]
    public async Task<ActionResult<PromoStackingRuleDto>> UpdateRule(
        Guid ruleId,
        [FromBody] UpdatePromoStackingRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdatePromoStackingRuleCommand(
            ruleId,
            request.Description,
            request.IsActive,
            request.Priority,
            request.MaxStackableCount,
            request.AllowExclusiveStacking,
            request.MaxTotalDiscountPercentage,
            request.MaxTotalDiscountAmount,
            request.ConflictStrategy);

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Delete a stacking rule
    /// </summary>
    /// <param name="ruleId">Rule ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpDelete("{ruleId:guid}")]
    public async Task<IActionResult> DeleteRule(
        Guid ruleId,
        CancellationToken cancellationToken = default)
    {
        var command = new DeletePromoStackingRuleCommand(ruleId);
        var deleted = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return deleted ? NoContent() : NotFound();
    }
}

/// <summary>
/// Request to create a promo stacking rule
/// </summary>
public sealed record CreatePromoStackingRuleRequest(
    string Name,
    string? Description = null,
    bool IsActive = true,
    int Priority = 1,
    int MaxStackableCount = 3,
    bool AllowExclusiveStacking = false,
    decimal? MaxTotalDiscountPercentage = null,
    decimal? MaxTotalDiscountAmount = null,
    ConflictResolutionStrategy ConflictStrategy = ConflictResolutionStrategy.Sequential
);

/// <summary>
/// Request to update a promo stacking rule
/// </summary>
public sealed record UpdatePromoStackingRuleRequest(
    string? Description = null,
    bool? IsActive = null,
    int? Priority = null,
    int? MaxStackableCount = null,
    bool? AllowExclusiveStacking = null,
    decimal? MaxTotalDiscountPercentage = null,
    decimal? MaxTotalDiscountAmount = null,
    ConflictResolutionStrategy? ConflictStrategy = null
);
