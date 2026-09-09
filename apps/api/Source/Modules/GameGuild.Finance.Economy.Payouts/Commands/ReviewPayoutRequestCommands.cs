using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Finance.Economy.Payouts.Queries;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Finance.Economy.Payouts.Commands;

public sealed record ReviewPayoutRequestRequest(string Reason);

[AuthorizeRequest(EconomyPermission.Keys.ReviewPayouts)]
public sealed record ReviewPayoutRequestCommand(
    Guid RequestId,
    PayoutRequestState Outcome,
    ReviewPayoutRequestRequest Request)
    : ICommand<EconomyPayoutRequestReviewDto>;

public sealed class ReviewPayoutRequestCommandValidator : AbstractValidator<ReviewPayoutRequestCommand>
{
    public ReviewPayoutRequestCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.Outcome)
            .Must(outcome => outcome is PayoutRequestState.Approved or PayoutRequestState.Rejected)
            .WithMessage("Payout reviews must approve or reject the request.");
        RuleFor(command => command.Request).NotNull();
        When(command => command.Request is not null, () =>
        {
            RuleFor(command => command.Request.Reason).NotEmpty().MinimumLength(3).MaximumLength(1000);
        });
    }
}

public sealed class ReviewPayoutRequestCommandHandler(
    IActorContextAccessor actorContextAccessor,
    IPayoutRequestStore requests)
    : ICommandHandler<ReviewPayoutRequestCommand, EconomyPayoutRequestReviewDto>
{
    public Task<EconomyPayoutRequestReviewDto> Handle(
        ReviewPayoutRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Request);
        cancellationToken.ThrowIfCancellationRequested();

        var reviewer = RequireWalletAdministrator(actorContextAccessor);
        var current = requests.GetForReview(command.RequestId, reviewer.TenantId);
        var reviewed = current.Review(reviewer.ActorId, command.Outcome, DateTimeOffset.UtcNow);
        var persisted = requests.Review(
            reviewed,
            current.Version,
            reviewer.TenantId,
            reviewer.ActorId,
            command.Outcome,
            command.Request.Reason.Trim());
        return Task.FromResult(EconomyPayoutRequestReviewDto.From(persisted));
    }

    private static PayoutReviewActor RequireWalletAdministrator(IActorContextAccessor actorContextAccessor)
    {
        var actor = actorContextAccessor.ActorContext;
        return actor.IsAuthenticated && actor.SubjectIdAsGuid is { } actorId && actor.TenantId is { } tenantId &&
               actor.HasPermission(EconomyPermission.Keys.ReviewPayouts)
            ? new PayoutReviewActor(actorId, tenantId)
            : throw new UnauthorizedAccessException("The Economy payout-review permission is required.");
    }

    private sealed record PayoutReviewActor(Guid ActorId, Guid TenantId);
}
