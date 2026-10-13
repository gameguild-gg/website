using GameGuild.CQRS;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Handler for SetSubscriptionPlanIntervalPriceCommand (issue #392): upserts the
///     interval price point and invalidates the public plan-list cache.
/// </summary>
public sealed class SetSubscriptionPlanIntervalPriceCommandHandler(
    ISubscriptionPlanRepository planRepository,
    ISubscriptionIntervalPricingService intervalPricingService,
    ISubscriptionPlanListCache planListCache)
    : ICommandHandler<SetSubscriptionPlanIntervalPriceCommand, PlanIntervalPricing>
{
    public async Task<PlanIntervalPricing> Handle(SetSubscriptionPlanIntervalPriceCommand request, CancellationToken cancellationToken)
    {
        var plan = await planRepository.GetByIdAsync(request.PlanId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("SubscriptionPlan", request.PlanId);

        await planRepository
            .UpsertIntervalPriceAsync(new SubscriptionPlanIntervalPrice(request.PlanId, request.Interval, request.PriceInCents), cancellationToken)
            .ConfigureAwait(false);

        planListCache.Invalidate();

        var pricing = await intervalPricingService
            .GetIntervalPricingAsync(plan, cancellationToken)
            .ConfigureAwait(false);

        return pricing.Single(point => point.Interval == request.Interval);
    }
}
