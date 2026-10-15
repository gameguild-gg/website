using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Command handler for creating a new subscription plan
/// </summary>
/// <remarks>
///     When <see cref="CreateSubscriptionPlanCommand.Slug" /> is omitted (issue #416: "Plan
///     slugs are automatically generated"), a URL-safe slug is generated from the plan name
///     and de-duplicated against the unique slug index with <c>-2</c>, <c>-3</c>, ...
///     suffixes. An explicit slug is used verbatim after format validation.
/// </remarks>
public sealed class CreateSubscriptionPlanCommandHandler(
    ISubscriptionPlanRepository subscriptionPlanRepository,
    ISubscriptionPlanSlugGenerator slugGenerator,
    ISubscriptionPlanListCache planListCache,
    ILogger<CreateSubscriptionPlanCommandHandler> logger) : ICommandHandler<CreateSubscriptionPlanCommand, Guid>
{
    public async Task<Guid> Handle(CreateSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        var slug = request.Slug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = await slugGenerator.GenerateUniqueSlugAsync(
                    request.Name,
                    async (candidate, token) => !await subscriptionPlanRepository.IsSlugUniqueAsync(candidate, null, token).ConfigureAwait(false),
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Could not generate a unique slug from plan name '{request.Name}'. Provide an explicit slug.");
        }

        // Issue #416: plan-type pricing invariants are enforced fail-closed before any write.
        SubscriptionPlanPricingRules.Validate(request.Type, request.MonthlyPriceInCents, null);
        SubscriptionPlanPricingRules.ValidateAnnualDiscountPercent(request.AnnualDiscountPercent);

        var plan = new SubscriptionPlan(
            request.Name,
            slug,
            request.MonthlyPriceInCents,
            request.Currency,
            request.Description)
        {
            // Issue #416: the create payload carries the full initial contract — type,
            // annual-discount derivation, features and limits. Later changes go through the
            // PATCH-by-concern endpoints (pricing, features, limits).
            Type = request.Type,
            AnnualDiscountPercent = request.AnnualDiscountPercent,
            Features = request.Features,
            MaxUsers = request.MaxUsers,
            MaxStorageMb = request.MaxStorageMb,
            MaxApiCallsPerMonth = request.MaxApiCallsPerMonth,
            HasPrioritySupport = request.HasPrioritySupport ?? false,
            HasAdvancedAnalytics = request.HasAdvancedAnalytics ?? false,
            HasCustomBranding = request.HasCustomBranding ?? false
        };

        await subscriptionPlanRepository.AddAsync(plan, cancellationToken).ConfigureAwait(false);

        // Issue #416: "Cache invalidation on plan updates" — every mutation resets the
        // cached public plan lists.
        planListCache.Invalidate();

        logger.LogInformation(
            "Created subscription plan {PlanId} with name {PlanName} and slug {PlanSlug}",
            plan.Id, plan.Name, plan.Slug);

        return plan.Id;
    }
}
