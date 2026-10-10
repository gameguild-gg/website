using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace GameGuild.Configuration.ApplicationLayer;

/// <summary>
///     Single policy-aware resolution point for refresh-token lifetimes.
///     Standard sessions use <see cref="JwtOptions.RefreshTokenExpirationDays" />; persistent
///     ("remember me") sessions use <see cref="JwtOptions.PersistentRefreshTokenExpirationDays" />.
///     Both honor the raw-config fallback chains kept for legacy deployments that do not bind
///     the typed options, so every issuing path derives the same duration for the same request.
/// </summary>
public static class RefreshTokenLifetimeResolver
{
    /// <summary>Default standard refresh lifetime in days when nothing is configured.</summary>
    public const int StandardDefaultDays = 7;

    /// <summary>Default persistent ("remember me") refresh lifetime in days when nothing is configured.</summary>
    public const int PersistentDefaultDays = 30;

    /// <summary>
    ///     Resolves the refresh-token lifetime in days for a sign-in request.
    /// </summary>
    /// <param name="jwtOptions">Typed JWT options when the host binds them; otherwise null.</param>
    /// <param name="configuration">Raw configuration for the legacy fallback chain.</param>
    /// <param name="persistent">Whether the sign-in requested a persistent ("remember me") session.</param>
    public static int ResolveExpirationDays(IOptions<JwtOptions>? jwtOptions, IConfiguration configuration, bool persistent)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (jwtOptions is not null)
        {
            return persistent
                ? jwtOptions.Value.PersistentRefreshTokenExpirationDays
                : jwtOptions.Value.RefreshTokenExpirationDays;
        }

        return persistent
            ? ParseDays(configuration, PersistentDefaultDays, "Jwt:PersistentRefreshTokenExpirationDays", "Jwt:PersistentRefreshTokenExpiryInDays")
            : ParseDays(configuration, StandardDefaultDays, "Jwt:RefreshTokenExpirationDays", "Jwt:RefreshTokenExpiryInDays");
    }

    /// <summary>
    ///     Resolves the lifetime (in days) a replacement refresh token must be minted with
    ///     during rotation. The session is classified by its originating policy — a stored
    ///     originating duration at or beyond the configured persistent lifetime marks a
    ///     persistent ("remember me") session, which renews at the configured persistent
    ///     lifetime; every other row (standard sessions, legacy or malformed rows, and rows
    ///     minted under different configuration) renews at the configured standard lifetime.
    ///     The raw stored duration itself is never copied into the replacement: rotation
    ///     preserves the originating CreatedAt while advancing ExpiresAt, so replaying the
    ///     stored span would compound elapsed time and drift past every configured deadline
    ///     (short-lived or absolute-session-capped rows would renew with a stale lifetime).
    /// </summary>
    /// <param name="jwtOptions">Typed JWT options when the host binds them; otherwise null.</param>
    /// <param name="configuration">Raw configuration for the legacy fallback chain.</param>
    /// <param name="createdAt">UTC creation time of the stored refresh token.</param>
    /// <param name="expiresAt">UTC expiration time of the stored refresh token.</param>
    public static int ResolveRenewalDays(
        IOptions<JwtOptions>? jwtOptions,
        IConfiguration configuration,
        DateTime createdAt,
        DateTime expiresAt)
    {
        var standardDays = ResolveExpirationDays(jwtOptions, configuration, persistent: false);
        var persistentDays = ResolveExpirationDays(jwtOptions, configuration, persistent: true);
        var originatingLifetime = ResolveOriginatingLifetime(createdAt, expiresAt, TimeSpan.FromDays(standardDays));

        return originatingLifetime >= TimeSpan.FromDays(persistentDays) ? persistentDays : standardDays;
    }

    /// <summary>
    ///     Recovers the lifetime a session was issued with so rotation can classify the
    ///     session (persistent vs standard). Falls back to the configured duration for
    ///     legacy or malformed rows.
    /// </summary>
    /// <param name="createdAt">UTC creation time of the stored refresh token.</param>
    /// <param name="expiresAt">UTC expiration time of the stored refresh token.</param>
    /// <param name="fallback">Configured duration used when the stored lifetime is not positive.</param>
    public static TimeSpan ResolveOriginatingLifetime(DateTime createdAt, DateTime expiresAt, TimeSpan fallback)
    {
        if (createdAt <= DateTime.UnixEpoch || expiresAt <= DateTime.UnixEpoch)
        {
            return fallback;
        }

        var duration = expiresAt - createdAt;
        return duration > TimeSpan.Zero ? duration : fallback;
    }

    private static int ParseDays(IConfiguration configuration, int defaultValue, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value) && int.TryParse(value, out var parsed) && parsed > 0)
            {
                return parsed;
            }
        }

        return defaultValue;
    }
}
