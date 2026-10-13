using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Orders;

/// <summary>
///     Rule-based product recommendations (issue #401): "frequently bought together"
///     co-occurrence from completed orders, bundle-savings hints and complete-the-bundle
///     suggestions. Deterministic v1 rules only — ML personalization is deferred. Lives in
///     the Orders module because co-occurrence reads order history.
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/products")]
[Microsoft.AspNetCore.Http.Tags("products")]
[Authorize]
public class ProductRecommendationsController(IMediator mediator) : BaseApiController
{
    /// <summary>
    /// Get rule-based recommendations for a product
    /// </summary>
    /// <param name="productId">Anchor product ID</param>
    /// <param name="take">Maximum frequently-bought-together items (1–20)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Frequently-bought-together, bundle savings hints, complete-the-bundle items</returns>
    [HttpGet("{productId:guid}/recommendations")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductRecommendationsDto>> GetRecommendations(
        Guid productId,
        [FromQuery] int take = 5,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetProductRecommendationsQuery(productId, take), cancellationToken).ConfigureAwait(false);
        return result is null ? NotFound() : Ok(result);
    }
}
