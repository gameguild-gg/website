namespace GameGuild.Identity.Authentication;

/// <summary>
///     Result of building an OIDC authorization redirect for a configured federation provider.
/// </summary>
public sealed record OidcSignInChallenge
{
    /// <summary>
    ///     The provider's authorization endpoint URL with client, redirect, scope, and state parameters.
    /// </summary>
    public required string AuthUrl { get; init; }

    /// <summary>
    ///     The CSRF state parameter embedded in <see cref="AuthUrl" /> (returned separately so the
    ///     caller can stash it in its state cookie, mirroring the Discord flow).
    /// </summary>
    public required string State { get; init; }
}

/// <summary>
///     A validated federated identity derived from the provider's cryptographically verified ID token.
/// </summary>
public sealed record OidcFederatedIdentity
{
    /// <summary>
    ///     The federation provider slug the identity was authenticated against.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    ///     The mapped subject claim — the provider-stable unique identifier.
    /// </summary>
    public required string ProviderKey { get; init; }

    public string? Email { get; init; }

    public bool EmailVerified { get; init; }

    public string? Name { get; init; }

    /// <summary>
    ///     Authentication method references from the <c>amr</c> claim (empty when absent).
    /// </summary>
    public IReadOnlyList<string> Amr { get; init; } = [];

    /// <summary>
    ///     Authentication context class reference from the <c>acr</c> claim (null when absent).
    /// </summary>
    public string? Acr { get; init; }

    /// <summary>
    ///     Whether <see cref="Amr" /> contains the OIDC multiple-factor method reference ("mfa").
    /// </summary>
    public bool HasMfaProof => Amr.Contains("mfa", StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The scope tokens granted at this authorization (the provider's configured scopes),
    ///     recorded on the external-login consent trail like the social providers.
    /// </summary>
    public IReadOnlyList<string> GrantedScopes { get; init; } = [];
}

/// <summary>
///     A discovered federation provider serving one email domain.
/// </summary>
public sealed record OidcDiscoveredProvider
{
    public required string Slug { get; init; }

    public required string DisplayName { get; init; }
}

/// <summary>
///     Generic OpenID Connect federation for enterprise identity providers: discovery-document
///     metadata (cached), authorization-code exchange, and cryptographic ID-token validation
///     against the provider's JWKS. Config-gated per <c>Authentication:ExternalProviders:Oidc:&lt;slug&gt;</c>;
///     every operation fails closed when the provider is missing or disabled.
/// </summary>
public interface IOidcFederationService
{
    /// <summary>
    ///     Builds the authorization redirect for <paramref name="slug" /> from its discovered
    ///     authorization endpoint. The state parameter is caller-supplied CSRF protection.
    /// </summary>
    Task<OidcSignInChallenge> BuildAuthorizationUrlAsync(string slug, string redirectUri, string state, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Exchanges the authorization code at the discovered token endpoint and cryptographically
    ///     validates the returned ID token (issuer, audience, lifetime, RS256 signature via JWKS),
    ///     mapping claims through the configured <c>ClaimMapping</c>.
    /// </summary>
    Task<OidcFederatedIdentity> AuthenticateCallbackAsync(string slug, string code, string state, string redirectUri, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the provider's <c>end_session_endpoint</c> for front-channel logout forwarding
    ///     when the discovery document advertises one; null when it does not.
    /// </summary>
    Task<string?> GetEndSessionEndpointAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Domain-to-provider discovery: the enabled federation providers whose configured
    ///     <c>EmailDomains</c> include <paramref name="emailDomain" /> (case-insensitive).
    /// </summary>
    IReadOnlyList<OidcDiscoveredProvider> FindProvidersForEmailDomain(string emailDomain);
}
