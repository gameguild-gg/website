using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Query to get paginated promo codes
/// </summary>
/// <param name="IsActive">Filter by active status</param>
/// <param name="Type">Filter by promo code type</param>
/// <param name="ProductId">Filter by product ID</param>
/// <param name="SearchTerm">Search term for code, name, or description</param>
/// <param name="CampaignId">Filter by campaign identifier</param>
/// <param name="Skip">Number of items to skip</param>
/// <param name="Take">Number of items to take</param>
public sealed record GetPromoCodesQuery(
    bool? IsActive = null,
    PromoCodeType? Type = null,
    Guid? ProductId = null,
    string? SearchTerm = null,
    string? CampaignId = null,
    int Skip = 0,
    int Take = 50
) : IQuery<PagedResult<PromoCodeDto>>;
