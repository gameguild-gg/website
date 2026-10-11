using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Handler for FullUpdateSubscriptionPlanCommand
/// </summary>
/// <remarks>
///     When <see cref="FullUpdateSubscriptionPlanCommand.Slug" /> is omitted (issue #416),
///     a URL-safe slug is regenerated from the new plan name and de-duplicated against the
///     unique slug index (excluding the plan being updated). An explicit slug is used
///     verbatim after format validation.
/// </remarks>
public sealed class FullUpdateSubscriptionPlanHandler(
    IApplicationDbContext context,
    ISubscriptionPlanSlugGenerator slugGenerator,
    ISubscriptionPlanListCache planListCache) : ICommandHandler<FullUpdateSubscriptionPlanCommand>
{
    public async Task<Unit> Handle(FullUpdateSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await context.Set<SubscriptionPlan>()
            .FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken)
            ?? throw new InvalidOperationException($"Subscription plan {request.PlanId} not found");

        var slug = request.Slug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = await slugGenerator.GenerateUniqueSlugAsync(
                    request.Name,
                    async (candidate, token) => await context.Set<SubscriptionPlan>()
                        .AnyAsync(p => p.Slug == candidate && p.Id != request.PlanId, token)
                        .ConfigureAwait(false),
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Could not generate a unique slug from plan name '{request.Name}'. Provide an explicit slug.");
        }

        // Full update - replace all fields
        plan.Name = request.Name;
        plan.Slug = slug;
        plan.Description = request.Description;
        plan.MonthlyPriceInCents = request.MonthlyPriceInCents;
        plan.AnnualPriceInCents = request.AnnualPriceInCents;
        plan.MaxUsers = request.MaxUsers;
        plan.MaxStorageMb = request.MaxStorageMb;
        plan.MaxApiCallsPerMonth = request.MaxApiCallsPerMonth;
        plan.HasPrioritySupport = request.HasPrioritySupport ?? false;
        plan.HasAdvancedAnalytics = request.HasAdvancedAnalytics ?? false;
        plan.HasCustomBranding = request.HasCustomBranding ?? false;
        plan.Features = request.Features;
        plan.SortOrder = request.SortOrder ?? 0;
        if (request.TrialPeriodDays.HasValue)
        {
            plan.TrialPeriodDays = request.TrialPeriodDays.Value;
        }
        plan.Touch();

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Issue #416: "Cache invalidation on plan updates".
        planListCache.Invalidate();

        return Unit.Value;
    }
}
