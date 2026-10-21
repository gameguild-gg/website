using GameGuild.CQRS;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handles <see cref="IssueGuestSessionCommand"/> (issue #213): issues a short-lived
///     anonymous guest session in its own identity namespace. Issuance itself is audited and
///     rate limited; failures (guest access disabled) surface as
///     <see cref="GuestAccessDisabledException"/> so the endpoint fails closed with 403.
/// </summary>
public sealed class IssueGuestSessionHandler(
    IGuestSessionService guestSessionService,
    IHttpContextAccessor? httpContextAccessor = null,
    ILogger<IssueGuestSessionHandler>? logger = null) : ICommandHandler<IssueGuestSessionCommand, GuestSessionResponse>
{
    public async Task<GuestSessionResponse> Handle(IssueGuestSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var httpContext = httpContextAccessor?.HttpContext;
        var request = new GuestSessionRequest
        {
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
            DeviceFingerprint = command.DeviceFingerprint
        };

        var result = await guestSessionService.IssueAsync(request, cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            logger?.LogInformation("Guest session issuance refused: {Reason}", result.FailureReason);
            throw new GuestAccessDisabledException("Anonymous guest access is not available.");
        }

        var now = SystemClock.UtcNow;
        return new GuestSessionResponse
        {
            GuestSessionId = result.GuestSessionId,
            TokenType = "Bearer",
            AccessToken = result.AccessToken,
            ExpiresAt = result.ExpiresAt,
            ExpiresIn = checked((int)Math.Ceiling((result.ExpiresAt - now).TotalSeconds)),
            Permissions = result.Permissions
        };
    }
}

/// <summary>
///     Thrown when guest access is disabled by configuration; the presenting endpoint maps
///     it to 403 so the surface fails closed.
/// </summary>
public sealed class GuestAccessDisabledException(string message) : Exception(message);
