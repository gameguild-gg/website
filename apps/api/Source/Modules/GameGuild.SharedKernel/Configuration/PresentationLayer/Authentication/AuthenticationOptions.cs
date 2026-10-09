using Microsoft.AspNetCore.Http;

namespace GameGuild.Configuration.PresentationLayer.Authentication;

/// <summary>
///     Configuration options for authentication and authorization
/// </summary>
public sealed class AuthenticationOptions : BaseOptions
{
    /// <summary>
    ///     The configuration section name for this options type.
    /// </summary>
    public const string SectionName = "Authentication";

    public bool EnableAuthentication { get; set; } = true;

    public bool EnableAuthorization { get; set; } = true;

    public bool EnableDacAuthorization { get; set; } = true;

    /// <summary>
    ///     Registers the API key scheme alongside JWT bearer authentication.
    /// </summary>
    public bool EnableApiKeyAuthentication { get; set; }

    /// <summary>
    ///     Registers an opt-in HTTP Basic authentication scheme alongside JWT bearer authentication.
    ///     The scheme requires HTTPS for every request.
    /// </summary>
    public bool EnableBasicAuthentication { get; set; }

    /// <summary>
    ///     Settings for the optional HTTP Basic scheme.
    /// </summary>
    public BasicAuthenticationSettings? Basic { get; set; } = new();

    /// <summary>
    ///     Registers an opt-in X.509 client-certificate (mTLS) authentication scheme
    ///     alongside JWT bearer authentication. Client certificates must chain to the
    ///     configured CA allowlist and be bound to a service account; validated
    ///     certificates authenticate as <c>ActorKind.Service</c>.
    /// </summary>
    public bool EnableClientCertificateAuthentication { get; set; }

    /// <summary>
    ///     Settings for the optional client-certificate scheme. The CA allowlist is
    ///     required when the scheme is enabled (fail closed when unconfigured).
    /// </summary>
    public ClientCertificateAuthenticationSettings? ClientCertificate { get; set; } = new();

    /// <summary>
    ///     Registers an opt-in cookie authentication scheme alongside the default JWT bearer scheme.
    /// </summary>
    public bool EnableCookieAuthentication { get; set; }

    /// <summary>
    ///     Cookie scheme settings. Cookies are emitted with secure, HTTP-only defaults.
    /// </summary>
    public CookieAuthenticationSettings? Cookie { get; set; } = new();

    /// <summary>
    ///     Password requirements for local account registration.
    /// </summary>
    public AuthenticationPasswordPolicySettings PasswordPolicy { get; set; } = new();

    /// <summary>
    ///     OAuth 2.0 provider credentials and scope configuration.
    /// </summary>
    public ExternalProviderOptions ExternalProviders { get; set; } = new();

    /// <summary>
    ///     Header used to submit API keys when the API key scheme is enabled.
    /// </summary>
    public string? ApiKeyHeaderName { get; set; }

    /// <summary>
    ///     Allows API keys in a query parameter. This is disabled by default because URLs are commonly logged.
    ///     When enabled, requests must use HTTPS.
    /// </summary>
    public bool AllowApiKeyInQueryString { get; set; }

    /// <summary>
    ///     Query parameter used when <see cref="AllowApiKeyInQueryString"/> is enabled.
    /// </summary>
    public string? ApiKeyQueryStringParameterName { get; set; }

    /// <summary>
    ///     Optional programmatic resolver for API keys supplied in a deployment-specific request location.
    ///     This delegate is configured in host code and is not bindable from configuration files.
    /// </summary>
    public Func<HttpRequest, string?>? ApiKeyCustomKeyResolver { get; set; }

    public string JwtSecretKey { get; set; } = string.Empty;

    public string JwtIssuer { get; set; } = string.Empty;

    public string JwtAudience { get; set; } = string.Empty;

    /// <summary>
    ///     Lifetime for newly issued access tokens.
    /// </summary>
    public TimeSpan JwtExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    ///     Lifetime for refresh tokens, in days.
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 30;

    public override void Validate()
    {
        base.Validate();

        if (EnableApiKeyAuthentication && !EnableAuthentication)
        {
            throw new InvalidOperationException("API key authentication cannot be enabled when authentication is disabled.");
        }

        if (EnableCookieAuthentication && !EnableAuthentication)
        {
            throw new InvalidOperationException("Cookie authentication cannot be enabled when authentication is disabled.");
        }

        if (EnableBasicAuthentication && !EnableAuthentication)
        {
            throw new InvalidOperationException("Basic authentication cannot be enabled when authentication is disabled.");
        }

        if (EnableClientCertificateAuthentication && !EnableAuthentication)
        {
            throw new InvalidOperationException(
                "Client certificate authentication cannot be enabled when authentication is disabled.");
        }

        if (EnableCookieAuthentication)
        {
            (Cookie ?? throw new InvalidOperationException("Cookie authentication settings are required when the cookie scheme is enabled."))
                .Validate();
        }

        if (EnableBasicAuthentication)
        {
            (Basic ?? throw new InvalidOperationException("Basic authentication settings are required when the Basic scheme is enabled."))
                .Validate();
        }

        if (EnableClientCertificateAuthentication)
        {
            var clientCertificate = ClientCertificate
                ?? throw new InvalidOperationException(
                    "Client certificate authentication settings are required when the client certificate scheme is enabled.");
            clientCertificate.Validate();

            if (clientCertificate.TrustedCaCertificates.Length == 0)
            {
                // Fail closed: without a CA allowlist any PKI-trusted certificate would authenticate.
                throw new InvalidOperationException(
                    "Client certificate authentication requires at least one trusted CA certificate; the CA allowlist is unconfigured.");
            }
        }

        (PasswordPolicy ?? throw new InvalidOperationException("Authentication password policy settings are required."))
            .Validate();

        (ExternalProviders ?? throw new InvalidOperationException("External provider settings are required."))
            .Validate();

        if (!EnableAuthentication && ExternalProviders.Providers.Any(provider => provider.Value.Enabled))
        {
            throw new InvalidOperationException("External OAuth providers cannot be enabled when authentication is disabled.");
        }

        if (AllowApiKeyInQueryString && !EnableApiKeyAuthentication)
        {
            throw new InvalidOperationException("API key query authentication requires the API key scheme to be enabled.");
        }

        if (EnableApiKeyAuthentication)
        {
            if (ApiKeyHeaderName is not null && string.IsNullOrWhiteSpace(ApiKeyHeaderName))
            {
                throw new InvalidOperationException("API key header name must not be empty.");
            }

            if (AllowApiKeyInQueryString && ApiKeyQueryStringParameterName is not null &&
                string.IsNullOrWhiteSpace(ApiKeyQueryStringParameterName))
            {
                throw new InvalidOperationException("API key query parameter name must not be empty when query authentication is enabled.");
            }
        }

        if (EnableAuthentication)
        {
            if (string.IsNullOrEmpty(JwtSecretKey))
            {
                throw new InvalidOperationException("JWT secret key must be configured when authentication is enabled.");
            }

            if (string.IsNullOrEmpty(JwtIssuer))
            {
                throw new InvalidOperationException("JWT issuer must be configured when authentication is enabled.");
            }

            if (string.IsNullOrEmpty(JwtAudience))
            {
                throw new InvalidOperationException("JWT audience must be configured when authentication is enabled.");
            }

            if (JwtExpiration <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("JWT expiration must be greater than zero.");
            }

            if (RefreshTokenExpirationDays <= 0)
            {
                throw new InvalidOperationException("Refresh token expiration must be greater than zero.");
            }
        }
    }

    public static AuthenticationOptions CreateDefault() { return new AuthenticationOptions(); }
}
