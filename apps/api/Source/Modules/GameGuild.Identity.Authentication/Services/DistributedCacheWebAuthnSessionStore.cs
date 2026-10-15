using System.Text.Json;
using Fido2NetLib;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Distributed WebAuthn challenge store backed by <see cref="IDistributedCache"/> (issue #234) —
/// the same pattern as <see cref="DistributedCacheTokenRevocationService"/>: Redis in production,
/// the host's local distributed-memory cache otherwise, so ceremonies work across instances.
/// Fido2 options round-trip through their canonical JSON forms. Entries carry a five-minute TTL
/// mirroring the previous process-local cleanup; logical expiry is re-checked on read against the
/// domain clock so a cache that lags cannot revive a stale challenge.
/// </summary>
public sealed class DistributedCacheWebAuthnSessionStore(
    IDistributedCache cache,
    ILogger<DistributedCacheWebAuthnSessionStore> logger) : IWebAuthnSessionStore
{
    /// <summary>How long a challenge session stays decidable.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(5);

    private const string RegistrationKeyPrefix = "auth:webauthn:session:reg:";
    private const string AssertionKeyPrefix = "auth:webauthn:session:auth:";
    private const string GuestKey = "auth:webauthn:session:auth:guest";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public Task<string> StoreRegistrationSessionAsync(WebAuthnSession session, CancellationToken cancellationToken = default) =>
        StoreAsync(session, RegistrationKeyPrefix + OwnerKey(session.UserId), requireUser: true, cancellationToken);

    /// <inheritdoc />
    public Task<string> StoreAssertionSessionAsync(WebAuthnSession session, CancellationToken cancellationToken = default) =>
        StoreAsync(session, session.UserId is null ? GuestKey : AssertionKeyPrefix + OwnerKey(session.UserId), requireUser: false, cancellationToken);

    /// <inheritdoc />
    public Task<WebAuthnSession?> FindRegistrationSessionAsync(Guid userId, CancellationToken cancellationToken = default) =>
        FindAsync(RegistrationKeyPrefix + userId.ToString("N"), cancellationToken);

    /// <inheritdoc />
    public Task<WebAuthnSession?> FindAssertionSessionAsync(Guid userId, CancellationToken cancellationToken = default) =>
        FindAsync(AssertionKeyPrefix + userId.ToString("N"), cancellationToken);

    /// <inheritdoc />
    public Task<WebAuthnSession?> FindGuestAssertionSessionAsync(CancellationToken cancellationToken = default) =>
        FindAsync(GuestKey, cancellationToken);

    /// <inheritdoc />
    public Task RemoveRegistrationSessionAsync(Guid userId, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(RegistrationKeyPrefix + userId.ToString("N"), cancellationToken);

    /// <inheritdoc />
    public Task RemoveAssertionSessionAsync(Guid userId, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(AssertionKeyPrefix + userId.ToString("N"), cancellationToken);

    /// <inheritdoc />
    public Task RemoveGuestAssertionSessionAsync(CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(GuestKey, cancellationToken);

    private async Task<string> StoreAsync(WebAuthnSession session, string key, bool requireUser, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (requireUser && session.UserId is not { } userId)
        {
            throw new InvalidOperationException("A registration ceremony session requires a user id.");
        }

        if (session.RegistrationOptions is null && session.AssertionOptions is null)
        {
            throw new InvalidOperationException("A WebAuthn session must carry registration or assertion options.");
        }

        var payload = new StoredSession(
            session.UserId,
            session.Challenge,
            session.CreatedAt,
            session.RegistrationOptions?.ToJson(),
            session.AssertionOptions?.ToJson());

        await cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(payload, JsonOptions),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = SessionLifetime + EvictionSlack },
            cancellationToken).ConfigureAwait(false);

        return key;
    }

    private async Task<WebAuthnSession?> FindAsync(string key, CancellationToken cancellationToken)
    {
        var payload = await cache.GetStringAsync(key, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        StoredSession? stored;
        try
        {
            stored = JsonSerializer.Deserialize<StoredSession>(payload, JsonOptions);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Unreadable WebAuthn session payload at {Key}; treating as expired", key);
            return null;
        }

        if (stored is null)
        {
            return null;
        }

        // Logical expiry against the domain clock: cache TTLs are a safety net, not the rule.
        if (stored.CreatedAt.Add(SessionLifetime) < SystemClock.UtcNow)
        {
            await cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            return null;
        }

        CredentialCreateOptions? registrationOptions = null;
        AssertionOptions? assertionOptions = null;
        try
        {
            if (stored.RegistrationOptionsJson is not null)
            {
                registrationOptions = CredentialCreateOptions.FromJson(stored.RegistrationOptionsJson);
            }

            if (stored.AssertionOptionsJson is not null)
            {
                assertionOptions = AssertionOptions.FromJson(stored.AssertionOptionsJson);
            }
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Undecodable Fido2 options in the WebAuthn session at {Key}; treating as expired", key);
            return null;
        }

        return new WebAuthnSession
        {
            UserId = stored.UserId,
            Challenge = stored.Challenge ?? [],
            CreatedAt = stored.CreatedAt,
            RegistrationOptions = registrationOptions,
            AssertionOptions = assertionOptions
        };
    }

    private static string OwnerKey(Guid? userId) => userId?.ToString("N") ?? "guest";

    /// <summary>
    ///     Extra retention beyond <see cref="SessionLifetime"/> for the cache-level safety-net
    ///     eviction, so an entry is never judged by the domain clock after physical removal.
    /// </summary>
    private static readonly TimeSpan EvictionSlack = TimeSpan.FromMinutes(5);

    private sealed record StoredSession(
        Guid? UserId,
        byte[]? Challenge,
        DateTime CreatedAt,
        string? RegistrationOptionsJson,
        string? AssertionOptionsJson);
}
