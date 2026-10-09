using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;

namespace GameGuild.Identity.Authentication;

/// <summary>Signs the authenticated user out of every session and revokes their issued tokens.</summary>
public sealed record RevokeAllUserTokensCommand(string? IpAddress) : ICommand<int>;

public sealed class RevokeAllUserTokensHandler(
    IActorContextAccessor actorContextAccessor,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ISessionManagementService sessionService,
    IVersionedUserTokenRevocationService tokenRevocationService,
    IRefreshTokenLifecycleRecorder lifecycleRecorder) : ICommandHandler<RevokeAllUserTokensCommand, int>
{
    private readonly IRefreshTokenLifecycleRecorder _lifecycleRecorder = lifecycleRecorder ?? throw new ArgumentNullException(nameof(lifecycleRecorder));
    public async Task<int> Handle(RevokeAllUserTokensCommand command, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.ActorKind != ActorKind.User ||
            !Guid.TryParse(actor.SubjectId, out var userId) || userId == Guid.Empty)
        {
            throw new AuthenticationRequiredException("An authenticated user is required.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        RefreshTokenLifecycleMetrics.RecordAttempt(RefreshTokenLifecycleOperation.AllRevoked);
        var user = await userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            throw new AuthenticationRequiredException("Invalid access token");
        }

        // The host command pipeline commits these database mutations together. Failures propagate.
        await refreshTokenRepository.RevokeAllForUserAsync(userId, command.IpAddress, cancellationToken)
            .ConfigureAwait(false);
        var terminatedCount = await sessionService.TerminateAllUserSessionsAsync(
            userId, SessionTerminationReason.UserLogout, cancellationToken: cancellationToken).ConfigureAwait(false);
        user.IncrementTokenVersion();
        await userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _lifecycleRecorder.RecordMutationAsync(new RefreshTokenLifecycleEvent(RefreshTokenLifecycleOperation.AllRevoked,
            userId, TenantId: actor.TenantId), cancellationToken).ConfigureAwait(false);

        // Cover legacy access tokens without version/session claims as well. A store failure must not return success.
        await tokenRevocationService.RevokeAllUserTokensAsync(
            userId, user.TokenVersion, "User initiated logout everywhere", cancellationToken).ConfigureAwait(false);
        return terminatedCount;
    }
}
