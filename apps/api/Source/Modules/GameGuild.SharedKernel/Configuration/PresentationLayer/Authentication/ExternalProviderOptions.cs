namespace GameGuild.Configuration.PresentationLayer.Authentication;

/// <summary>
///     Configuration for OAuth 2.0 identity providers supported by GameGuild, plus
///     config-gated generic OIDC federation providers (see <see cref="Oidc" />).
/// </summary>
public sealed partial class ExternalProviderOptions
{
    private static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "discord",
        "github",
        "google",
        "microsoft"
    };

    /// <summary>
    ///     Provider settings keyed by provider name. Client secrets should be supplied by the deployment's secret store.
    /// </summary>
    public Dictionary<string, OAuthProviderOptions> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Validate()
    {
        if (Providers is null)
        {
            throw new InvalidOperationException("External provider settings are required.");
        }

        foreach (var (providerName, providerOptions) in Providers)
        {
            if (!SupportedProviders.Contains(providerName))
            {
                throw new InvalidOperationException($"External provider '{providerName}' is not supported.");
            }

            (providerOptions ?? throw new InvalidOperationException($"External provider settings for '{providerName}' are required."))
                .Validate(providerName);
        }

        if (Oidc is null)
        {
            throw new InvalidOperationException("OIDC federation provider settings are required.");
        }

        foreach (var (slug, oidcOptions) in Oidc)
        {
            (oidcOptions ?? throw new InvalidOperationException($"OIDC federation settings for '{slug}' are required."))
                .Validate(slug);
        }
    }
}

/// <summary>
///     Credentials and requested scopes for one OAuth 2.0 identity provider.
/// </summary>
public sealed class OAuthProviderOptions
{
    /// <summary>
    ///     Whether this provider is enabled for authorization-code and profile requests.
    /// </summary>
    public bool Enabled { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    /// <summary>
    ///     Optional provider-specific scopes. An empty list uses the provider's safe application defaults.
    /// </summary>
    public List<string> Scopes { get; set; } = [];

    /// <summary>
    ///     Optional HTTPS endpoint overrides for providers hosted in a national cloud or custom deployment.
    ///     If one endpoint is overridden, all three must be provided.
    /// </summary>
    public string? AuthorizationEndpoint { get; set; }

    public string? TokenEndpoint { get; set; }

    public string? UserInformationEndpoint { get; set; }

    /// <summary>
    ///     Optional GitHub endpoint for listing the authenticated user's email addresses.
    /// </summary>
    public string? UserEmailEndpoint { get; set; }

    /// <summary>
    ///     Microsoft identity tenant. Supported values are common, organizations, consumers, or a tenant GUID.
    /// </summary>
    public string Tenant { get; set; } = "common";

    public void Validate(string providerName)
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ClientId) || ClientId.Any(char.IsControl))
        {
            throw new InvalidOperationException($"OAuth client ID is required for enabled provider '{providerName}'.");
        }

        if (string.IsNullOrWhiteSpace(ClientSecret) || ClientSecret.Any(char.IsControl))
        {
            throw new InvalidOperationException($"OAuth client secret is required for enabled provider '{providerName}'.");
        }

        if (Scopes is null)
        {
            throw new InvalidOperationException($"OAuth scopes for provider '{providerName}' cannot be null.");
        }

        var endpoints = new[] { AuthorizationEndpoint, TokenEndpoint, UserInformationEndpoint };
        if (endpoints.Any(endpoint => !string.IsNullOrWhiteSpace(endpoint)))
        {
            if (endpoints.Any(string.IsNullOrWhiteSpace) ||
                endpoints.Any(endpoint => !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
                                          uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)))
            {
                throw new InvalidOperationException($"OAuth endpoint overrides for provider '{providerName}' must all be absolute HTTPS URLs without embedded credentials.");
            }
        }

        if (!string.IsNullOrWhiteSpace(UserEmailEndpoint))
        {
            if (!providerName.Equals("github", StringComparison.OrdinalIgnoreCase) ||
                !Uri.TryCreate(UserEmailEndpoint, UriKind.Absolute, out var emailEndpoint) ||
                emailEndpoint.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(emailEndpoint.UserInfo))
            {
                throw new InvalidOperationException("A GitHub user email endpoint must be an absolute HTTPS URL without embedded credentials.");
            }
        }

        var uniqueScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var scope in Scopes)
        {
            if (string.IsNullOrWhiteSpace(scope) || scope.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
            {
                throw new InvalidOperationException($"OAuth scopes for provider '{providerName}' must be non-empty scope tokens.");
            }

            if (!uniqueScopes.Add(scope))
            {
                throw new InvalidOperationException($"OAuth scopes for provider '{providerName}' cannot contain duplicates.");
            }
        }

        if (providerName.Equals("microsoft", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(Tenant) ||
                (!Tenant.Equals("common", StringComparison.OrdinalIgnoreCase) &&
                 !Tenant.Equals("organizations", StringComparison.OrdinalIgnoreCase) &&
                 !Tenant.Equals("consumers", StringComparison.OrdinalIgnoreCase) &&
                 !Guid.TryParse(Tenant, out _)))
            {
                throw new InvalidOperationException("Microsoft OAuth tenant must be common, organizations, consumers, or a tenant GUID.");
            }

            if (Scopes.Count > 0 &&
                new[] { "openid", "email", "profile" }.Any(requiredScope => !uniqueScopes.Contains(requiredScope)))
            {
                throw new InvalidOperationException("Microsoft OAuth scopes must include openid, email, and profile.");
            }
        }
    }
}
