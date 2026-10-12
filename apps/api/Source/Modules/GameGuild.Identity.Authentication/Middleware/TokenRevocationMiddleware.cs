using GameGuild.Identity.Authorization.Utilities;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Middleware that validates JWT tokens against the revocation list and token version.
///     Rejects requests with revoked tokens or mismatched token versions, enabling immediate logout functionality.
/// </summary>
/// <remarks>
///     <para>
///         <b>Execution Order:</b> This middleware should run AFTER authentication middleware
///         but BEFORE authorization middleware. It requires a valid ClaimsPrincipal.
///     </para>
///     <para>
///         <b>Performance:</b> Uses async operations to check revocation status and token version.
///         For high-throughput scenarios, consider caching token version with short TTL.
///     </para>
///     <para>
///         <b>Token Version:</b> Compares the token's version claim against the user's current
///         token version in the database. If the token version is lower, the token is rejected.
///         This enables immediate invalidation of all tokens when a user changes their password
///         or initiates "logout all sessions".
///     </para>
/// </remarks>
public sealed class TokenRevocationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenRevocationMiddleware> _logger;

    public TokenRevocationMiddleware(RequestDelegate next, ILogger<TokenRevocationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITokenRevocationService revocationService, IUserRepository userRepository,
        IUserSessionRepository sessionRepository)
    {
        // Skip if not authenticated
        if (!ClaimsExtractor.IsAuthenticated(context.User))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Extract JTI from claims
        var jti = ClaimsExtractor.GetJti(context.User);
        
        if (!string.IsNullOrEmpty(jti))
        {
            // Check if individual token is revoked
            if (await revocationService.IsRevokedAsync(jti, context.RequestAborted))
            {
                _logger.LogWarning("Rejected request with revoked token: JTI={Jti}", jti);
                await RejectAsync(context).ConfigureAwait(false);
                return;
            }
        }

        // Extract user ID and token issued time for user-level revocation check
        var userId = ClaimsExtractor.GetUserIdAsGuid(context.User);
        var tokenIssuedAt = ClaimsExtractor.GetIssuedAtDateTime(context.User);
        var tokenVersionClaim = ClaimsExtractor.GetTokenVersion(context.User);
        int? parsedTokenVersion = int.TryParse(tokenVersionClaim, out var parsedVersion) ? parsedVersion : null;

        if (userId.HasValue && tokenIssuedAt.HasValue)
        {
            // Check if all user tokens were revoked after this token was issued
            var userRevoked = revocationService is IVersionedUserTokenRevocationService versioned
                ? await versioned.IsUserTokenRevokedAsync(userId.Value, tokenIssuedAt.Value, parsedTokenVersion, context.RequestAborted).ConfigureAwait(false)
                : await revocationService.IsUserTokenRevokedAsync(userId.Value, tokenIssuedAt.Value, context.RequestAborted).ConfigureAwait(false);
            if (userRevoked)
            {
                _logger.LogWarning(
                    "Rejected request with user-revoked token: UserId={UserId}, IssuedAt={IssuedAt}",
                    userId, tokenIssuedAt);
                await RejectAsync(context).ConfigureAwait(false);
                return;
            }
        }

        // Token version validation: compare JWT's token_version against user's current TokenVersion
        if (userId.HasValue)
        {
            if (parsedTokenVersion.HasValue)
            {
                var tokenVersion = parsedTokenVersion.Value;
                var currentVersion = await userRepository.GetTokenVersionAsync(userId.Value, context.RequestAborted).ConfigureAwait(false);
                
                // Versioned user tokens require an available user and a current version.
                if (!currentVersion.HasValue || tokenVersion < currentVersion.Value)
                {
                    _logger.LogWarning(
                        "Rejected request with unavailable user or outdated token version: UserId={UserId}, TokenVersion={TokenVersion}, CurrentVersion={CurrentVersion}",
                        userId, tokenVersion, currentVersion);
                    await RejectAsync(context).ConfigureAwait(false);
                    return;
                }
            }
        }

        var sessionClaims = context.User.FindAll(JwtClaimTypes.SessionId).Take(2).ToArray();
        if (sessionClaims.Length != 0)
        {
            if (sessionClaims.Length != 1 || !Guid.TryParse(sessionClaims[0].Value, out var sessionId) ||
                sessionId == Guid.Empty || !userId.HasValue || userId.Value == Guid.Empty)
            {
                await RejectAsync(context).ConfigureAwait(false);
                return;
            }

            var session = await sessionRepository.GetByIdAsync(sessionId, context.RequestAborted).ConfigureAwait(false);
            if (session == null || session.UserId != userId.Value || !session.IsActive ||
                session.TerminatedAt.HasValue || session.ExpiresAt <= SystemClock.UtcNow)
            {
                await RejectAsync(context).ConfigureAwait(false);
                return;
            }
        }

        await _next(context).ConfigureAwait(false);
    }

    private async Task RejectAsync(HttpContext context)
    {
        // An optional stale bearer must not block public sign-in/recovery or become its actor.
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "Invalid access token"
        };
        var writer = context.RequestServices?.GetService<IProblemDetailsService>();
        if (writer is null || !await writer.TryWriteAsync(new ProblemDetailsContext
            { HttpContext = context, ProblemDetails = problem }).ConfigureAwait(false))
        {
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json",
                cancellationToken: context.RequestAborted).ConfigureAwait(false);
        }
    }
}
