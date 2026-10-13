using GameGuild.CQRS;

namespace GameGuild.Commerce.Subscriptions;

public sealed class UpdateSubscriptionPlanCommandHandler(ISubscriptionPlanRepository repository, ISubscriptionPlanListCache planListCache)
    : SubscriptionPlanCommandHandlerBase<UpdateSubscriptionPlanCommand>(repository, planListCache)
{
    protected override Guid GetPlanId(UpdateSubscriptionPlanCommand request) => request.Id;

    protected override Task ExecuteAsync(
        SubscriptionPlan plan,
        UpdateSubscriptionPlanCommand request,
        CancellationToken cancellationToken)
    {
        plan.UpdateDetails(request.Name, request.Description, request.SortOrder);
        return Task.CompletedTask;
    }
}

public sealed class UpdateSubscriptionPlanPricingCommandHandler(ISubscriptionPlanRepository repository, ISubscriptionPlanListCache planListCache)
    : SubscriptionPlanCommandHandlerBase<UpdateSubscriptionPlanPricingCommand>(repository, planListCache)
{
    protected override Guid GetPlanId(UpdateSubscriptionPlanPricingCommand request) => request.Id;

    protected override Task ExecuteAsync(
        SubscriptionPlan plan,
        UpdateSubscriptionPlanPricingCommand request,
        CancellationToken cancellationToken)
    {
        plan.UpdatePricing(request.MonthlyPriceInCents, request.AnnualPriceInCents);
        return Task.CompletedTask;
    }
}

public sealed class UpdateSubscriptionPlanLimitsCommandHandler(ISubscriptionPlanRepository repository, ISubscriptionPlanListCache planListCache)
    : SubscriptionPlanCommandHandlerBase<UpdateSubscriptionPlanLimitsCommand>(repository, planListCache)
{
    protected override Guid GetPlanId(UpdateSubscriptionPlanLimitsCommand request) => request.Id;

    protected override Task ExecuteAsync(
        SubscriptionPlan plan,
        UpdateSubscriptionPlanLimitsCommand request,
        CancellationToken cancellationToken)
    {
        plan.UpdateLimits(request.MaxUsers, request.MaxStorageMb, request.MaxApiCallsPerMonth);
        return Task.CompletedTask;
    }
}

public sealed class UpdateSubscriptionPlanFeaturesCommandHandler(ISubscriptionPlanRepository repository, ISubscriptionPlanListCache planListCache)
    : SubscriptionPlanCommandHandlerBase<UpdateSubscriptionPlanFeaturesCommand>(repository, planListCache)
{
    protected override Guid GetPlanId(UpdateSubscriptionPlanFeaturesCommand request) => request.Id;

    protected override Task ExecuteAsync(
        SubscriptionPlan plan,
        UpdateSubscriptionPlanFeaturesCommand request,
        CancellationToken cancellationToken)
    {
        plan.UpdateFeatures(
            request.HasPrioritySupport,
            request.HasAdvancedAnalytics,
            request.HasCustomBranding,
            request.Features);
        return Task.CompletedTask;
    }
}

public sealed class ActivateSubscriptionPlanCommandHandler(ISubscriptionPlanRepository repository, ISubscriptionPlanListCache planListCache)
    : SubscriptionPlanCommandHandlerBase<ActivateSubscriptionPlanCommand>(repository, planListCache)
{
    protected override Guid GetPlanId(ActivateSubscriptionPlanCommand request) => request.Id;

    protected override Task ExecuteAsync(
        SubscriptionPlan plan,
        ActivateSubscriptionPlanCommand request,
        CancellationToken cancellationToken)
    {
        plan.Activate();
        return Task.CompletedTask;
    }
}

public sealed class DeactivateSubscriptionPlanCommandHandler(ISubscriptionPlanRepository repository, ISubscriptionPlanListCache planListCache)
    : SubscriptionPlanCommandHandlerBase<DeactivateSubscriptionPlanCommand>(repository, planListCache)
{
    protected override Guid GetPlanId(DeactivateSubscriptionPlanCommand request) => request.Id;

    protected override Task ExecuteAsync(
        SubscriptionPlan plan,
        DeactivateSubscriptionPlanCommand request,
        CancellationToken cancellationToken)
    {
        plan.Deactivate();
        return Task.CompletedTask;
    }
}

public sealed class SetSubscriptionPlanFeaturedCommandHandler(ISubscriptionPlanRepository repository, ISubscriptionPlanListCache planListCache)
    : SubscriptionPlanCommandHandlerBase<SetSubscriptionPlanFeaturedCommand>(repository, planListCache)
{
    protected override Guid GetPlanId(SetSubscriptionPlanFeaturedCommand request) => request.Id;

    protected override Task ExecuteAsync(
        SubscriptionPlan plan,
        SetSubscriptionPlanFeaturedCommand request,
        CancellationToken cancellationToken)
    {
        plan.SetFeatured(request.IsFeatured);
        return Task.CompletedTask;
    }
}

public sealed class SetSubscriptionPlanExternalIdCommandHandler(ISubscriptionPlanRepository repository, ISubscriptionPlanListCache planListCache)
    : SubscriptionPlanCommandHandlerBase<SetSubscriptionPlanExternalIdCommand>(repository, planListCache)
{
    protected override Guid GetPlanId(SetSubscriptionPlanExternalIdCommand request) => request.Id;

    protected override Task ExecuteAsync(
        SubscriptionPlan plan,
        SetSubscriptionPlanExternalIdCommand request,
        CancellationToken cancellationToken)
    {
        plan.SetExternalId(request.ExternalId);
        return Task.CompletedTask;
    }
}

public sealed class DeleteSubscriptionPlanCommandHandler(
    ISubscriptionPlanRepository repository,
    ISubscriptionPlanListCache planListCache) : ICommandHandler<DeleteSubscriptionPlanCommand>
{
    public async Task<Unit> Handle(DeleteSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new EntityNotFoundException("SubscriptionPlan", request.Id);
        }

        // Issue #416: "Cannot delete plans with active subscriptions (must archive)" —
        // check explicitly instead of relying on the database FK (which surfaced as a raw
        // constraint error). The FK remains as a backstop.
        var activeSubscriptions = await repository
            .GetActiveSubscriptionCountAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);
        if (activeSubscriptions > 0)
        {
            throw new InvalidOperationException(
                $"Subscription plan {request.Id} has {activeSubscriptions} active subscription(s) and cannot be deleted. " +
                "Archive the plan instead (POST /v1/subscription-plans/{id}:archive) to retire it while preserving existing subscriptions.");
        }

        await repository.DeleteAsync(request.Id, cancellationToken).ConfigureAwait(false);

        planListCache.Invalidate();

        return Unit.Value;
    }
}

public sealed class ChangeSubscriptionBillingCycleCommandHandler(ISubscriptionLifecycleService lifecycleService)
    : ICommandHandler<ChangeSubscriptionBillingCycleCommand, ChangeBillingCycleResult>
{
    public async Task<ChangeBillingCycleResult> Handle(ChangeSubscriptionBillingCycleCommand request, CancellationToken cancellationToken)
    {
        return await lifecycleService.ChangeBillingCycleAsync(
            request.SubscriptionId,
            request.NewBillingCycle,
            cancellationToken).ConfigureAwait(false);
    }
}
