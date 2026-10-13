using GameGuild.CQRS;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Query handler for getting featured subscription plans
/// </summary>
/// <remarks>
///     Backed by the 5-minute plan-list cache (issue #416); invalidated by every plan
///     mutation command.
/// </remarks>
public sealed class GetFeaturedSubscriptionPlansQueryHandler(
    ISubscriptionPlanRepository subscriptionPlanRepository,
    ISubscriptionPlanListCache planListCache)
    : IQueryHandler<GetFeaturedSubscriptionPlansQuery, IEnumerable<SubscriptionPlan>>
{
    public async Task<IEnumerable<SubscriptionPlan>> Handle(GetFeaturedSubscriptionPlansQuery request, CancellationToken cancellationToken)
    {
        return await planListCache
            .GetOrCreateFeaturedPlansAsync(
                ct => subscriptionPlanRepository.GetFeaturedAsync(ct),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
