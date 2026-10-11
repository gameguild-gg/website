using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Query to get a single pricing rule by ID, including its tiers (issue #395).
/// </summary>
/// <param name="RuleId">Rule ID</param>
public sealed record GetPricingRuleByIdQuery(Guid RuleId) : IQuery<PricingRuleDto>;

/// <summary>
/// Handler for the single pricing rule query.
/// </summary>
public sealed class GetPricingRuleByIdQueryHandler(IPricingRuleRepository pricingRuleRepository)
    : IQueryHandler<GetPricingRuleByIdQuery, PricingRuleDto?>
{
    public async Task<PricingRuleDto?> Handle(GetPricingRuleByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rule = await pricingRuleRepository.GetByIdAsync(request.RuleId, cancellationToken).ConfigureAwait(false);
        return rule?.ToDto();
    }
}
