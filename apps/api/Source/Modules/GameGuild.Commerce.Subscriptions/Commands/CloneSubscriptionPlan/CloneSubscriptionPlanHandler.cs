using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Handler for CloneSubscriptionPlanCommand.
///     Creates a new plan based on source plan's settings.
/// </summary>
public sealed class CloneSubscriptionPlanHandler(IApplicationDbContext context, ISubscriptionPlanListCache planListCache)
    : ICommandHandler<CloneSubscriptionPlanCommand, Guid>
{
    public async Task<Guid> Handle(CloneSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        var sourcePlan = await context.Set<SubscriptionPlan>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.SourcePlanId, cancellationToken)
            ?? throw new InvalidOperationException($"Subscription plan {request.SourcePlanId} not found");

        // Use constructor to create new plan with required parameters
        var newPlan = new SubscriptionPlan(
            request.NewName,
            request.NewSlug,
            sourcePlan.MonthlyPriceInCents,
            sourcePlan.Currency,
            sourcePlan.Description);

        // Copy additional settings using available methods/setters
        newPlan.AnnualPriceInCents = sourcePlan.AnnualPriceInCents;
        newPlan.UpdateLimits(sourcePlan.MaxUsers, sourcePlan.MaxStorageMb, sourcePlan.MaxApiCallsPerMonth);
        newPlan.UpdateFeatures(sourcePlan.HasPrioritySupport, sourcePlan.HasAdvancedAnalytics, sourcePlan.HasCustomBranding, sourcePlan.Features);
        newPlan.SortOrder = sourcePlan.SortOrder;
        newPlan.TrialPeriodDays = sourcePlan.TrialPeriodDays;
        // Clone starts as inactive
        newPlan.Deactivate();

        context.Set<SubscriptionPlan>().Add(newPlan);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Clones start deactivated, so the public lists do not change — but invalidating
        // keeps the cache coherent with the plan table (issue #416).
        planListCache.Invalidate();

        return newPlan.Id;
    }
}
