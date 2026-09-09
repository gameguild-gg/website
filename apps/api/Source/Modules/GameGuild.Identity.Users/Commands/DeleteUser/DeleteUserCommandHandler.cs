using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Identity.Users;

/// <summary>
///     Command handler for deleting users (soft delete).
///     Decrements the Users quota to maintain accurate resource accounting.
/// </summary>
public sealed class DeleteUserCommandHandler(
    IUserRepository userRepository,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<DeleteUserCommand>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<Unit> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false) 
            ?? throw new UserNotFoundException($"User with ID {request.UserId} not found");

        // Use domain method for soft delete
        user.MarkDeleted();
        user.AddIntegrationEvent(new UserDeletedEvent(user.Id)
        {
            TenantId = Actor.TenantId ?? DurableIntegrationEventTenants.Platform,
            ActorId = Actor.SubjectIdAsGuid ?? DurableIntegrationEventActors.System,
            AggregateType = nameof(User),
            AggregateId = user.Id.ToString(),
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow
        });
        await userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
