using System.Text.RegularExpressions;

namespace GameGuild.Configuration.PresentationLayer.Authentication;

public sealed partial class ExternalProviderOptions
{
    /// <summary>
    ///     Generic OIDC federation providers keyed by deployment-assigned slug
    ///     (<c>Authentication:ExternalProviders:Oidc:&lt;slug&gt;</c>). Every entry defaults to
    ///     disabled; unknown slugs fail closed at validation time.
    /// </summary>
    public Dictionary<string, OidcProviderOptions> Oidc { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex OidcSlugPattern = CreateOidcSlugPattern();

    /// <summary>
    ///     Validates an OIDC federation slug: lowercase ASCII letters, digits, and hyphens,
    ///     starting and ending with a letter or digit (URL-path safe, max 64 characters).
    /// </summary>
    public static bool IsValidOidcSlug(string? slug) =>
        !string.IsNullOrEmpty(slug) && OidcSlugPattern.IsMatch(slug);

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]{0,62}[a-z0-9]$|^[a-z0-9]$", RegexOptions.None)]
    private static partial Regex CreateOidcSlugPattern();
}

/// <summary>
///     Configuration for one generic OpenID Connect federation provider (enterprise IdPs).
///     Disabled by default; only enabled entries are validated and reachable.
/// </summary>
public sealed partial class OidcProviderOptions
{
    /// <summary>
    ///     Logical claim names that may be remapped through <see cref="ClaimMapping" />.
    /// </summary>
    public static readonly IReadOnlySet<string> SupportedClaimMappingKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sub", "email", "name", "email_verified" };

    /// <summary>
    ///     Whether this federation provider is enabled for sign-in. Fail closed by default.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The issuer authority; <c>.well-known/openid-configuration</c> is discovered below it.
    ///     Must be an absolute HTTPS URL without embedded credentials.
    /// </summary>
    public string? Authority { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    /// <summary>
    ///     Optional display name surfaced to users (defaults to the slug).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    ///     Requested scopes. Defaults to <c>openid profile email</c>; must always include <c>openid</c>.
    /// </summary>
    public List<string> Scopes { get; set; } = ["openid", "profile", "email"];

    /// <summary>
    ///     Maps logical claim names (<c>sub</c>/<c>email</c>/<c>name</c>/<c>email_verified</c>)
    ///     to the provider's ID-token claim types. Unmapped logical claims use their own name.
    /// </summary>
    public Dictionary<string, string> ClaimMapping { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Email domains served by this provider. When non-empty, sign-in is accepted only
    ///     for identities whose email domain is listed (domain-to-provider discovery gate).
    /// </summary>
    public List<string> EmailDomains { get; set; } = [];

    public void Validate(string slug)
    {
        if (!ExternalProviderOptions.IsValidOidcSlug(slug))
        {
            throw new InvalidOperationException($"OIDC provider slug '{slug}' must be lowercase letters, digits, and hyphens (max 64 characters).");
        }

        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ClientId) || ClientId.Any(char.IsControl))
        {
            throw new InvalidOperationException($"OIDC client ID is required for enabled provider '{slug}'.");
        }

        if (string.IsNullOrWhiteSpace(ClientSecret) || ClientSecret.Any(char.IsControl))
        {
            throw new InvalidOperationException($"OIDC client secret is required for enabled provider '{slug}'.");
        }

        if (string.IsNullOrWhiteSpace(Authority) ||
            !Uri.TryCreate(Authority, UriKind.Absolute, out var authority) ||
            authority.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(authority.UserInfo) ||
            !string.IsNullOrEmpty(authority.Query) ||
            !string.IsNullOrEmpty(authority.Fragment))
        {
            throw new InvalidOperationException($"OIDC authority for provider '{slug}' must be an absolute HTTPS URL without credentials, query, or fragment.");
        }

        if (Scopes is null)
        {
            throw new InvalidOperationException($"OIDC scopes for provider '{slug}' cannot be null.");
        }

        var uniqueScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var scope in Scopes)
        {
            if (string.IsNullOrWhiteSpace(scope) || scope.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
            {
                throw new InvalidOperationException($"OIDC scopes for provider '{slug}' must be non-empty scope tokens.");
            }

            if (!uniqueScopes.Add(scope))
            {
                throw new InvalidOperationException($"OIDC scopes for provider '{slug}' cannot contain duplicates.");
            }
        }

        if (!uniqueScopes.Contains("openid"))
        {
            throw new InvalidOperationException($"OIDC scopes for provider '{slug}' must include 'openid'.");
        }

        if (ClaimMapping is null)
        {
            throw new InvalidOperationException($"OIDC claim mapping for provider '{slug}' cannot be null.");
        }

        foreach (var (logicalClaim, providerClaim) in ClaimMapping)
        {
            if (!SupportedClaimMappingKeys.Contains(logicalClaim))
            {
                throw new InvalidOperationException($"OIDC claim mapping key '{logicalClaim}' for provider '{slug}' is not a supported logical claim.");
            }

            if (string.IsNullOrWhiteSpace(providerClaim) || providerClaim.Any(char.IsControl))
            {
                throw new InvalidOperationException($"OIDC claim mapping for '{logicalClaim}' of provider '{slug}' must map to a non-empty claim name.");
            }
        }

        if (EmailDomains is null)
        {
            throw new InvalidOperationException($"OIDC email domains for provider '{slug}' cannot be null.");
        }

        var uniqueDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var domain in EmailDomains)
        {
            if (string.IsNullOrWhiteSpace(domain) ||
                domain.StartsWith('@') ||
                !MailAddressDomainPattern().IsMatch(domain) ||
                !uniqueDomains.Add(domain))
            {
                throw new InvalidOperationException($"OIDC email domains for provider '{slug}' must be unique DNS hostnames without '@'.");
            }
        }
    }

    [GeneratedRegex(@"^(?=.{1,253}$)[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MailAddressDomainPattern();
}
