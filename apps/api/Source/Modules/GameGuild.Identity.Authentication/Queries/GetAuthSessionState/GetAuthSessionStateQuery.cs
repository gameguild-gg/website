using System.Security.Claims;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization.Utilities;
using Microsoft.AspNetCore.Http;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Self-service authentication state for the caller's current token and session (issue #221):
///     session validity and expiry, token version, active-session count and MFA enrollment state.
///     The caller is always the authenticated principal; no identity can be supplied in the request.
/// </summary>
public sealed record GetAuthSessionStateQuery : IQuery<AuthSessionStateDto>;

/// <summary>
///     The v1 session-state projection (issue #221). <see cref="SessionValid" /> is false when the
///     token is not session-bound or the bound session row is expired, terminated or missing —
///     the surface reports that state instead of trusting the token alone. <see cref="TokenRevoked" />
///     is null when the token carries no issued-at claim, so no revocation verdict is claimed.
/// </summary>
public sealed record AuthSessionStateDto(
    Guid UserId,
    Guid? SessionId,
    bool SessionValid,
    DateTime? SessionExpiresAtUtc,
    DateTime? SessionLastUsedAtUtc,
    string? SessionTerminationReason,
    int? TokenVersion,
    bool? TokenRevoked,
    int ActiveSessionCount,
    bool MfaEnrolled,
    bool MfaVerifiedInCurrentToken);

/// <summary>
///     Reads the caller's session state from the existing session, revocation and MFA
///     infrastructure (issue #221). Read-only: it records nothing and mutates nothing.
/// </summary>
public sealed class GetAuthSessionStateQueryHandler(
    IHttpContextAccessor httpContextAccessor,
    ISessionManagementService sessions,
    IUserMfaConfigurationRepository mfaConfigurations,
    IVersionedUserTokenRevocationService tokenRevocations) : IQueryHandler<GetAuthSessionStateQuery, AuthSessionStateDto>
{
    public async Task<AuthSessionStateDto> Handle(GetAuthSessionStateQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = httpContextAccessor.HttpContext?.User;
        // Fail closed: without an authenticated principal there is no state to report.
        if (user is null || !ClaimsExtractor.IsAuthenticated(user))
        {
            throw new UnauthorizedAccessException("Session state requires an authenticated caller.");
        }
        var userId = ClaimsExtractor.GetUserIdAsGuid(user)
            ?? throw new UnauthorizedAccessException("Session state requires a resolvable user identity.");
        var tokenVersion = int.TryParse(ClaimsExtractor.GetTokenVersion(user), out var version) ? version : (int?) null;

        var sessionIdClaim = ClaimsExtractor.GetClaim(user, JwtClaimTypes.SessionId);
        var sessionId = Guid.TryParse(sessionIdClaim, out var parsedSession) && parsedSession != Guid.Empty
            ? parsedSession
            : (Guid?) null;

        var activeSessions = await sessions.GetUserSessionsAsync(userId, activeOnly: true, cancellationToken)
            .ConfigureAwait(false);
        UserSession? session = null;
        if (sessionId.HasValue)
        {
            session = await sessions.GetSessionAsync(sessionId.Value, cancellationToken).ConfigureAwait(false);
        }

        var mfa = await mfaConfigurations.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        // The versioned revocation service only issues a verdict for tokens that state when
        // they were issued; without an issued-at claim the surface stays silent (null) instead
        // of claiming the token is live.
        bool? tokenRevoked = null;
        var issuedAt = ClaimsExtractor.GetIssuedAtDateTime(user);
        if (issuedAt.HasValue)
        {
            tokenRevoked = await tokenRevocations.IsUserTokenRevokedAsync(userId, issuedAt.Value, tokenVersion, cancellationToken)
                .ConfigureAwait(false);
        }

        return new AuthSessionStateDto(
            userId,
            session?.Id,
            session is not null && session.IsValid && activeSessions.Exists(active => active.Id == session.Id),
            session?.ExpiresAt,
            session?.LastUsedAt,
            session?.TerminationReason,
            tokenVersion,
            tokenRevoked,
            activeSessions.Count,
            mfa?.IsEnabled ?? false,
            ClaimsExtractor.IsMfaVerified(user));
    }
}
