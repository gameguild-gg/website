using System.IdentityModel.Tokens.Jwt;
using GameGuild.CQRS;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Creates a persisted root refresh token and session within the caller's command transaction.
///     Provider verification must precede this operation; exceptions must roll back that transaction.
/// </summary>
public sealed class AuthenticatedSessionIssuer(
    ISender sender,
    IJwtTokenService jwtTokenService,
    IRefreshTokenHasher refreshTokenHasher,
    IRefreshTokenRepository refreshTokenRepository,
    ISessionManagementService sessionManagementService) : IAuthenticatedSessionIssuer
{
    public async Task<SignInResponse> IssueAsync(
        User user,
        Guid? requestedTenantId,
        DeviceInfo deviceInfo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(deviceInfo);
        if (user.Id == Guid.Empty || user.IsDeleted || !user.ValidateForAuthentication(user.TokenVersion).IsSuccess)
        {
            throw new AuthenticationRequiredException("The account is unavailable for authentication.");
        }

        var memberships = await sender.Send(new GetUserMembershipsQuery(user.Id), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var eligibleMemberships = memberships.Memberships.Where(membership =>
            membership.IsActive && membership.TenantIsActive && membership.TenantId != Guid.Empty &&
            (membership.InviteStatus is null || string.Equals(membership.InviteStatus, "Accepted", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var context = TenantAccessContextResolver.Resolve(
            new GetUserMembershipsResponse { Memberships = eligibleMemberships, TotalCount = eligibleMemberships.Count },
            requestedTenantId);
        if (context.TenantId is null)
        {
            throw new AuthenticationRequiredException("No authorized tenant is available for authentication.");
        }

        var authenticatedAt = new DateTimeOffset(SystemClock.UtcNow);
        var rawRefreshToken = await jwtTokenService.GenerateRefreshTokenAsync(
            user.Id, deviceInfo, authenticatedAt, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            throw IssuanceFailure();
        }
        var hash = refreshTokenHasher.HashToken(rawRefreshToken);
        var storedToken = await refreshTokenRepository.GetByTokenAsync(hash, cancellationToken).ConfigureAwait(false);
        RequireRootToken(storedToken, user.Id, rawRefreshToken, null);

        var sessionId = Guid.NewGuid();
        // Binding may cap the tracked root token; preserve the original requested bound.
        var requestedDeadline = storedToken!.ExpiresAt;
        var session = await sessionManagementService.CreateSessionAsync(
            sessionId, user.Id, deviceInfo.IpAddress ?? "unknown", deviceInfo.UserAgent ?? "unknown",
            hash, requestedDeadline, deviceInfo.Fingerprint, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (session is null || session.Id != sessionId || session.UserId != user.Id || !session.IsValid ||
            session.TerminatedAt is not null || session.ExpiresAt > requestedDeadline ||
            !refreshTokenHasher.VerifyToken(rawRefreshToken, session.RefreshToken))
        {
            throw IssuanceFailure();
        }
        storedToken = await refreshTokenRepository.GetByTokenAsync(hash, cancellationToken).ConfigureAwait(false);
        RequireRootToken(storedToken, user.Id, rawRefreshToken, sessionId);
        if (storedToken!.ExpiresAt > session.ExpiresAt)
        {
            storedToken.ExpiresAt = session.ExpiresAt;
            await refreshTokenRepository.UpdateAsync(storedToken, cancellationToken).ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();

        var accessToken = await jwtTokenService.GenerateAccessTokenAsync(
            user.Id, user.Email, context.Roles.ToArray(), context.TenantId, user.TokenVersion,
            new DateTimeOffset(storedToken.CreatedAt), sessionId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(accessToken))
        {
            throw IssuanceFailure();
        }
        var accessExpiresAt = handler.ReadJwtToken(accessToken).ValidTo;
        var now = SystemClock.UtcNow;
        if (accessExpiresAt <= now)
        {
            throw IssuanceFailure();
        }
        accessExpiresAt = accessExpiresAt < session.ExpiresAt ? accessExpiresAt : session.ExpiresAt;

        return new SignInResponse
        {
            Success = true,
            Message = "Sign-in successful",
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            UserId = user.Id,
            Email = user.Email,
            SessionId = sessionId,
            TenantId = context.TenantId,
            AvailableTenants = context.AvailableTenants,
            ExpiresAt = storedToken.ExpiresAt,
            RefreshTokenExpiresAt = storedToken.ExpiresAt,
            AccessTokenExpiresAt = accessExpiresAt,
            ExpiresIn = checked((int)Math.Ceiling((accessExpiresAt - now).TotalSeconds))
        };
    }

    private void RequireRootToken(RefreshToken? token, Guid userId, string rawToken, Guid? sessionId)
    {
        if (token is null || token.Id == Guid.Empty || token.UserId != userId || !token.IsActive ||
            token.ParentTokenId is not null || token.ReplacedByToken is not null || token.SessionId != sessionId ||
            token.CreatedAt == default || token.CreatedAt > SystemClock.UtcNow ||
            !refreshTokenHasher.VerifyToken(rawToken, token.Token))
        {
            throw IssuanceFailure();
        }
    }

    private static InvalidOperationException IssuanceFailure() =>
        new("Authenticated credentials could not be bound to a persisted session.");
}
