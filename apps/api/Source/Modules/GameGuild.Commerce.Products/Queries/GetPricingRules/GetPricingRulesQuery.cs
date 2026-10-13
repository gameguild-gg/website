using GameGuild.CQRS;
using GameGuild.Commerce;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Query to get paginated pricing rules with optional filters (issue #395).
/// </summary>
/// <param name="IsActive">Filter by active status</param>
/// <param name="RuleType">Filter by rule type</param>
/// <param name="ProductId">Filter by product (includes global rules)</param>
/// <param name="SearchTerm">Search term for name/description</param>
/// <param name="Skip">Items to skip</param>
/// <param name="Take">Items to take</param>
public sealed record GetPricingRulesQuery(
    bool? IsActive = null,
    PricingRuleType? RuleType = null,
    Guid? ProductId = null,
    string? SearchTerm = null,
    int Skip = 0,
    int Take = 50
) : IQuery<PagedResult<PricingRuleDto>>;

/// <summary>
/// Handler for the paginated pricing rules query.
/// </summary>
public sealed class GetPricingRulesQueryHandler(IPricingRuleRepository pricingRuleRepository)
    : IQueryHandler<GetPricingRulesQuery, PagedResult<PricingRuleDto>>
{
    public async Task<PagedResult<PricingRuleDto>> Handle(GetPricingRulesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (items, totalCount) = await pricingRuleRepository.GetPagedAsync(
            request.IsActive,
            request.RuleType,
            request.ProductId,
            request.SearchTerm,
            request.Skip,
            request.Take,
            cancellationToken).ConfigureAwait(false);

        var skip = Math.Max(0, request.Skip);
        var take = Math.Clamp(request.Take, 1, 200);

        return new PagedResult<PricingRuleDto>(
            items.Select(r => r.ToDto()).ToList(),
            totalCount,
            skip,
            take);
    }
}
