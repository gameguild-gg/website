using GameGuild.CQRS;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Query handler for getting active subscription plans.
/// </summary>
/// <remarks>
///     Backed by the 5-minute plan-list cache (issue #416: "Plan list queries cached for
///     5 minutes"); every plan mutation command invalidates it.
/// </remarks>
public sealed class GetActiveSubscriptionPlansQueryHandler(
    ISubscriptionPlanRepository subscriptionPlanRepository,
    ISubscriptionPlanListCache planListCache)
    : IRequestHandler<GetActiveSubscriptionPlansQuery, IEnumerable<SubscriptionPlan>>
{
    public async Task<IEnumerable<SubscriptionPlan>> Handle(GetActiveSubscriptionPlansQuery request, CancellationToken cancellationToken)
    {
        return await planListCache
            .GetOrCreateActivePlansAsync(
                ct => subscriptionPlanRepository.GetActiveAsync(ct),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
