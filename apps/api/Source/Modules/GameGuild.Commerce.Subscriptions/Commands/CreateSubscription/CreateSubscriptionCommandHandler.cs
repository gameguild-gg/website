using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Command handler for creating a new subscription
/// </summary>
public sealed class CreateSubscriptionCommandHandler(
    ISubscriptionRepository subscriptionRepository,
    ISubscriptionPlanRepository planRepository,
    ILogger<CreateSubscriptionCommandHandler> logger) : ICommandHandler<CreateSubscriptionCommand, Guid>
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

        var startDate = request.StartDate ?? SystemClock.UtcNow;
        var trialEndDate = request.TrialDays.HasValue ? startDate.AddDays(request.TrialDays.Value) : (DateTime?) null;

        var subscription = new Subscription(
            request.TenantId, 
            request.PlanId, 
            request.CreatedByUserId, 
            request.BillingCycle, 
            new Money(request.Amount), 
            startDate, 
            trialEndDate);

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

        return subscription.Id;
    }
}
