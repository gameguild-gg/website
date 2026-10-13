using GameGuild.CQRS;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Base class for subscription command handlers that need to fetch, validate, and save a subscription.
///     Reduces boilerplate across the 26+ subscription command handlers.
/// </summary>
/// <typeparam name="TCommand">The command type</typeparam>
public abstract class SubscriptionCommandHandlerBase<TCommand>(ISubscriptionRepository subscriptionRepository)
    : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    /// <summary>
    ///     The subscription repository for data access.
    /// </summary>
    protected readonly ISubscriptionRepository SubscriptionRepository = subscriptionRepository;

    /// <summary>
    ///     Handles the command by fetching the subscription, executing the operation, and saving changes.
    /// </summary>
    public async Task<Unit> Handle(TCommand request, CancellationToken cancellationToken)
    {
        var subscriptionId = GetSubscriptionId(request);
        var subscription = await SubscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription == null)
            throw new SubscriptionNotFoundException(subscriptionId);

        await ExecuteAsync(subscription, request, cancellationToken).ConfigureAwait(false);

        await SubscriptionRepository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }

    /// <summary>
    ///     Gets the subscription ID from the command.
    /// </summary>
    /// <param name="request">The command containing the subscription ID</param>
    /// <returns>The subscription ID</returns>
    protected abstract Guid GetSubscriptionId(TCommand request);

    /// <summary>
    ///     Executes the specific operation on the subscription.
    /// </summary>
    /// <param name="subscription">The subscription entity</param>
    /// <param name="request">The command</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected abstract Task ExecuteAsync(Subscription subscription, TCommand request, CancellationToken cancellationToken);
}

/// <summary>
///     Base class for subscription plan command handlers that need to fetch, validate, and save a subscription plan.
/// </summary>
/// <typeparam name="TCommand">The command type</typeparam>
public abstract class SubscriptionPlanCommandHandlerBase<TCommand>(
    ISubscriptionPlanRepository planRepository,
    ISubscriptionPlanListCache planListCache)
    : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    /// <summary>
    ///     The subscription plan repository for data access.
    /// </summary>
    protected readonly ISubscriptionPlanRepository PlanRepository = planRepository;

    /// <summary>
    ///     Handles the command by fetching the plan, executing the operation, and saving changes.
    /// </summary>
    public async Task<Unit> Handle(TCommand request, CancellationToken cancellationToken)
    {
        var planId = GetPlanId(request);
        var plan = await PlanRepository.GetByIdAsync(planId, cancellationToken).ConfigureAwait(false);

        if (plan == null)
            throw new EntityNotFoundException("SubscriptionPlan", planId);

        await ExecuteAsync(plan, request, cancellationToken).ConfigureAwait(false);

        await PlanRepository.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);

        // Issue #416: "Cache invalidation on plan updates" — every plan mutation resets
        // the cached public plan lists.
        planListCache.Invalidate();

        return Unit.Value;
    }

    /// <summary>
    ///     Gets the plan ID from the command.
    /// </summary>
    /// <param name="request">The command containing the plan ID</param>
    /// <returns>The plan ID</returns>
    protected abstract Guid GetPlanId(TCommand request);

    /// <summary>
    ///     Executes the specific operation on the plan.
    /// </summary>
    /// <param name="plan">The subscription plan entity</param>
    /// <param name="request">The command</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected abstract Task ExecuteAsync(SubscriptionPlan plan, TCommand request, CancellationToken cancellationToken);
}
