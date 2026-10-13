using GameGuild.CQRS;
using GameGuild.Commerce;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Handler for creating a dynamic pricing rule with its volume tiers.
/// </summary>
public sealed class CreatePricingRuleCommandHandler(IPricingRuleRepository pricingRuleRepository)
    : ICommandHandler<CreatePricingRuleCommand, PricingRuleDto>
{
    public async Task<PricingRuleDto> Handle(CreatePricingRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rule = new PricingRule
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            Name = request.Name.Trim(),
            Description = request.Description,
            RuleType = request.RuleType,
            Priority = request.Priority,
            IsActive = request.IsActive,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MinQuantity = request.MinQuantity,
            MaxQuantity = request.MaxQuantity,
            DiscountPercentage = request.DiscountPercentage,
            DiscountAmount = request.DiscountAmount,
            FixedPrice = request.FixedPrice,
            BuyQuantity = request.BuyQuantity,
            GetQuantity = request.GetQuantity,
            TimeStart = request.TimeStart,
            TimeEnd = request.TimeEnd,
            DaysOfWeek = request.DaysOfWeek,
            Region = request.Region,
            CustomerSegment = request.CustomerSegment
        };

        PricingRuleMutationHelpers.ApplyTiers(rule, request.Tiers);

        await pricingRuleRepository.AddAsync(rule, cancellationToken).ConfigureAwait(false);
        await pricingRuleRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return rule.ToDto();
    }
}
