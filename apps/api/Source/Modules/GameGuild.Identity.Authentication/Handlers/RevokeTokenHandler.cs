using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for revoke token command
/// </summary>
public sealed class RevokeTokenHandler(
    IAuthService authService,
    ILogger<RevokeTokenHandler> logger,
    IActorContextAccessor actorContextAccessor,
    IRefreshTokenRepository refreshTokenRepository,
    IRefreshTokenHasher refreshTokenHasher) : ICommandHandler<RevokeTokenCommand>
{
    private readonly IAuthService _authService = authService ?? throw new ArgumentNullException(nameof(authService));

    private readonly ILogger<RevokeTokenHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IActorContextAccessor _actorContextAccessor = actorContextAccessor ?? throw new ArgumentNullException(nameof(actorContextAccessor));

    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));

    private readonly IRefreshTokenHasher _refreshTokenHasher = refreshTokenHasher ?? throw new ArgumentNullException(nameof(refreshTokenHasher));

    public async Task<Unit> Handle(RevokeTokenCommand command, CancellationToken cancellationToken)
    {
        var actor = _actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.ActorKind != ActorKind.User ||
            actor.SubjectIdAsGuid is not { } userId || userId == Guid.Empty)
        {
            throw new AuthenticationRequiredException();
        }

        var tokenHash = _refreshTokenHasher.HashToken(command.RefreshToken);
        var token = await _refreshTokenRepository.GetByTokenAsync(tokenHash, cancellationToken).ConfigureAwait(false);
        if (token == null) { throw new ArgumentException("Invalid token"); }

        // This self-service operation uses the authenticated actor, never command.UserId.
        if (token.UserId != userId) { throw new AccessDeniedException(); }

        _logger.LogInformation("Processing revoke token request");

        await _authService.RevokeRefreshTokenAsync(command.RefreshToken, command.IpAddress ?? "Unknown", cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Token revoked successfully");

        return Unit.Value;
    }
}
