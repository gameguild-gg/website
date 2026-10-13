using FluentValidation;
using GameGuild.Commerce.Payments;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
/// Query (issue #417): the current price breakdown of a subscription through the unified
/// pricing engine — the same single calculation path every other pricing surface uses.
/// </summary>
/// <param name="SubscriptionId">Subscription identifier</param>
/// <param name="PromoCodes">Optional promo codes to include in the breakdown</param>
public sealed record GetSubscriptionPricingBreakdownQuery(
    Guid SubscriptionId,
    IReadOnlyList<string>? PromoCodes = null) : IQuery<PricingCalculationResult>;

/// <summary>Validator for <see cref="GetSubscriptionPricingBreakdownQuery"/>.</summary>
public sealed class GetSubscriptionPricingBreakdownQueryValidator : AbstractValidator<GetSubscriptionPricingBreakdownQuery>
{
    /// <summary>Initializes the validator.</summary>
    public GetSubscriptionPricingBreakdownQueryValidator()
    {
        RuleFor(query => query.SubscriptionId).NotEmpty();
    }
}

/// <summary>Handler for <see cref="GetSubscriptionPricingBreakdownQuery"/>.</summary>
public sealed class GetSubscriptionPricingBreakdownQueryHandler(
    ISubscriptionRepository subscriptionRepository,
    IPricingEngine pricingEngine)
    : IQueryHandler<GetSubscriptionPricingBreakdownQuery, PricingCalculationResult>
{
    /// <inheritdoc />
    public async Task<PricingCalculationResult> Handle(
        GetSubscriptionPricingBreakdownQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subscription = await subscriptionRepository.GetByIdAsync(request.SubscriptionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Subscription {request.SubscriptionId} not found.");

        return await pricingEngine.CalculateAsync(
            new PricingCalculationRequest(
                PlanId: subscription.PlanId,
                UserId: subscription.CreatedByUserId,
                BillingCycle: subscription.BillingCycle,
                Currency: subscription.Amount.Currency,
                PromoCodes: request.PromoCodes,
                IncludeTax: false,
                ApplyCredits: false,
                RecordDecision: false,
                SubscriptionId: subscription.Id),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Query (issue #417): preview a plan change for a subscription — the immediate prorated
/// charge and next-renewal amount via the unified pricing engine, using the subscription's
/// real billing period. Read-only: nothing is applied, recorded, or consumed.
/// </summary>
/// <param name="SubscriptionId">Subscription identifier</param>
/// <param name="NewPlanId">Plan being considered</param>
/// <param name="PromoCodes">Optional promo codes to preview</param>
public sealed record PreviewSubscriptionPlanChangeQuery(
    Guid SubscriptionId,
    Guid NewPlanId,
    IReadOnlyList<string>? PromoCodes = null) : IQuery<PricingPreviewResult>;

/// <summary>Validator for <see cref="PreviewSubscriptionPlanChangeQuery"/>.</summary>
public sealed class PreviewSubscriptionPlanChangeQueryValidator : AbstractValidator<PreviewSubscriptionPlanChangeQuery>
{
    /// <summary>Initializes the validator.</summary>
    public PreviewSubscriptionPlanChangeQueryValidator()
    {
        RuleFor(query => query.SubscriptionId).NotEmpty();
        RuleFor(query => query.NewPlanId).NotEmpty();
    }
}

/// <summary>Handler for <see cref="PreviewSubscriptionPlanChangeQuery"/>.</summary>
public sealed class PreviewSubscriptionPlanChangeQueryHandler(
    ISubscriptionRepository subscriptionRepository,
    IPricingEngine pricingEngine)
    : IQueryHandler<PreviewSubscriptionPlanChangeQuery, PricingPreviewResult>
{
    /// <inheritdoc />
    public async Task<PricingPreviewResult> Handle(
        PreviewSubscriptionPlanChangeQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subscription = await subscriptionRepository.GetByIdAsync(request.SubscriptionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Subscription {request.SubscriptionId} not found.");

        return await pricingEngine.PreviewAsync(
            new PricingPreviewRequest(
                CurrentPlanId: subscription.PlanId,
                NewPlanId: request.NewPlanId,
                PeriodStart: subscription.CurrentPeriodStart,
                PeriodEnd: subscription.CurrentPeriodEnd,
                BillingCycle: subscription.BillingCycle,
                Currency: subscription.Amount.Currency,
                UserId: subscription.CreatedByUserId,
                PromoCodes: request.PromoCodes,
                IncludeTax: false),
            cancellationToken).ConfigureAwait(false);
    }
}
