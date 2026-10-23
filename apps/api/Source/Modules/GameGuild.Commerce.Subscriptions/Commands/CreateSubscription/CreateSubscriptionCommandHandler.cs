using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Command handler for creating a new subscription. When promo codes are supplied
///     (issue #418) they are quoted through the unified promo engine via
///     <see cref="GameGuild.Commerce.ISubscriptionPromoCoordinator"/>: the subscription is
///     created on the discounted amount, trial-extension codes extend (or grant) the trial,
///     free-months codes skip billing periods, and one usage row per applied code is
///     recorded and linked to the created subscription.
/// </summary>
public sealed class CreateSubscriptionCommandHandler(
    ISubscriptionRepository subscriptionRepository,
    ISubscriptionPlanRepository planRepository,
    ILogger<CreateSubscriptionCommandHandler> logger,
    GameGuild.Commerce.ISubscriptionPromoCoordinator? promoCoordinator = null) : ICommandHandler<CreateSubscriptionCommand, Guid>
{
    public async Task<Guid> Handle(CreateSubscriptionCommand request, CancellationToken cancellationToken)
    {
        // Issue #416: fail closed — an enterprise plan still carrying the -1 custom-quote
        // sentinel cannot be checked out until a concrete price is configured.
        var plan = await planRepository.GetByIdAsync(request.PlanId, cancellationToken).ConfigureAwait(false)
            ?? throw new GameGuild.EntityNotFoundException("SubscriptionPlan", request.PlanId);
        if (plan.RequiresCustomQuote())
        {
            throw new SubscriptionPlanCustomQuotePendingException(request.PlanId);
        }

        // Issue #418: quote promo codes against the plan before creating the subscription.
        GameGuild.Commerce.SubscriptionPromoQuoteResult? promoQuote = null;
        if (request.PromoCodes is { Count: > 0 })
        {
            if (promoCoordinator is null)
            {
                throw new InvalidOperationException(
                    "Promo codes were supplied but no promo coordinator is registered for subscription creation.");
            }

            promoQuote = await promoCoordinator.QuoteForSubscriptionCreationAsync(
                new GameGuild.Commerce.SubscriptionPromoQuoteRequest(
                    request.PlanId,
                    request.Amount,
                    request.PromoCodes,
                    request.CreatedByUserId,
                    request.ProductId,
                    request.FulfilledOrderId),
                cancellationToken).ConfigureAwait(false);

            if (promoQuote.AppliedCodes.Count == 0)
            {
                // Fail closed: checkout must not silently drop a supplied promo. The client
                // previews codes through promo-codes:validate first; a total rejection here
                // means the code expired or was fully consumed between preview and create.
                var reasons = string.Join("; ", promoQuote.RejectedCodes.Select(r => $"{r.Code}: {r.Reason}"));
                throw new InvalidOperationException(
                    $"None of the supplied promo codes could be applied to plan {request.PlanId}. {reasons}");
            }
        }

        var effectiveAmount = promoQuote?.FinalAmount ?? request.Amount;

        var startDate = request.StartDate ?? SystemClock.UtcNow;
        var trialEndDate = request.TrialDays.HasValue ? startDate.AddDays(request.TrialDays.Value) : (DateTime?) null;

        var subscription = new Subscription(
            request.TenantId,
            request.PlanId,
            request.CreatedByUserId,
            request.BillingCycle,
            new Money(effectiveAmount),
            startDate,
            trialEndDate);

        // Issue #418: consume trial-extension and free-months grants from the applied codes.
        var promoTrialDays = promoQuote?.AppliedCodes.Sum(c => c.TrialExtensionDays ?? 0) ?? 0;
        if (promoTrialDays > 0)
        {
            subscription.ExtendTrial(promoTrialDays);
        }

        var promoFreeMonths = promoQuote?.AppliedCodes.Sum(c => c.FreeMonthsGranted ?? 0) ?? 0;
        if (promoFreeMonths > 0)
        {
            subscription.SkipBillingPeriods(promoFreeMonths);
        }

        // Associate with fulfilled order for economic audit trail (if provided)
        if (request.FulfilledOrderId.HasValue)
        {
            subscription.SetFulfilledOrderId(request.FulfilledOrderId.Value);
            logger.LogInformation(
                "Subscription {SubscriptionId} linked to fulfilled order {OrderId}",
                subscription.Id, request.FulfilledOrderId.Value);
        }
        else
        {
            // Log warning for subscriptions created without order linkage (legacy/migration scenarios)
            logger.LogWarning(
                "Subscription {SubscriptionId} created without FulfilledOrderId linkage. " +
                "This should only occur for legacy data migration or admin corrections.",
                subscription.Id);
        }

        await subscriptionRepository.AddAsync(subscription, cancellationToken).ConfigureAwait(false);

        // Issue #418: persist one usage row per applied code, linked to the created
        // subscription (idempotent, fail-closed on limits inside the recorder).
        if (promoQuote is { AppliedCodes.Count: > 0 } && promoCoordinator is not null)
        {
            await promoCoordinator.RecordSubscriptionUsageAsync(
                new GameGuild.Commerce.SubscriptionPromoUsageRecordRequest(
                    subscription.Id,
                    request.CreatedByUserId,
                    promoQuote.AppliedCodes,
                    promoQuote.Currency,
                    request.FulfilledOrderId,
                    request.PromoCodeIdempotencyKey),
                cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Subscription {SubscriptionId} created with {PromoCount} applied promo code(s); amount {OriginalAmount} → {FinalAmount} {Currency}",
                subscription.Id,
                promoQuote.AppliedCodes.Count,
                promoQuote.OriginalAmount,
                effectiveAmount,
                promoQuote.Currency);
        }

        return subscription.Id;
    }
}
