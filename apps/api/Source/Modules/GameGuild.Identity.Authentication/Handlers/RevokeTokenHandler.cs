using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for revoke token command
/// </summary>
public sealed class RevokeTokenHandler(IAuthService authService, ILogger<RevokeTokenHandler> logger) : ICommandHandler<RevokeTokenCommand>
{
    private readonly IAuthService _authService = authService ?? throw new ArgumentNullException(nameof(authService));

    private readonly ILogger<RevokeTokenHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Unit> Handle(RevokeTokenCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing revoke token request");

        await _authService.RevokeRefreshTokenAsync(command.RefreshToken, command.IpAddress ?? "Unknown", cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Token revoked successfully");

        return Unit.Value;
    }
}
