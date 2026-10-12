using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Commerce.Payments;

// ── Self-service gift operations (actor-scoped: giver/redeemer and tenant derived from the actor) ──

/// <summary>Purchases a gift subscription; the full redemption code is returned exactly once.</summary>
[AuthorizeRequest("")]
public sealed record PurchaseGiftCommand(
    string RecipientEmail,
    decimal Amount,
    string Currency,
    int DurationMonths,
    Guid? PlanId = null,
    string? RecipientName = null,
    string? PersonalMessage = null,
    DateTime? DeliveryDate = null) : ICommand<Result<GiftPurchaseResult>>;

/// <summary>Non-secret preview of a gift by redemption code.</summary>
[AuthorizeRequest("")]
public sealed record GetGiftByCodeQuery(string RedemptionCode) : IQuery<Result<GiftPublicView>>;

/// <summary>Redeems a gift for the calling user: grants account credit of the gift value.</summary>
[AuthorizeRequest("")]
public sealed record RedeemGiftCommand(string RedemptionCode) : ICommand<Result<GiftPublicView>>;

/// <summary>Gifts the caller has sent.</summary>
[AuthorizeRequest("")]
public sealed record GetMySentGiftsQuery(int Skip = 0, int Take = 20) : IQuery<PagedResult<GiftPublicView>>;

/// <summary>Gifts the caller has redeemed.</summary>
[AuthorizeRequest("")]
public sealed record GetMyReceivedGiftsQuery(int Skip = 0, int Take = 20) : IQuery<PagedResult<GiftPublicView>>;

// ── Handlers ──

public sealed class PurchaseGiftCommandHandler(
    IGiftService giftService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<PurchaseGiftCommand, Result<GiftPurchaseResult>>
{
    public Task<Result<GiftPurchaseResult>> Handle(PurchaseGiftCommand request, CancellationToken cancellationToken)
    {
        var (userId, tenantId) = AccountCreditActor.RequireUserAndTenant(actorContextAccessor);
        return giftService.PurchaseAsync(
            new PurchaseGiftRequest(
                tenantId,
                userId,
                request.RecipientEmail,
                request.Amount,
                request.Currency,
                request.DurationMonths,
                request.PlanId,
                request.RecipientName,
                request.PersonalMessage,
                request.DeliveryDate),
            cancellationToken);
    }
}

public sealed class GetGiftByCodeQueryHandler(
    IGiftService giftService,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetGiftByCodeQuery, Result<GiftPublicView>>
{
    public Task<Result<GiftPublicView>> Handle(GetGiftByCodeQuery request, CancellationToken cancellationToken)
    {
        var (_, tenantId) = AccountCreditActor.RequireUserAndTenant(actorContextAccessor);
        return giftService.GetByCodeAsync(tenantId, request.RedemptionCode, cancellationToken);
    }
}

public sealed class RedeemGiftCommandHandler(
    IGiftService giftService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<RedeemGiftCommand, Result<GiftPublicView>>
{
    public Task<Result<GiftPublicView>> Handle(RedeemGiftCommand request, CancellationToken cancellationToken)
    {
        var (userId, tenantId) = AccountCreditActor.RequireUserAndTenant(actorContextAccessor);
        return giftService.RedeemAsync(tenantId, request.RedemptionCode, userId, cancellationToken);
    }
}

public sealed class GetMySentGiftsQueryHandler(
    IGiftService giftService,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetMySentGiftsQuery, PagedResult<GiftPublicView>>
{
    public Task<PagedResult<GiftPublicView>> Handle(GetMySentGiftsQuery request, CancellationToken cancellationToken)
    {
        var (userId, tenantId) = AccountCreditActor.RequireUserAndTenant(actorContextAccessor);
        var skip = Math.Max(0, request.Skip);
        var take = Math.Clamp(request.Take, 1, 100);
        return giftService.GetSentAsync(tenantId, userId, skip, take, cancellationToken);
    }
}

public sealed class GetMyReceivedGiftsQueryHandler(
    IGiftService giftService,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetMyReceivedGiftsQuery, PagedResult<GiftPublicView>>
{
    public Task<PagedResult<GiftPublicView>> Handle(GetMyReceivedGiftsQuery request, CancellationToken cancellationToken)
    {
        var (userId, tenantId) = AccountCreditActor.RequireUserAndTenant(actorContextAccessor);
        var skip = Math.Max(0, request.Skip);
        var take = Math.Clamp(request.Take, 1, 100);
        return giftService.GetReceivedAsync(tenantId, userId, skip, take, cancellationToken);
    }
}

// ── Validators ──

public sealed class PurchaseGiftCommandValidator : AbstractValidator<PurchaseGiftCommand>
{
    public PurchaseGiftCommandValidator()
    {
        RuleFor(command => command.RecipientEmail)
            .NotEmpty()
            .MaximumLength(255)
            .EmailAddress();
        RuleFor(command => command.Amount).GreaterThan(0);
        RuleFor(command => command.Currency)
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Za-z]{3}$");
        RuleFor(command => command.DurationMonths).InclusiveBetween(1, 36);
        RuleFor(command => command.RecipientName).MaximumLength(255);
        RuleFor(command => command.PersonalMessage).MaximumLength(500);
    }
}

public sealed class GetGiftByCodeQueryValidator : AbstractValidator<GetGiftByCodeQuery>
{
    public GetGiftByCodeQueryValidator()
    {
        RuleFor(query => query.RedemptionCode).NotEmpty().MaximumLength(64);
    }
}

public sealed class RedeemGiftCommandValidator : AbstractValidator<RedeemGiftCommand>
{
    public RedeemGiftCommandValidator()
    {
        RuleFor(command => command.RedemptionCode).NotEmpty().MaximumLength(64);
    }
}
