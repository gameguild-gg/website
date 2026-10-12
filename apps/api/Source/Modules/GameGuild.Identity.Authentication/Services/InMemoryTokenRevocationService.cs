using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     In-memory implementation of token revocation service.
///     Suitable for single-instance deployments; replace with Redis for distributed scenarios.
/// </summary>
/// <remarks>
///     <para>
///         <b>Thread Safety:</b> Uses ConcurrentDictionary for thread-safe operations.
///     </para>
///     <para>
///         <b>Memory Management:</b> Automatically cleans up expired entries during operations.
///         For production, consider running <see cref="CleanupExpiredAsync"/> periodically via hosted service.
///     </para>
///     <para>
///         <b>Redis Migration:</b> To migrate to Redis, implement <see cref="ITokenRevocationService"/>
///         using Redis SETEX with TTL matching token expiry for automatic cleanup.
///     </para>
/// </remarks>
public sealed class InMemoryTokenRevocationService : ITokenRevocationService, IVersionedUserTokenRevocationService
{
    private readonly ILogger<InMemoryTokenRevocationService> _logger;
    
    // JTI -> ExpiresAt (for individual token revocation)
    private readonly ConcurrentDictionary<string, RevokedToken> _revokedTokens = new();
    
    // UserId -> RevokedAt (for "revoke all user tokens" functionality)
    private readonly ConcurrentDictionary<Guid, UserTokenRevocationBoundary> _userRevocationTimes = new();

    public InMemoryTokenRevocationService(ILogger<InMemoryTokenRevocationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task RevokeTokenAsync(string jti, DateTime expiresAt, string? reason = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);

        var revokedToken = new RevokedToken
        {
            Jti = jti,
            ExpiresAt = expiresAt,
            RevokedAt = SystemClock.UtcNow,
            Reason = reason
        };

        _revokedTokens.TryAdd(jti, revokedToken);
        
        _logger.LogInformation(
            "Token revoked: JTI={Jti}, ExpiresAt={ExpiresAt}, Reason={Reason}",
            jti, expiresAt, reason ?? "Not specified");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RevokeAllUserTokensAsync(Guid userId, string? reason = null, CancellationToken cancellationToken = default)
        => RevokeAllUserTokensCoreAsync(userId, null, reason, cancellationToken);

    public Task RevokeAllUserTokensAsync(Guid userId, int minimumTokenVersion, string? reason, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumTokenVersion);
        return RevokeAllUserTokensCoreAsync(userId, minimumTokenVersion, reason, cancellationToken);
    }

    private Task RevokeAllUserTokensCoreAsync(Guid userId, int? minimumTokenVersion, string? reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var revocationTime = SystemClock.UtcNow;
        
        // Update or add the user's revocation time
        var boundary = new UserTokenRevocationBoundary(revocationTime, minimumTokenVersion);
        _userRevocationTimes.AddOrUpdate(userId, boundary, (_, _) => boundary);
        
        _logger.LogInformation(
            "All tokens revoked for user: UserId={UserId}, RevokedAt={RevokedAt}, Reason={Reason}",
            userId, revocationTime, reason ?? "Not specified");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return Task.FromResult(false);
        }

        var isRevoked = _revokedTokens.ContainsKey(jti);
        
        if (isRevoked)
        {
            _logger.LogDebug("Token check: JTI={Jti} is REVOKED", jti);
        }

        return Task.FromResult(isRevoked);
    }

    /// <inheritdoc />
    public Task<bool> IsUserTokenRevokedAsync(Guid userId, DateTime tokenIssuedAt, CancellationToken cancellationToken = default)
        => IsUserTokenRevokedAsync(userId, tokenIssuedAt, null, cancellationToken);

    public Task<bool> IsUserTokenRevokedAsync(Guid userId, DateTime tokenIssuedAt, int? tokenVersion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_userRevocationTimes.TryGetValue(userId, out var revocationTime))
        {
            // Token is revoked if it was issued before the revocation time
            var isRevoked = revocationTime.IsRevoked(tokenIssuedAt, tokenVersion);
            
            if (isRevoked)
            {
                _logger.LogDebug(
                    "User token check: UserId={UserId}, IssuedAt={IssuedAt}, RevokedAt={RevokedAt} - REVOKED",
                    userId, tokenIssuedAt, revocationTime.RevokedAt);
            }

            return Task.FromResult(isRevoked);
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;
        var cleanedCount = 0;

        // Clean up expired individual token revocations
        foreach (var kvp in _revokedTokens)
        {
            if (kvp.Value.ExpiresAt < now)
            {
                if (_revokedTokens.TryRemove(kvp.Key, out _))
                {
                    cleanedCount++;
                }
            }
        }

        // Clean up old user revocation times (older than 24 hours)
        var userCleanupThreshold = now.AddHours(-24);
        foreach (var kvp in _userRevocationTimes)
        {
            if (kvp.Value.RevokedAt < userCleanupThreshold)
            {
                _userRevocationTimes.TryRemove(kvp.Key, out _);
            }
        }

        if (cleanedCount > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired token revocation entries", cleanedCount);
        }

        return Task.FromResult(cleanedCount);
    }

    /// <summary>
    ///     Internal record for storing revoked token information.
    /// </summary>
    private sealed record RevokedToken
    {
        public required string Jti { get; init; }
        public required DateTime ExpiresAt { get; init; }
        public required DateTime RevokedAt { get; init; }
        public string? Reason { get; init; }
    }
}
