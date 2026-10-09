namespace GameGuild.Identity.Authorization;

/// <summary>
///     Behavior of the external authorization-decision integration when the external
///     service cannot return a usable decision (network failure, timeout, non-2xx
///     response, unparseable payload, failed token acquisition).
/// </summary>
public enum ExternalAuthorizationFailMode
{
    /// <summary>
    ///     Observe: unavailable external decisions are logged as warnings and treated as
    ///     "no decision" — local resolution applies unchanged. Rollout mode only: during
    ///     an outage the external policy stops constraining access.
    /// </summary>
    Observe = 0,

    /// <summary>
    ///     Enforce (default): an unavailable external decision denies the queried
    ///     permission (fail-closed). Use in production whenever the external policy is
    ///     expected to constrain access.
    /// </summary>
    Enforce = 1
}

/// <summary>
///     Configuration for the external authorization-decision integration (issue #146),
///     bound from the <c>Authorization:ExternalDecision</c> section. Disabled by
///     default: with <c>Enabled = false</c> the built-in HTTP provider returns no
///     decision for any query, no outbound call is ever made and effective-permission
///     resolution keeps purely local semantics with zero overhead.
/// </summary>
/// <remarks>
///     <para>
///         <b>Protocol.</b> The built-in provider speaks OAuth2 client-credentials for
///         authentication against the decision endpoint: it acquires a bearer token from
///         <see cref="TokenEndpoint"/> using <see cref="ClientId"/>/<see cref="ClientSecret"/>,
///         caches it until shortly before expiry, and POSTs the permission-shaped query
///         as JSON to <see cref="Endpoint"/>. Both endpoints must use HTTPS.
///     </para>
///     <para>
///         <b>SAML authorization-assertion integration is intentionally deferred.</b>
///         SAML carries authorization statements inside signed authentication
///         assertions; consuming them as decision inputs requires product decisions
///         (assertion freshness, attribute mapping, per-IdP trust configuration) that
///         are out of scope for this seam. Until that lands, SAML-authenticated
///         identities authorize through the local layers like every other identity,
///         and any external decision service is integrated over the OAuth2
///         client-credentials contract above. See
///         <c>apps/api/docs/effective-permission-resolution.md</c>.
///     </para>
/// </remarks>
public sealed class ExternalAuthorizationOptions
{
    /// <summary>
    ///     The configuration section name (<c>Authorization:ExternalDecision</c>).
    /// </summary>
    public const string SectionName = "Authorization:ExternalDecision";

    /// <summary>
    ///     Master switch. <c>false</c> (default) keeps resolution purely local: the
    ///     provider returns no decision and makes no outbound call.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     HTTPS endpoint receiving permission-shaped decision requests
    ///     (POST, JSON body, bearer-token authorized). Required when enabled.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    ///     HTTPS OAuth2 token endpoint used for the client-credentials grant.
    ///     Required when enabled.
    /// </summary>
    public string? TokenEndpoint { get; set; }

    /// <summary>OAuth2 client id for the client-credentials grant. Required when enabled.</summary>
    public string? ClientId { get; set; }

    /// <summary>OAuth2 client secret for the client-credentials grant. Required when enabled.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    ///     Per-request timeout for token and decision calls. Default 5 seconds. Must be
    ///     positive and at most 2 minutes.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     How long a cached external decision is reused for an identical query.
    ///     Default 30 seconds. <see cref="TimeSpan.Zero"/> disables decision caching
    ///     (every resolution re-queries the external service).
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Behavior when the external service is unavailable or returns an unusable
    ///     response. Default <see cref="ExternalAuthorizationFailMode.Enforce"/>
    ///     (fail-closed: the queried permission is denied).
    /// </summary>
    public ExternalAuthorizationFailMode FailMode { get; set; } = ExternalAuthorizationFailMode.Enforce;

    /// <summary>
    ///     Validates the configuration. Disabled configuration always passes; enabling
    ///     the integration requires complete, HTTPS-only endpoints and credentials so a
    ///     half-configured provider can never silently run.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when an enabled configuration is incomplete or invalid.</exception>
    public void Validate()
    {
        if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(2))
        {
            throw new InvalidOperationException(
                $"{SectionName}:Timeout must be greater than zero and at most 2 minutes.");
        }

        if (CacheTtl < TimeSpan.Zero || CacheTtl > TimeSpan.FromHours(1))
        {
            throw new InvalidOperationException(
                $"{SectionName}:CacheTtl must be zero (disabled) or between zero and 1 hour.");
        }

        if (!Enabled)
        {
            return;
        }

        if (!IsHttpsUri(Endpoint))
        {
            throw new InvalidOperationException(
                $"{SectionName}:Endpoint is required when the external decision integration is enabled and must be an absolute HTTPS URI.");
        }

        if (!IsHttpsUri(TokenEndpoint))
        {
            throw new InvalidOperationException(
                $"{SectionName}:TokenEndpoint is required when the external decision integration is enabled and must be an absolute HTTPS URI.");
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new InvalidOperationException(
                $"{SectionName}:ClientId is required when the external decision integration is enabled.");
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            throw new InvalidOperationException(
                $"{SectionName}:ClientSecret is required when the external decision integration is enabled.");
        }
    }

    private static bool IsHttpsUri(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;
}
