using GameGuild.CQRS;

namespace GameGuild.Commerce.Subscriptions;

public sealed class UpdateSubscriptionPlanCommandHandler(ISubscriptionPlanRepository repository)
    : SubscriptionPlanCommandHandlerBase<UpdateSubscriptionPlanCommand>(repository)
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

public sealed class UpdateSubscriptionPlanPricingCommandHandler(ISubscriptionPlanRepository repository)
    : SubscriptionPlanCommandHandlerBase<UpdateSubscriptionPlanPricingCommand>(repository)
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

public sealed class UpdateSubscriptionPlanLimitsCommandHandler(ISubscriptionPlanRepository repository)
    : SubscriptionPlanCommandHandlerBase<UpdateSubscriptionPlanLimitsCommand>(repository)
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

public sealed class UpdateSubscriptionPlanFeaturesCommandHandler(ISubscriptionPlanRepository repository)
    : SubscriptionPlanCommandHandlerBase<UpdateSubscriptionPlanFeaturesCommand>(repository)
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

public sealed class ActivateSubscriptionPlanCommandHandler(ISubscriptionPlanRepository repository)
    : SubscriptionPlanCommandHandlerBase<ActivateSubscriptionPlanCommand>(repository)
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

public sealed class DeactivateSubscriptionPlanCommandHandler(ISubscriptionPlanRepository repository)
    : SubscriptionPlanCommandHandlerBase<DeactivateSubscriptionPlanCommand>(repository)
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

public sealed class SetSubscriptionPlanFeaturedCommandHandler(ISubscriptionPlanRepository repository)
    : SubscriptionPlanCommandHandlerBase<SetSubscriptionPlanFeaturedCommand>(repository)
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

public sealed class SetSubscriptionPlanExternalIdCommandHandler(ISubscriptionPlanRepository repository)
    : SubscriptionPlanCommandHandlerBase<SetSubscriptionPlanExternalIdCommand>(repository)
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

public sealed class DeleteSubscriptionPlanCommandHandler(ISubscriptionPlanRepository repository)
    : ICommandHandler<DeleteSubscriptionPlanCommand>
{
    public async Task<Unit> Handle(DeleteSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false) is null)
            throw new EntityNotFoundException("SubscriptionPlan", request.Id);

        await repository.DeleteAsync(request.Id, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

public sealed class ChangeSubscriptionBillingCycleCommandHandler(ISubscriptionLifecycleService lifecycleService)
    : ICommandHandler<ChangeSubscriptionBillingCycleCommand>
{
    public async Task<Unit> Handle(ChangeSubscriptionBillingCycleCommand request, CancellationToken cancellationToken)
    {
        await lifecycleService.ChangeBillingCycleAsync(
            request.SubscriptionId,
            request.NewBillingCycle,
            cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
