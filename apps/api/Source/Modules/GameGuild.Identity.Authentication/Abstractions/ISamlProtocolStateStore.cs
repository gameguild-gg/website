using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Server-side SAML protocol state, backed by <see cref="IDistributedCache"/> (Redis in
///     production, the host's distributed-memory cache otherwise) so flows started on one
///     instance complete on any other — the same pattern as the WebAuthn challenge store.
///     Holds two single-use facts: outstanding AuthnRequest IDs (<c>InResponseTo</c>
///     correlation, fail-closed against unsolicited responses) and consumed assertion IDs
///     (replay protection).
/// </summary>
public interface ISamlProtocolStateStore
{
    /// <summary>
    ///     Records that <paramref name="requestId" /> was issued as an AuthnRequest ID for
    ///     provider <paramref name="slug" />, valid for <paramref name="timeToLive" />.
    /// </summary>
    Task SavePendingAuthnRequestAsync(string slug, string requestId, TimeSpan timeToLive, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Consumes (single-use) the pending AuthnRequest <paramref name="requestId" /> for
    ///     provider <paramref name="slug" />. Returns false when the request is unknown,
    ///     already used, or expired — the response must then be rejected.
    /// </summary>
    Task<bool> ConsumePendingAuthnRequestAsync(string slug, string requestId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reserves (single-use) the assertion <paramref name="assertionId" /> for provider
    ///     <paramref name="slug" /> for <paramref name="timeToLive" />. Returns false when the
    ///     assertion was already consumed — a replayed response.
    /// </summary>
    Task<bool> TryReserveAssertionIdAsync(string slug, string assertionId, TimeSpan timeToLive, CancellationToken cancellationToken = default);
}

/// <summary>
///     <see cref="IDistributedCache"/> implementation of the SAML protocol state. Values are
///     opaque markers (never assertion content); pending-request entries live for the
///     AuthnRequest round-trip window and assertion markers live past the assertion's
///     NotOnOrAfter so a replayed assertion can never be accepted.
/// </summary>
public sealed class DistributedCacheSamlProtocolStateStore(
    IDistributedCache cache,
    ILogger<DistributedCacheSamlProtocolStateStore>? logger = null) : ISamlProtocolStateStore
{
    private const string PendingRequestPrefix = "auth:saml:pending:";
    private const string AssertionPrefix = "auth:saml:assertion:";

    public Task SavePendingAuthnRequestAsync(string slug, string requestId, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(requestId);
        return cache.SetStringAsync(
            PendingRequestPrefix + SlugSegment(slug) + ":" + requestId,
            "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeToLive },
            cancellationToken);
    }

    public async Task<bool> ConsumePendingAuthnRequestAsync(string slug, string requestId, CancellationToken cancellationToken = default)
    {
        var key = PendingRequestPrefix + SlugSegment(slug) + ":" + requestId;
        var pending = await cache.GetStringAsync(key, cancellationToken).ConfigureAwait(false);
        if (pending is null)
        {
            return false;
        }

        await cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> TryReserveAssertionIdAsync(string slug, string assertionId, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        var key = AssertionPrefix + SlugSegment(slug) + ":" + assertionId;
        var existing = await cache.GetStringAsync(key, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            logger?.LogWarning("SAML assertion replay detected for provider {Slug} (assertion ID already consumed)", slug);
            return false;
        }

        await cache.SetStringAsync(
            key,
            "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeToLive },
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static string SlugSegment(string slug) => slug.Replace(':', '_');
}
