namespace GameGuild.Identity.Authentication;

/// <summary>
///     Extension contract for one external sign-in provider (OAuth 2.0 / OpenID).
///     Implementations are self-contained: they own their authorization-URL construction,
///     code exchange (or OpenID assertion verification), profile-claims normalization,
///     credential lookup, and safe default scopes. <see cref="OAuthService" /> dispatches
///     by <see cref="ProviderKey" /> over the registered provider set — adding a provider
///     means adding one implementation plus one DI registration, never editing a provider
///     switch (issues #235/#246).
///     <para>
///         Fail-closed contract: every method must throw <see cref="InvalidOperationException" />
///         describing the missing configuration when the provider's credentials are absent
///         or disabled, so an unconfigured provider surfaces as a 503 at the API edge
///         instead of silently half-working.
///     </para>
/// </summary>
public interface IOAuthProvider
{
    /// <summary>
    ///     Unique lowercase provider key used for dispatch and configuration lookup
    ///     (for example <c>github</c>, <c>steam</c>). Also the provider name persisted on
    ///     <see cref="ExternalLogin" /> links.
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    ///     Display name written to <see cref="OAuthUserProfile.Provider" /> (for example <c>GitHub</c>).
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    ///     Safe application-default scopes used when neither the caller nor the typed
    ///     provider configuration supplies any. OpenID 2.0 providers (Steam) return an
    ///     empty list — the protocol has no scope concept.
    /// </summary>
    IReadOnlyList<string> DefaultScopes { get; }

    /// <summary>
    ///     Whether this provider's credentials are configured. Unconfigured providers
    ///     stay registered (fail-closed NoOp semantics, mirroring the geolocation and
    ///     threat-intelligence provider defaults) and refuse operations at call time.
    /// </summary>
    bool IsConfigured();

    /// <summary>
    ///     Builds the provider authorization (or OpenID redirect) URL the user is sent to.
    ///     Must embed the CSRF <paramref name="state" /> and the resolved scope list.
    /// </summary>
    /// <param name="redirectUri">Registered callback URL</param>
    /// <param name="state">CSRF protection state parameter</param>
    /// <param name="scopes">Optional caller-requested scopes</param>
    string BuildAuthorizationUrl(string redirectUri, string state, string[]? scopes = null);

    /// <summary>
    ///     Resolves the exact scope list an authorization request will ask for:
    ///     explicitly requested scopes win, else the configured provider scopes, else
    ///     <see cref="DefaultScopes" />. Implementations deduplicate preserving
    ///     first-seen order and union any protocol-required scopes (Microsoft: openid,
    ///     email, profile). Callback and link handlers persist this list as the granted
    ///     scopes — the single source of truth shared with <see cref="BuildAuthorizationUrl" />.
    /// </summary>
    string[] ResolveScopes(string[]? requestedScopes = null);

    /// <summary>
    ///     Exchanges the callback credential for the provider access token.
    ///     For OAuth 2.0 providers <paramref name="code" /> is the authorization code.
    ///     For OpenID 2.0 providers (Steam) it is the URL-encoded signed callback
    ///     assertion (the full <c>openid.*</c> query string) verified against the
    ///     provider's <c>check_authentication</c> endpoint; the verified subject
    ///     identifier takes the access-token slot because OpenID 2.0 issues no token.
    /// </summary>
    /// <param name="code">Authorization code (OAuth 2.0) or signed callback assertion (OpenID 2.0)</param>
    /// <param name="redirectUri">The same redirect URI used in the authorization request</param>
    /// <returns>Access token, or the verified subject identifier for OpenID 2.0 providers</returns>
    Task<string> ExchangeCodeAsync(string code, string redirectUri);

    /// <summary>
    ///     Fetches the user profile with the provider and normalizes its claims into the
    ///     shared <see cref="OAuthUserProfile" /> shape (provider id, email + verification
    ///     attestation, name parts, avatar). Providers must never infer email verification
    ///     the provider does not attest.
    /// </summary>
    /// <param name="accessToken">Access token from <see cref="ExchangeCodeAsync" />, or the verified subject identifier for OpenID 2.0 providers</param>
    Task<OAuthUserProfile> GetUserProfileAsync(string accessToken);
}
