using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Distributed token revocation store backed by <see cref="IDistributedCache"/>.
/// </summary>
public sealed class DistributedCacheTokenRevocationService : ITokenRevocationService, IVersionedUserTokenRevocationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string TokenKeyPrefix = "auth:revoked-token:";
    private const string UserKeyPrefix = "auth:user-token-revoked-at:";
    private static readonly TimeSpan UserRevocationRetention = TimeSpan.FromDays(30);

    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedCacheTokenRevocationService> _logger;

    public DistributedCacheTokenRevocationService(
        IDistributedCache cache,
        ILogger<DistributedCacheTokenRevocationService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task RevokeTokenAsync(
        string jti,
        DateTime expiresAt,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);

        var now = SystemClock.UtcNow;
        var ttl = expiresAt.ToUniversalTime() - now;
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        var revokedToken = new RevokedToken(jti.Trim(), expiresAt.ToUniversalTime(), now, reason);
        await _cache.SetStringAsync(
            TokenKeyPrefix + revokedToken.Jti,
            JsonSerializer.Serialize(revokedToken, JsonOptions),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Token revoked in distributed cache: JTI={Jti}, ExpiresAt={ExpiresAt}", revokedToken.Jti, revokedToken.ExpiresAt);
    }

    public Task RevokeAllUserTokensAsync(
        Guid userId,
        string? reason = null,
        CancellationToken cancellationToken = default)
        => RevokeAllUserTokensCoreAsync(userId, null, reason, cancellationToken);

    public Task RevokeAllUserTokensAsync(Guid userId, int minimumTokenVersion, string? reason, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumTokenVersion);
        return RevokeAllUserTokensCoreAsync(userId, minimumTokenVersion, reason, cancellationToken);
    }

    private async Task RevokeAllUserTokensCoreAsync(Guid userId, int? minimumTokenVersion, string? reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var revokedAt = SystemClock.UtcNow;
        var payload = new UserTokenRevocation(userId, revokedAt, reason, minimumTokenVersion);

        await _cache.SetStringAsync(
            UserKeyPrefix + userId.ToString("N"),
            JsonSerializer.Serialize(payload, JsonOptions),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = UserRevocationRetention },
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("All user tokens revoked in distributed cache: UserId={UserId}, RevokedAt={RevokedAt}", userId, revokedAt);
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return false;
        }

        var payload = await _cache.GetStringAsync(TokenKeyPrefix + jti.Trim(), cancellationToken).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(payload);
    }

    public Task<bool> IsUserTokenRevokedAsync(
        Guid userId,
        DateTime tokenIssuedAt,
        CancellationToken cancellationToken = default)
        => IsUserTokenRevokedAsync(userId, tokenIssuedAt, null, cancellationToken);

    public async Task<bool> IsUserTokenRevokedAsync(Guid userId, DateTime tokenIssuedAt, int? tokenVersion, CancellationToken cancellationToken)
    {
        var payload = await _cache.GetStringAsync(UserKeyPrefix + userId.ToString("N"), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        var revocation = JsonSerializer.Deserialize<UserTokenRevocation>(payload, JsonOptions);
        return revocation is not null && new UserTokenRevocationBoundary(revocation.RevokedAt, revocation.MinimumTokenVersion)
            .IsRevoked(tokenIssuedAt, tokenVersion);
    }

    public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    private sealed record RevokedToken(string Jti, DateTime ExpiresAt, DateTime RevokedAt, string? Reason);
    private sealed record UserTokenRevocation(Guid UserId, DateTime RevokedAt, string? Reason,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        int? MinimumTokenVersion = null);
}
