using GameGuild.Commerce.Subscriptions;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Shared plan-eligibility rule for promo code commands (issue #418). A plan-scoped promo
/// code must reference an existing subscription plan; the check runs through the
/// Subscriptions module's <see cref="ISubscriptionPlanRepository"/> (Products already
/// references Subscriptions, so the dependency direction is unchanged). Validators that
/// are constructed without a repository (unit tests) skip the persistence check.
/// </summary>
public static class PromoPlanEligibility
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="planId"/> is not plan-scoped (null) or the
    /// referenced subscription plan exists. Returns <c>true</c> when no repository is
    /// available (persistence check skipped).
    /// </summary>
    public static async Task<bool> PlanExistsOrNullAsync(
        ISubscriptionPlanRepository? planRepository,
        Guid? planId,
        CancellationToken cancellationToken = default)
    {
        if (!planId.HasValue || planId.Value == Guid.Empty)
        {
            return true;
        }

        if (planRepository is null)
        {
            return true;
        }

        var plan = await planRepository.GetByIdAsync(planId.Value, cancellationToken).ConfigureAwait(false);
        return plan is not null;
    }
}
