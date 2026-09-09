using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Identity.Users;

/// <summary>
///     Command handler for creating a new user
/// </summary>
public sealed class CreateUserCommandHandler(
    IUserRepository userRepository,
    IActorContextAccessor? actorContextAccessor = null) : ICommandHandler<CreateUserCommand, UserDto>
{
    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Create new user entity
        var user = User.Create(request.Email, request.Name, request.PhoneNumber);
        var actor = actorContextAccessor?.ActorContext ?? ActorContext.Anonymous;
        user.AddIntegrationEvent(new UserCreatedEvent(user.Id)
        {
            TenantId = actor.TenantId ?? DurableIntegrationEventTenants.Platform,
            ActorId = actor.SubjectIdAsGuid ?? DurableIntegrationEventActors.System,
            AggregateType = nameof(User),
            AggregateId = user.Id.ToString(),
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow
        });

        // Add to repository
        await userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Map to DTO
        return new UserDto(user.Id, user.Email, user.Name, user.CreatedAt, user.UpdatedAt, user.IsActive, user.PhoneNumber, user.LastSeenAt);
    }
}
