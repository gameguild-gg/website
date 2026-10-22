using System.Text.Json;
using GameGuild.Configuration.ApplicationLayer;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Guest session store backed by <see cref="IDistributedCache"/> (issue #213) — the same
/// pattern as the WebAuthn challenge store: Redis in production, the host's local
/// distributed-memory cache otherwise, so guest sessions work across instances without a
/// durable table. Entries carry a TTL of the session lifetime plus eviction slack; logical
/// expiry is re-checked on read against the domain clock so a lagging cache cannot revive a
/// stale session.
/// </summary>
public sealed class DistributedCacheGuestSessionStore(
    IDistributedCache cache,
    IOptions<GuestSessionOptions> options,
    ILogger<DistributedCacheGuestSessionStore> logger) : IGuestSessionStore
{
    private const string KeyPrefix = "auth:guest:session:";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly GuestSessionOptions _options = options.Value;

    /// <inheritdoc />
    public async Task SaveAsync(GuestSessionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.GuestSessionId == Guid.Empty || snapshot.ExpiresAt <= snapshot.IssuedAt)
        {
            throw new ArgumentException("A guest session requires an id and a future-bounded lifetime.");
        }

        var ttl = snapshot.ExpiresAt - SystemClock.UtcNow + TimeSpan.FromMinutes(Math.Max(0, _options.StoreEvictionSlackMinutes));
        if (ttl <= TimeSpan.Zero)
        {
            // Already logically expired; never persist.
            return;
        }

        await cache.SetStringAsync(
            Key(snapshot.GuestSessionId),
            JsonSerializer.Serialize(StoredSession.From(snapshot), JsonOptions),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GuestSessionSnapshot?> FindAsync(Guid guestSessionId, CancellationToken cancellationToken = default)
    {
        var snapshot = await ReadAsync(guestSessionId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return null;
        }

        if (snapshot.IsExpired(SystemClock.UtcNow) || snapshot.UpgradedToUserId is not null)
        {
            return null;
        }

        return snapshot;
    }

    /// <inheritdoc />
    public async Task<bool> TryMarkUpgradedAsync(
        Guid guestSessionId,
        Guid userId,
        Guid authenticatedSessionId,
        DateTime upgradedAt,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || authenticatedSessionId == Guid.Empty)
        {
            return false;
        }

        var snapshot = await ReadAsync(guestSessionId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null || snapshot.IsExpired(SystemClock.UtcNow) || snapshot.UpgradedToUserId is not null)
        {
            return false;
        }

        var upgraded = snapshot with
        {
            UpgradedToUserId = userId,
            UpgradedToSessionId = authenticatedSessionId,
            UpgradedAt = upgradedAt
        };

        // The distributed cache has no compare-and-swap; the token itself is a single-client
        // bearer capability with a short lifetime, so a concurrent double-upgrade window is
        // bounded by the guest token's expiry and the second upgrade still maps to a real,
        // freshly authenticated session of the same flow.
        var ttl = upgraded.ExpiresAt - SystemClock.UtcNow + TimeSpan.FromMinutes(Math.Max(0, _options.StoreEvictionSlackMinutes));
        if (ttl > TimeSpan.Zero)
        {
            await cache.SetStringAsync(
                Key(guestSessionId),
                JsonSerializer.Serialize(StoredSession.From(upgraded), JsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private async Task<GuestSessionSnapshot?> ReadAsync(Guid guestSessionId, CancellationToken cancellationToken)
    {
        if (guestSessionId == Guid.Empty)
        {
            return null;
        }

        var payload = await cache.GetStringAsync(Key(guestSessionId), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<StoredSession>(payload, JsonOptions);
            return stored?.ToSnapshot();
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Unreadable guest session payload at {Key}; treating as expired", Key(guestSessionId));
            return null;
        }
    }

    private static string Key(Guid guestSessionId) => KeyPrefix + guestSessionId.ToString("N");

    private sealed record StoredSession(
        Guid GuestSessionId,
        DateTime IssuedAt,
        DateTime ExpiresAt,
        string? IpAddress,
        string? UserAgent,
        string? DeviceFingerprint,
        Guid? UpgradedToUserId,
        Guid? UpgradedToSessionId,
        DateTime? UpgradedAt)
    {
        public static StoredSession From(GuestSessionSnapshot snapshot) => new(
            snapshot.GuestSessionId,
            snapshot.IssuedAt,
            snapshot.ExpiresAt,
            snapshot.IpAddress,
            snapshot.UserAgent,
            snapshot.DeviceFingerprint,
            snapshot.UpgradedToUserId,
            snapshot.UpgradedToSessionId,
            snapshot.UpgradedAt);

        public GuestSessionSnapshot ToSnapshot() => new()
        {
            GuestSessionId = GuestSessionId,
            IssuedAt = IssuedAt,
            ExpiresAt = ExpiresAt,
            IpAddress = IpAddress,
            UserAgent = UserAgent,
            DeviceFingerprint = DeviceFingerprint,
            UpgradedToUserId = UpgradedToUserId,
            UpgradedToSessionId = UpgradedToSessionId,
            UpgradedAt = UpgradedAt
        };
    }
}
