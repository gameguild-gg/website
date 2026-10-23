namespace GameGuild.Identity.Authentication;

/// <summary>
///     Result of building a SAML sign-in redirect for a configured federation provider.
/// </summary>
public sealed record SamlSignInChallenge
{
    /// <summary>
    ///     The identity provider's sign-on URL with the deflated <c>SAMLRequest</c> (and the
    ///     caller's <c>RelayState</c>, when supplied) applied — HTTP-Redirect binding.
    /// </summary>
    public required string RedirectUrl { get; init; }

    /// <summary>
    ///     The <c>ID</c> of the generated <c>AuthnRequest</c>. Stored server-side so the
    ///     returned response's <c>InResponseTo</c> can be correlated (single use).
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    ///     The <c>RelayState</c> value embedded in the redirect URL (echoed by the identity
    ///     provider on callback; correlation itself is the server-side <c>InResponseTo</c>).
    /// </summary>
    public required string RelayState { get; init; }
}

/// <summary>
///     A validated federated identity derived from a signature-verified SAML 2.0 response.
/// </summary>
public sealed record SamlFederatedIdentity
{
    /// <summary>
    ///     The federation provider slug the identity was authenticated against.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    ///     The assertion <c>NameID</c> — the provider-stable unique identifier.
    /// </summary>
    public required string ProviderKey { get; init; }

    public string? Email { get; init; }

    /// <summary>
    ///     Whether the provider's email assertion counts as verified — only true when the
    ///     provider is configured with <c>TrustIdpEmailVerification</c> (SAML carries no
    ///     standardized verification proof, so the default is false).
    /// </summary>
    public bool EmailVerified { get; init; }

    public string? Name { get; init; }

    /// <summary>
    ///     The assertion's <c>AuthnContextClassRef</c> values (empty when the assertion
    ///     carries no authentication statement).
    /// </summary>
    public IReadOnlyList<string> AuthnContextClassRefs { get; init; } = [];

    /// <summary>
    ///     Whether any <see cref="AuthnContextClassRefs" /> entry is configured as an MFA
    ///     context class for the provider.
    /// </summary>
    public bool HasMfaProof { get; init; }

    /// <summary>
    ///     The provider's configured tenant allowlist (empty = unrestricted). Sign-ins must
    ///     land on one of these tenants — the secure tenant/provider binding.
    /// </summary>
    public IReadOnlyList<Guid> TenantAllowlist { get; init; } = [];
}

/// <summary>
///     SAML 2.0 service-provider federation for enterprise identity providers: SP metadata
///     generation, SP-initiated sign-in redirects (HTTP-Redirect binding), and
///     cryptographic validation of posted responses (X.509 signatures against config-supplied
///     certificates, issuer/audience/destination checks, clock-skew-bounded lifetimes,
///     <c>InResponseTo</c> correlation, and assertion-ID replay protection via
///     <see cref="ISamlProtocolStateStore" />). Config-gated per
///     <c>Authentication:ExternalProviders:Saml:&lt;slug&gt;</c>; every operation fails closed
///     when the provider is missing or disabled, and unsigned responses are always refused.
/// </summary>
public interface ISamlFederationService
{
    /// <summary>
    ///     Builds the identity provider sign-on redirect for <paramref name="slug" /> carrying a
    ///     fresh <c>AuthnRequest</c> (whose ID is stored for single-use correlation) and the
    ///     caller-supplied <see cref="SamlSignInChallenge.RelayState" />.
    /// </summary>
    Task<SamlSignInChallenge> BuildSignInRedirectAsync(string slug, string? relayState, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Base64-decodes and validates the posted <c>SAMLResponse</c> for <paramref name="slug" />:
    ///     XML-DSig signature against the configured certificates (unsigned or wrongly-signed
    ///     responses are refused), issuer, status, destination, audience, clock-skew-bounded
    ///     lifetimes, <c>InResponseTo</c> correlation with an outstanding AuthnRequest, and
    ///     single-use assertion IDs. Returns the mapped identity on success.
    /// </summary>
    Task<SamlFederatedIdentity> ConsumeAssertionAsync(string slug, string samlResponse, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Builds this service provider's SAML 2.0 metadata document (entity descriptor with
    ///     the POST-binding assertion consumer service) for exchange with the identity
    ///     provider.
    /// </summary>
    string BuildServiceProviderMetadata(string slug);

    /// <summary>
    ///     Builds the provider's front-channel single-logout redirect (an unsigned
    ///     <c>LogoutRequest</c> via HTTP-Redirect binding) when the provider is configured with
    ///     a single logout service URL; null when logout stays local.
    /// </summary>
    string? BuildLogoutRedirectUrl(string slug, string? postLogoutRedirectUri);

    /// <summary>
    ///     Domain-to-provider discovery: lists the enabled SAML providers whose configured
    ///     <c>EmailDomains</c> include the requested address's domain, so the login page can
    ///     route enterprise users before any session exists. Reveals only configured slugs.
    /// </summary>
    IReadOnlyList<OidcDiscoveredProvider> FindProvidersForEmailDomain(string emailDomain);
}
