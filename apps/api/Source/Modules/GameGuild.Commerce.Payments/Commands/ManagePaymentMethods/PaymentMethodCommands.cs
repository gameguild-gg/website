using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Resolves the acting identity for payment-method self-service from the request
///     context (never from the request body). Returns null when the actor cannot own a
///     payment method (anonymous or unresolvable subject).
/// </summary>
public static class PaymentMethodActors
{
    /// <summary>
    ///     Resolves the actor from the accessor. System admins carry no tenant scope.
    /// </summary>
    public static PaymentMethodActor? Resolve(IActorContextAccessor actorContextAccessor)
    {
        var actorContext = actorContextAccessor.ActorContext;
        if (!actorContext.IsAuthenticated)
        {
            return null;
        }

        if (actorContext.IsSystemAdmin)
        {
            var adminUserId = Guid.TryParse(actorContext.SubjectId, out var parsedAdminUserId) ? parsedAdminUserId : Guid.Empty;
            return new PaymentMethodActor(TenantId: null, adminUserId, IsSystemAdmin: true);
        }

        if (!Guid.TryParse(actorContext.SubjectId, out var userId) || userId == Guid.Empty)
        {
            return null;
        }

        return new PaymentMethodActor(actorContext.TenantId, userId, IsSystemAdmin: false);
    }

    /// <summary>Error returned when the acting identity cannot own payment methods.</summary>
    public static Error InvalidActor() => Error.Forbidden(
        "PaymentMethods.InvalidActor",
        "The authenticated actor cannot own payment methods.");
}

/// <summary>
///     Registers a payment method from a tokenized gateway result (issue #414).
/// </summary>
public sealed record RegisterPaymentMethodCommand(
    Guid TenantId,
    string GatewayPaymentMethodId,
    bool SetAsDefault = false,
    string? Nickname = null,
    string? BillingAddress = null,
    Guid? OwnerUserId = null) : ICommand<Result<PaymentMethodDto>>;

/// <summary>
///     Handler for <see cref="RegisterPaymentMethodCommand"/>.
/// </summary>
public sealed class RegisterPaymentMethodCommandHandler(
    IPaymentMethodService paymentMethodService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<RegisterPaymentMethodCommand, Result<PaymentMethodDto>>
{
    /// <inheritdoc />
    public Task<Result<PaymentMethodDto>> Handle(RegisterPaymentMethodCommand request, CancellationToken cancellationToken)
    {
        var actor = PaymentMethodActors.Resolve(actorContextAccessor);
        if (actor is null)
        {
            return Task.FromResult(Result.Failure<PaymentMethodDto>(PaymentMethodActors.InvalidActor()));
        }

        return paymentMethodService.CreateAsync(
            actor,
            new CreatePaymentMethodRequest(
                request.TenantId,
                request.GatewayPaymentMethodId,
                request.SetAsDefault,
                request.Nickname,
                request.BillingAddress,
                request.OwnerUserId),
            cancellationToken);
    }
}

/// <summary>
///     Updates display-only payment-method fields (nickname, billing address).
/// </summary>
public sealed record UpdatePaymentMethodCommand(
    Guid PaymentMethodId,
    string? Nickname = null,
    string? BillingAddress = null) : ICommand<Result<PaymentMethodDto>>;

/// <summary>
///     Handler for <see cref="UpdatePaymentMethodCommand"/>.
/// </summary>
public sealed class UpdatePaymentMethodCommandHandler(
    IPaymentMethodService paymentMethodService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<UpdatePaymentMethodCommand, Result<PaymentMethodDto>>
{
    /// <inheritdoc />
    public Task<Result<PaymentMethodDto>> Handle(UpdatePaymentMethodCommand request, CancellationToken cancellationToken)
    {
        var actor = PaymentMethodActors.Resolve(actorContextAccessor);
        if (actor is null)
        {
            return Task.FromResult(Result.Failure<PaymentMethodDto>(PaymentMethodActors.InvalidActor()));
        }

        return paymentMethodService.UpdateAsync(
            actor,
            request.PaymentMethodId,
            new UpdatePaymentMethodRequest(request.Nickname, request.BillingAddress),
            cancellationToken);
    }
}

/// <summary>
///     Makes a payment method its owner's default (single-default invariant; subscriptions
///     billed with the previous default are re-pointed).
/// </summary>
public sealed record SetDefaultPaymentMethodCommand(Guid PaymentMethodId) : ICommand<Result<PaymentMethodDto>>;

/// <summary>
///     Handler for <see cref="SetDefaultPaymentMethodCommand"/>.
/// </summary>
public sealed class SetDefaultPaymentMethodCommandHandler(
    IPaymentMethodService paymentMethodService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<SetDefaultPaymentMethodCommand, Result<PaymentMethodDto>>
{
    /// <inheritdoc />
    public Task<Result<PaymentMethodDto>> Handle(SetDefaultPaymentMethodCommand request, CancellationToken cancellationToken)
    {
        var actor = PaymentMethodActors.Resolve(actorContextAccessor);
        if (actor is null)
        {
            return Task.FromResult(Result.Failure<PaymentMethodDto>(PaymentMethodActors.InvalidActor()));
        }

        return paymentMethodService.SetDefaultAsync(actor, request.PaymentMethodId, cancellationToken);
    }
}

/// <summary>
///     Soft-deletes a payment method (fail-closed while billable subscriptions use it).
/// </summary>
public sealed record RemovePaymentMethodCommand(Guid PaymentMethodId) : ICommand<Result>;

/// <summary>
///     Handler for <see cref="RemovePaymentMethodCommand"/>.
/// </summary>
public sealed class RemovePaymentMethodCommandHandler(
    IPaymentMethodService paymentMethodService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<RemovePaymentMethodCommand, Result>
{
    /// <inheritdoc />
    public Task<Result> Handle(RemovePaymentMethodCommand request, CancellationToken cancellationToken)
    {
        var actor = PaymentMethodActors.Resolve(actorContextAccessor);
        if (actor is null)
        {
            return Task.FromResult(Result.Failure(PaymentMethodActors.InvalidActor()));
        }

        return paymentMethodService.DeleteAsync(actor, request.PaymentMethodId, cancellationToken);
    }
}

/// <summary>
///     Mirrors a confirmed checkout payment method into the local registry (integration
///     path from subscription checkout completion; keeps Stripe behavior unchanged).
/// </summary>
public sealed record UpsertCheckoutPaymentMethodCommand(
    Guid TenantId,
    Guid SubscriptionId,
    string GatewayPaymentMethodId) : ICommand<Result<PaymentMethodDto>>;

/// <summary>
///     Handler for <see cref="UpsertCheckoutPaymentMethodCommand"/>.
/// </summary>
public sealed class UpsertCheckoutPaymentMethodCommandHandler(
    IPaymentMethodService paymentMethodService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<UpsertCheckoutPaymentMethodCommand, Result<PaymentMethodDto>>
{
    /// <inheritdoc />
    public Task<Result<PaymentMethodDto>> Handle(UpsertCheckoutPaymentMethodCommand request, CancellationToken cancellationToken)
    {
        var actor = PaymentMethodActors.Resolve(actorContextAccessor)
                    // Checkout completion may run for service actors without a user subject;
                    // the service then falls back to the subscription's billing owner.
                    ?? new PaymentMethodActor(TenantId: request.TenantId, UserId: Guid.Empty, IsSystemAdmin: false);

        return paymentMethodService.UpsertFromCheckoutAsync(
            actor,
            request.TenantId,
            request.SubscriptionId,
            request.GatewayPaymentMethodId,
            cancellationToken);
    }
}
