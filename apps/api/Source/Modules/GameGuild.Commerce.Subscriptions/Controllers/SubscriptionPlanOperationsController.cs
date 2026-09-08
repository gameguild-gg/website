using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Commerce.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Subscription Plan Operations Controller - handles partial updates, lifecycle actions
///     (activate/deactivate/archive/clone), analytics, pricing, and plan configuration.
/// </summary>
[ApiVersion("1.0")]
[Route("api")]
[Microsoft.AspNetCore.Http.Tags("commerce/subscriptions/plans")]
[Authorize]
public sealed class SubscriptionPlanOperationsController(ISender sender) : BaseApiController
{
    #region Analytics & Insights - /v1/subscription-plans/{planId}/...

    /// <summary>
    ///     Get subscription plan usage statistics
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Usage statistics for the plan</returns>
    [HttpGet("v{version:apiVersion}/subscription-plans/{planId:guid}/usage")]
    [EndpointSummary("Get subscription plan usage statistics")]
    [EndpointDescription("Retrieves usage statistics for a specific subscription plan.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscriptionPlanUsage(Guid planId, CancellationToken ct)
    {
        return Ok(await sender.Send(new GetSubscriptionPlanUsageStatisticsQuery(planId), ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Get suggested plan upgrades
    /// </summary>
    /// <param name="planId">Current plan ID</param>
    /// <param name="users">Number of users</param>
    /// <param name="storageMb">Storage requirements in MB</param>
    /// <param name="apiCalls">API calls per month</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Suggested upgrade plans</returns>
    [HttpGet("v{version:apiVersion}/subscription-plans/{planId:guid}/suggest-upgrades")]
    [EndpointSummary("Get suggested plan upgrades")]
    [EndpointDescription("Suggests upgrade plans based on current usage requirements.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuggestedPlanUpgrades(Guid planId, [FromQuery] int users, [FromQuery] long storageMb, [FromQuery] long apiCalls, CancellationToken ct)
    {
        return Ok(await sender.Send(new SuggestSubscriptionPlanUpgradesQuery(planId, users, storageMb, apiCalls), ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Calculate pricing for a subscription plan
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="tenantId">Optional tenant ID for tenant-specific pricing</param>
    /// <param name="discountCode">Optional discount code</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Detailed pricing breakdown</returns>
    [HttpGet("v{version:apiVersion}/subscription-plans/{planId:guid}/pricing")]
    [EndpointSummary("Calculate pricing for a subscription plan")]
    [EndpointDescription("Calculates the total cost for a subscription plan including all applicable taxes, fees, and discounts.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CalculateSubscriptionPlanPricing(Guid planId, [FromQuery] Guid? tenantId, [FromQuery] string? discountCode, CancellationToken ct)
    {
        return Ok(await sender.Send(new CalculatePricingQuery(planId, tenantId, discountCode), ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Validate subscription plan limits
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="body">Validation request with usage requirements</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Validation result</returns>
    [HttpPost("v{version:apiVersion}/subscription-plans/{planId:guid}:validate-limits")]
    [NoBusinessMutationEndpoint("This POST-shaped limit validation is read-only.")]
    [EndpointSummary("Validate subscription plan limits")]
    [EndpointDescription("Validates whether the specified usage fits within the plan limits. Custom action per Google API guidelines.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateSubscriptionPlanLimits(Guid planId, [FromBody] ValidateLimitsRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var result = await sender.Send(new ValidateSubscriptionPlanLimitsQuery(planId, body.Users, body.StorageMb, body.ApiCalls), ct).ConfigureAwait(false);
        return Ok(result);
    }

    #endregion

    #region Partial Updates - /v1/subscription-plans/{planId}/...

    /// <summary>
    ///     Partially update subscription plan details
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="body">Update details</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPatch("v{version:apiVersion}/subscription-plans/{planId:guid}/details")]
    [EndpointSummary("Partially update subscription plan details")]
    [EndpointDescription("Updates specific fields of a subscription plan's details.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSubscriptionPlanDetails(Guid planId, [FromBody] UpdateDetailsRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new UpdateSubscriptionPlanCommand(planId, body.Name, body.Description, body.SortOrder), ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    ///     Update subscription plan pricing
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="body">Pricing update</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPatch("v{version:apiVersion}/subscription-plans/{planId:guid}/pricing")]
    [EndpointSummary("Update subscription plan pricing")]
    [EndpointDescription("Updates the pricing for a subscription plan.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSubscriptionPlanPricing(Guid planId, [FromBody] UpdatePricingRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new UpdateSubscriptionPlanPricingCommand(planId, body.MonthlyPriceInCents, body.AnnualPriceInCents), ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    ///     Update subscription plan limits
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="body">Limits update</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPatch("v{version:apiVersion}/subscription-plans/{planId:guid}/limits")]
    [EndpointSummary("Update subscription plan limits")]
    [EndpointDescription("Updates the limits for a subscription plan.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSubscriptionPlanLimits(Guid planId, [FromBody] UpdateLimitsRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new UpdateSubscriptionPlanLimitsCommand(planId, body.MaxUsers, body.MaxStorageMb, body.MaxApiCallsPerMonth), ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    ///     Update subscription plan features
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="body">Features update</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPatch("v{version:apiVersion}/subscription-plans/{planId:guid}/features")]
    [EndpointSummary("Update subscription plan features")]
    [EndpointDescription("Updates the features for a subscription plan.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSubscriptionPlanFeatures(Guid planId, [FromBody] UpdateFeaturesRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new UpdateSubscriptionPlanFeaturesCommand(planId, body.HasPrioritySupport, body.HasAdvancedAnalytics, body.HasCustomBranding, body.Features), ct).ConfigureAwait(false);
        return NoContent();
    }

    #endregion

    #region Lifecycle Actions - /v1/subscription-plans/{planId}:action

    /// <summary>
    ///     Activate subscription plan
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/subscription-plans/{planId:guid}:activate")]
    [EndpointSummary("Activate subscription plan")]
    [EndpointDescription("Activates a subscription plan by ID.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActivateSubscriptionPlan(Guid planId, CancellationToken ct)
    {
        await sender.Send(new ActivateSubscriptionPlanCommand(planId), ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    ///     Deactivate subscription plan
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/subscription-plans/{planId:guid}:deactivate")]
    [EndpointSummary("Deactivate subscription plan")]
    [EndpointDescription("Deactivates a subscription plan by ID.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeactivateSubscriptionPlan(Guid planId, CancellationToken ct)
    {
        await sender.Send(new DeactivateSubscriptionPlanCommand(planId), ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    ///     Archive subscription plan
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/subscription-plans/{planId:guid}:archive")]
    [EndpointSummary("Archive subscription plan")]
    [EndpointDescription("Archives a subscription plan, making it unavailable for new subscriptions while preserving existing subscriptions.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ArchiveSubscriptionPlan(Guid planId, CancellationToken ct)
    {
        await sender.Send(new ArchiveSubscriptionPlanCommand(planId), ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    ///     Clone subscription plan
    /// </summary>
    /// <param name="planId">Plan ID to clone</param>
    /// <param name="body">Clone request with new name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created plan ID</returns>
    [HttpPost("v{version:apiVersion}/subscription-plans/{planId:guid}:clone")]
    [EndpointSummary("Clone subscription plan")]
    [EndpointDescription("Creates a copy of an existing subscription plan with a new name and slug.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CloneSubscriptionPlan(Guid planId, [FromBody] CloneSubscriptionPlanRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var newPlanId = await sender.Send(new CloneSubscriptionPlanCommand(planId, body.NewName, body.NewSlug), ct).ConfigureAwait(false);
        return CreatedAtAction("GetSubscriptionPlanById", "SubscriptionPlansCrud", new { planId = newPlanId }, new { id = newPlanId });
    }

    /// <summary>
    ///     Set subscription plan featured status
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="body">Featured status configuration</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/subscription-plans/{planId:guid}:featured")]
    [EndpointSummary("Set subscription plan featured status")]
    [EndpointDescription("Sets whether a subscription plan is featured or not.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetSubscriptionPlanFeatured(Guid planId, [FromBody] SetFeaturedRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new SetSubscriptionPlanFeaturedCommand(planId, body.Featured), ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    ///     Set subscription plan external ID
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="body">External ID configuration</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("v{version:apiVersion}/subscription-plans/{planId:guid}:external-id")]
    [EndpointSummary("Set subscription plan external ID")]
    [EndpointDescription("Sets the external system ID for subscription plan integration.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetSubscriptionPlanExternalId(Guid planId, [FromBody] SetExternalIdRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new SetSubscriptionPlanExternalIdCommand(planId, body.ExternalId), ct).ConfigureAwait(false);
        return NoContent();
    }

    #endregion

    #region Request Records

    // POST /subscription-plans/{planId}:validate-limits
    public sealed record ValidateLimitsRequest(int Users, long StorageMb, long ApiCalls);

    // PATCH style updates separated by concern
    public sealed record UpdateDetailsRequest(Guid PlanId, string Name, string? Description, int? SortOrder);

    public sealed record UpdatePricingRequest(long MonthlyPriceInCents, long? AnnualPriceInCents);

    public sealed record UpdateLimitsRequest(int? MaxUsers, long? MaxStorageMb, long? MaxApiCallsPerMonth);

    public sealed record UpdateFeaturesRequest(bool? HasPrioritySupport, bool? HasAdvancedAnalytics, bool? HasCustomBranding, string? Features);

    public sealed record SetFeaturedRequest(bool Featured = true);

    public sealed record SetExternalIdRequest(string ExternalId);

    // POST /subscription-plans/{planId}:clone
    public sealed record CloneSubscriptionPlanRequest(string NewName, string NewSlug);

    #endregion
}
