using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Command to set or update product pricing with audit trail.
/// Setting product pricing is a revenue-generation action and requires the
/// monetization permission (issue #346, Monetize) on every dispatch path.
/// </summary>
/// <param name="ProductId">Product ID</param>
/// <param name="Name">Pricing option name</param>
/// <param name="BasePrice">Base price</param>
/// <param name="Currency">Currency code</param>
/// <param name="SalePrice">Optional sale price</param>
/// <param name="SaleStartDate">Sale start date</param>
/// <param name="SaleEndDate">Sale end date</param>
/// <param name="IsDefault">Whether this is the default pricing</param>
/// <param name="PricingId">Existing pricing ID to update (null = create new)</param>
/// <param name="UpdatedByUserId">User making the change for audit trail</param>
[AuthorizeRequest(MonetizationPermission.Keys.Monetize)]
public sealed record SetProductPricingCommand(
    Guid ProductId,
    string Name,
    decimal BasePrice,
    string Currency = "USD",
    decimal? SalePrice = null,
    DateTime? SaleStartDate = null,
    DateTime? SaleEndDate = null,
    bool IsDefault = false,
    Guid? PricingId = null,
    Guid? UpdatedByUserId = null
) : ICommand<ProductPricingDto>;
