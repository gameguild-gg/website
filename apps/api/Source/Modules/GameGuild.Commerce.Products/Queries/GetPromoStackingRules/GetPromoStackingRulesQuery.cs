using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Query to list promo stacking rules
/// </summary>
/// <param name="IncludeInactive">Whether to include inactive rules</param>
public sealed record GetPromoStackingRulesQuery(bool IncludeInactive = true) : IQuery<IReadOnlyList<PromoStackingRuleDto>>;

/// <summary>
/// Handler for <see cref="GetPromoStackingRulesQuery"/>
/// </summary>
public sealed class GetPromoStackingRulesQueryHandler(IPromoStackingRuleRepository repository)
    : IQueryHandler<GetPromoStackingRulesQuery, IReadOnlyList<PromoStackingRuleDto>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PromoStackingRuleDto>> Handle(GetPromoStackingRulesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rules = await repository.ListAsync(request.IncludeInactive, cancellationToken).ConfigureAwait(false);
        return rules.Select(r => r.ToDto()).ToList();
    }
}
