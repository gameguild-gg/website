using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Commerce.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Subscription Plans CRUD Controller - handles plan creation, retrieval, deletion,
///     full updates, and collection-level queries (search, filter, compare).
/// </summary>
[ApiVersion("1.0")]
[Microsoft.AspNetCore.Http.Tags("commerce/subscriptions/plans")]
[Authorize]
public sealed class SubscriptionPlansCrudController(ISender sender) : BaseApiController
{
    #region Collection Operations - /v1/subscription-plans

    /// <summary>
    ///     Create a new subscription plan
    /// </summary>
    /// <param name="body">Subscription plan creation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created subscription plan</returns>
    [HttpPost("v{version:apiVersion}/subscription-plans")]
    [EndpointSummary("Create a new subscription plan")]
    [EndpointDescription("Creates a new subscription plan with the provided information.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSubscriptionPlan([FromBody] CreatePlanRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var id = await sender.Send(new CreateSubscriptionPlanCommand(body.Name, body.Slug, body.MonthlyPriceInCents, body.Currency, body.Description), ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetSubscriptionPlanById), new { planId = id }, new { id, body.Name, body.Slug });
    }

    /// <summary>
    ///     Get subscription plans with pagination and filtering
    /// </summary>
    /// <param name="page">Page number for pagination (default: 1)</param>
    /// <param name="pageSize">Number of plans per page (default: 20, max: 100)</param>
    /// <param name="activeOnly">Filter to only active plans</param>
    /// <param name="isActive">Filter by active status</param>
    /// <param name="isFeatured">Filter by featured status (use featured=true to get featured plans)</param>
    /// <param name="q">Search term for filtering by name, description, or features</param>
    /// <param name="slug">Filter by exact slug match</param>
    /// <param name="minPrice">Minimum price in cents for price range filtering</param>
    /// <param name="maxPrice">Maximum price in cents for price range filtering</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of subscription plans</returns>
    [HttpGet("v{version:apiVersion}/subscription-plans")]
    [AllowAnonymous]
    [EndpointSummary("Get subscription plans with pagination and filtering")]
    [EndpointDescription("Retrieves a paginated list of subscription plans with optional filtering. Use query parameters: featured=true for featured plans, q=searchTerm for search, slug=value for slug lookup, minPrice/maxPrice for price range.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubscriptionPlans(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool activeOnly = false,
        [FromQuery] bool? isActive = null,
        [FromQuery(Name = "featured")] bool? isFeatured = null,
        [FromQuery] string? q = null,
        [FromQuery] string? slug = null,
        [FromQuery] long? minPrice = null,
        [FromQuery] long? maxPrice = null,
        CancellationToken ct = default
    )
    {
        var restrictToActive = User.Identity?.IsAuthenticated != true;
        if (restrictToActive)
        {
            activeOnly = true;
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        // If slug is specified, return single plan lookup
        if (!string.IsNullOrEmpty(slug))
        {
            var plan = await sender.Send(new GetSubscriptionPlanBySlugQuery(slug), ct).ConfigureAwait(false);
            if (restrictToActive && plan is not null && !plan.IsActive)
            {
                return NotFound();
            }

            return plan is null ? NotFound() : Ok(plan);
        }

        // If price range is specified, filter by price
        if (minPrice.HasValue || maxPrice.HasValue)
        {
            var priceResult = await sender.Send(new GetSubscriptionPlansByPriceRangeQuery(minPrice ?? 0, maxPrice ?? long.MaxValue), ct).ConfigureAwait(false);
            if (restrictToActive)
            {
                priceResult = priceResult.Where(plan => plan.IsActive);
            }

            return Ok(priceResult);
        }

        // If activeOnly is specified, use the simple query, otherwise use the paginated query
        if (activeOnly && page == 1 && pageSize == 20 && !isActive.HasValue && !isFeatured.HasValue && string.IsNullOrEmpty(q))
        {
            var result = await sender.Send(new GetActiveSubscriptionPlansQuery(), ct).ConfigureAwait(false);
            return Ok(result);
        }

        // If only featured filter is requested without other pagination
        if (isFeatured == true && page == 1 && pageSize == 20 && !isActive.HasValue && string.IsNullOrEmpty(q))
        {
            var featuredResult = await sender.Send(new GetFeaturedSubscriptionPlansQuery(), ct).ConfigureAwait(false);
            if (restrictToActive)
            {
                featuredResult = featuredResult.Where(plan => plan.IsActive);
            }

            return Ok(featuredResult);
        }

        // If search term is provided without pagination, use search query
        if (!string.IsNullOrEmpty(q) && page == 1 && pageSize == 20 && !isActive.HasValue && !isFeatured.HasValue)
        {
            var searchResult = await sender.Send(new SearchSubscriptionPlansQuery(q), ct).ConfigureAwait(false);
            if (restrictToActive)
            {
                searchResult = searchResult.Where(plan => plan.IsActive);
            }

            return Ok(searchResult);
        }

        var effectiveIsActive = restrictToActive ? true : isActive;
        var pagedResult = await sender.Send(new GetPagedSubscriptionPlansQuery(page, pageSize, effectiveIsActive, isFeatured, q), ct).ConfigureAwait(false);
        return Ok(pagedResult);
    }

    /// <summary>
    ///     Compare subscription plans
    /// </summary>
    /// <param name="body">Plan comparison request with base plan and comparison plan IDs</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Plan comparison results</returns>
    [HttpPost("v{version:apiVersion}/subscription-plans:compare")]
    [NoBusinessMutationEndpoint("This POST-shaped plan comparison is read-only.")]
    [EndpointSummary("Compare subscription plans")]
    [EndpointDescription("Compares multiple subscription plans side by side. Custom action per Google API guidelines.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompareSubscriptionPlans([FromBody] ComparePlansRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var result = await sender.Send(new CompareSubscriptionPlansQuery(body.BasePlanId, body.ComparePlanIds), ct).ConfigureAwait(false);
        return Ok(result);
    }

    #endregion

    #region Individual Item Operations - /v1/subscription-plans/{planId}

    /// <summary>
    ///     Check if subscription plan exists by ID
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>200 if exists, 404 if not</returns>
    [HttpHead("v{version:apiVersion}/subscription-plans/{planId:guid}")]
    [EndpointSummary("Check if subscription plan exists by ID")]
    [EndpointDescription("Checks if a subscription plan exists by ID without returning the body.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckSubscriptionPlanExistsById(Guid planId, CancellationToken ct)
    {
        var plan = await sender.Send(new GetSubscriptionPlanByIdQuery(planId), ct).ConfigureAwait(false);
        return plan is null ? NotFound() : Ok();
    }

    /// <summary>
    ///     Get subscription plan by ID
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Subscription plan details</returns>
    [HttpGet("v{version:apiVersion}/subscription-plans/{planId:guid}")]
    [EndpointSummary("Get subscription plan by ID")]
    [EndpointDescription("Retrieves detailed information for a specific subscription plan.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubscriptionPlanById(Guid planId, CancellationToken ct)
    {
        var plan = await sender.Send(new GetSubscriptionPlanByIdQuery(planId), ct).ConfigureAwait(false);
        return plan is null ? NotFound() : Ok(plan);
    }

    /// <summary>
    ///     Delete subscription plan
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("v{version:apiVersion}/subscription-plans/{planId:guid}")]
    [EndpointSummary("Delete subscription plan")]
    [EndpointDescription("Deletes a subscription plan by ID.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSubscriptionPlan(Guid planId, CancellationToken ct)
    {
        await sender.Send(new DeleteSubscriptionPlanCommand(planId), ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    ///     Full update of a subscription plan
    /// </summary>
    /// <param name="planId">Plan ID</param>
    /// <param name="body">Full plan update request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPut("v{version:apiVersion}/subscription-plans/{planId:guid}")]
    [EndpointSummary("Full update subscription plan")]
    [EndpointDescription("Performs a full replacement of subscription plan data. All fields will be updated.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PutSubscriptionPlan(Guid planId, [FromBody] PutSubscriptionPlanRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        await sender.Send(new FullUpdateSubscriptionPlanCommand(
            planId,
            body.Name,
            body.Slug,
            body.Description,
            body.MonthlyPriceInCents,
            body.AnnualPriceInCents,
            body.MaxUsers,
            body.MaxStorageMb,
            body.MaxApiCallsPerMonth,
            body.HasPrioritySupport,
            body.HasAdvancedAnalytics,
            body.HasCustomBranding,
            body.Features,
            body.SortOrder), ct).ConfigureAwait(false);
        return NoContent();
    }

    #endregion

    #region Request Records

    // POST /subscription-plans
    public sealed record CreatePlanRequest(string Name, string Slug, long MonthlyPriceInCents, string Currency = "USD", string? Description = null);

    // POST /subscription-plans:compare
    public sealed record ComparePlansRequest(Guid BasePlanId, List<Guid> ComparePlanIds);

    // PUT /subscription-plans/{planId}
    public sealed record PutSubscriptionPlanRequest(
        string Name,
        string Slug,
        string? Description,
        long MonthlyPriceInCents,
        long? AnnualPriceInCents,
        int? MaxUsers,
        long? MaxStorageMb,
        long? MaxApiCallsPerMonth,
        bool? HasPrioritySupport,
        bool? HasAdvancedAnalytics,
        bool? HasCustomBranding,
        string? Features,
        int? SortOrder);

    #endregion
}
