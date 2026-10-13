using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Handler for fully updating a pricing rule, optionally replacing its volume tiers.
/// </summary>
public sealed class UpdatePricingRuleCommandHandler(IPricingRuleRepository pricingRuleRepository)
    : ICommandHandler<UpdatePricingRuleCommand, PricingRuleDto>
{
    public async Task<PricingRuleDto> Handle(UpdatePricingRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rule = await pricingRuleRepository.GetByIdAsync(request.RuleId, cancellationToken).ConfigureAwait(false)
            ?? throw new PricingRuleNotFoundException(request.RuleId);

        rule.ProductId = request.ProductId;
        rule.Name = request.Name.Trim();
        rule.Description = request.Description;
        rule.RuleType = request.RuleType;
        rule.Priority = request.Priority;
        rule.IsActive = request.IsActive;
        rule.StartDate = request.StartDate;
        rule.EndDate = request.EndDate;
        rule.MinQuantity = request.MinQuantity;
        rule.MaxQuantity = request.MaxQuantity;
        rule.DiscountPercentage = request.DiscountPercentage;
        rule.DiscountAmount = request.DiscountAmount;
        rule.FixedPrice = request.FixedPrice;
        rule.BuyQuantity = request.BuyQuantity;
        rule.GetQuantity = request.GetQuantity;
        rule.TimeStart = request.TimeStart;
        rule.TimeEnd = request.TimeEnd;
        rule.DaysOfWeek = request.DaysOfWeek;
        rule.Region = request.Region;
        rule.CustomerSegment = request.CustomerSegment;

        if (request.Tiers is not null)
        {
            PricingRuleMutationHelpers.ApplyTiers(rule, request.Tiers);
        }

        // UpdateAsync touches UpdatedAt (and the DbContext bumps Version), which changes the
        // engine's rule-change stamp and invalidates cached rule sets for the affected product.
        await pricingRuleRepository.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);

        return rule.ToDto();
    }
}
