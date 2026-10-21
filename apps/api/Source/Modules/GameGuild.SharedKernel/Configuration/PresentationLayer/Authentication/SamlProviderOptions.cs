using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace GameGuild.Configuration.PresentationLayer.Authentication;

public sealed partial class ExternalProviderOptions
{
    /// <summary>
    ///     SAML 2.0 federation providers keyed by deployment-assigned slug
    ///     (<c>Authentication:ExternalProviders:Saml:&lt;slug&gt;</c>). Every entry defaults to
    ///     disabled; unknown slugs fail closed at validation time.
    /// </summary>
    public Dictionary<string, SamlProviderOptions> Saml { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Validates a federation provider slug (shared by the OIDC and SAML registries):
    ///     lowercase ASCII letters, digits, and hyphens, starting and ending with a letter
    ///     or digit (URL-path safe, max 64 characters).
    /// </summary>
    public static bool IsValidFederationSlug(string? slug) =>
        !string.IsNullOrEmpty(slug) && OidcSlugPattern.IsMatch(slug);
}

/// <summary>
///     Configuration for one SAML 2.0 service-provider federation (enterprise identity
///     providers). Disabled by default; only enabled entries are validated and reachable.
///     The identity provider is described statically by configuration (entity ID, signing
///     certificates, endpoint URLs) — no IdP metadata is fetched over the network, so the
///     trust base is exactly what the deployment supplies.
/// </summary>
public sealed partial class SamlProviderOptions
{
    /// <summary>
    ///     SAML assertion AuthnContext class references that count as multi-factor
    ///     authentication for the fail-closed platform MFA gate.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultMfaAuthnContextClassRefs =
    [
        "urn:oasis:names:tc:SAML:2.0:ac:classes:TimeSyncToken",
        "urn:oasis:names:tc:SAML:2.0:ac:classes:SmartcardPKI",
        "urn:oasis:names:tc:SAML:2.0:ac:classes:Smartcard",
        "urn:oasis:names:tc:SAML:2.0:ac:classes:PersonalSmartcardPKI",
        "urn:oasis:names:tc:SAML:2.0:ac:classes:MobileTwoFactorUnregistered",
        "urn:oasis:names:tc:SAML:2.0:ac:classes:MobileTwoFactorContract"
    ];

    /// <summary>
    ///     Maximum allowed clock skew when validating assertion lifetimes (10 minutes).
    /// </summary>
    public const int MaxClockSkewSeconds = 600;

    /// <summary>
    ///     Whether this federation provider is enabled for sign-in. Fail closed by default.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Optional display name surfaced to users (defaults to the slug).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    ///     This service provider's entity ID (the <c>Issuer</c> sent in AuthnRequests and
    ///     the expected <c>Audience</c> in assertions). A URI or URN, max 1024 characters.
    /// </summary>
    public string? SpEntityId { get; set; }

    /// <summary>
    ///     This service provider's Assertion Consumer Service URL (where the identity
    ///     provider posts <c>SAMLResponse</c>). Must be an absolute HTTPS URL; the response
    ///     <c>Destination</c> and the subject-confirmation <c>Recipient</c> must equal it.
    /// </summary>
    public string? AssertionConsumerServiceUrl { get; set; }

    /// <summary>
    ///     The identity provider's entity ID — the exact <c>Issuer</c> value accepted in
    ///     responses. A URI or URN, max 1024 characters.
    /// </summary>
    public string? IdpEntityId { get; set; }

    /// <summary>
    ///     Base64 (DER) encoded X.509 certificates used to verify response/assertion
    ///     signatures. More than one entry supports rotation overlap; every signature must
    ///     verify against at least one currently-valid certificate. Assertions signed by an
    ///     unknown or expired certificate are rejected — unsigned responses fail closed.
    /// </summary>
    public List<string> IdpSigningCertificates { get; set; } = [];

    /// <summary>
    ///     The identity provider's single sign-on service URL the browser is redirected to
    ///     (HTTP-Redirect binding) when sign-in is initiated here. Absolute HTTPS URL.
    /// </summary>
    public string? IdpSignInServiceUrl { get; set; }

    /// <summary>
    ///     The identity provider's optional single logout service URL (HTTP-Redirect
    ///     binding) used for front-channel logout forwarding. Null keeps logout local.
    /// </summary>
    public string? IdpSingleLogoutServiceUrl { get; set; }

    /// <summary>
    ///     Optional required <c>NameID/Format</c> (for example
    ///     <c>urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress</c>). When set, the
    ///     assertion NameID must carry exactly this format.
    /// </summary>
    public string? NameIdFormat { get; set; }

    /// <summary>
    ///     Maps logical claim names (<c>email</c>/<c>name</c>) onto the provider's SAML
    ///     attribute names. The subject is always the assertion NameID (not mappable).
    ///     Unmapped logical claims fall back to their own name.
    /// </summary>
    public Dictionary<string, string> ClaimMapping { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     When true, emails asserted by this provider count as verified for the account
    ///     auto-link policy (an enterprise trust decision). Default false: SAML assertions
    ///     carry no standardized email-verification proof, so untrusted emails never
    ///     link to a pre-existing account by address alone.
    /// </summary>
    public bool TrustIdpEmailVerification { get; set; }

    /// <summary>
    ///     Optional <c>AuthnContextClassRef</c> requested in AuthnRequests.
    /// </summary>
    public string? AuthnContextClassRef { get; set; }

    /// <summary>
    ///     <c>AuthnContextClassRef</c> values accepted as proof of multi-factor
    ///     authentication for the fail-closed MFA gate. Defaults to the standard
    ///     smartcard / one-time-password context classes.
    /// </summary>
    public List<string> MfaAuthnContextClassRefs { get; set; } = [.. DefaultMfaAuthnContextClassRefs];

    /// <summary>
    ///     Allowed clock skew (seconds) when validating NotBefore/NotOnOrAfter windows and
    ///     certificate validity. Default 90; range 0–600.
    /// </summary>
    public int AllowedClockSkewSeconds { get; set; } = 90;

    /// <summary>
    ///     Whether responses without an <c>InResponseTo</c> correlation (IdP-initiated SSO)
    ///     are accepted. Default false: every response must correlate with an AuthnRequest
    ///     issued by this service.
    /// </summary>
    public bool AllowIdpInitiated { get; set; }

    /// <summary>
    ///     Email domains served by this provider. When non-empty, sign-in is accepted only
    ///     for identities whose email domain is listed.
    /// </summary>
    public List<string> EmailDomains { get; set; } = [];

    /// <summary>
    ///     Tenant isolation: when non-empty, sign-ins through this provider may only land
    ///     on the tenants in this allowlist (requested tenant and resolved default).
    /// </summary>
    public List<Guid> TenantIds { get; set; } = [];

    public void Validate(string slug)
    {
        if (!ExternalProviderOptions.IsValidFederationSlug(slug))
        {
            throw new InvalidOperationException($"SAML provider slug '{slug}' must be lowercase letters, digits, and hyphens (max 64 characters).");
        }

        if (!Enabled)
        {
            return;
        }

        ValidateEntityId(SpEntityId, "service provider entity ID", slug);
        ValidateEntityId(IdpEntityId, "identity provider entity ID", slug);

        ValidateHttpsUrl(AssertionConsumerServiceUrl, "assertion consumer service URL", allowQuery: false);
        ValidateHttpsUrl(IdpSignInServiceUrl, "identity provider sign-in service URL", allowQuery: false);
        ValidateHttpsUrl(IdpSingleLogoutServiceUrl, "identity provider single logout service URL", allowQuery: false);

        if (IdpSigningCertificates is null)
        {
            throw new InvalidOperationException($"SAML signing certificates for provider '{slug}' cannot be null.");
        }

        if (IdpSigningCertificates.Count == 0)
        {
            throw new InvalidOperationException($"SAML provider '{slug}' requires at least one identity provider signing certificate.");
        }

        var thumbprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;
        var anyCurrentlyValid = false;
        foreach (var certificate in IdpSigningCertificates)
        {
            if (string.IsNullOrWhiteSpace(certificate) || certificate.Any(char.IsControl))
            {
                throw new InvalidOperationException($"SAML signing certificates for provider '{slug}' must be non-empty Base64 DER values.");
            }

            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(certificate);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException($"SAML signing certificates for provider '{slug}' must be Base64 DER encoded.");
            }

            X509Certificate2 parsed;
            try
            {
                parsed = X509CertificateLoader.LoadCertificate(decoded);
            }
            catch (CryptographicException)
            {
                throw new InvalidOperationException($"SAML signing certificates for provider '{slug}' must decode to valid X.509 certificates.");
            }

            if (!thumbprints.Add(parsed.Thumbprint))
            {
                throw new InvalidOperationException($"SAML signing certificates for provider '{slug}' cannot contain duplicates.");
            }

            if (parsed.NotBefore > now.AddMinutes(5))
            {
                throw new InvalidOperationException($"SAML signing certificate for provider '{slug}' is not valid yet (NotBefore {parsed.NotBefore:u}).");
            }

            if (parsed.NotAfter > now)
            {
                anyCurrentlyValid = true;
            }
        }

        if (!anyCurrentlyValid)
        {
            // Fail closed at startup: no configured certificate could verify a new
            // assertion, so the provider could never authenticate anyone.
            throw new InvalidOperationException($"All SAML signing certificates for provider '{slug}' have expired.");
        }

        if (!string.IsNullOrWhiteSpace(NameIdFormat) &&
            (NameIdFormat.Any(char.IsControl) || NameIdFormat.Length > 256))
        {
            throw new InvalidOperationException($"SAML NameID format for provider '{slug}' must be a non-empty format URN of at most 256 characters.");
        }

        if (ClaimMapping is null)
        {
            throw new InvalidOperationException($"SAML claim mapping for provider '{slug}' cannot be null.");
        }

        foreach (var (logicalClaim, providerClaim) in ClaimMapping)
        {
            if (!SupportedClaimMappingKeys.Contains(logicalClaim))
            {
                throw new InvalidOperationException($"SAML claim mapping key '{logicalClaim}' for provider '{slug}' is not a supported logical claim.");
            }

            if (string.IsNullOrWhiteSpace(providerClaim) || providerClaim.Any(char.IsControl))
            {
                throw new InvalidOperationException($"SAML claim mapping for '{logicalClaim}' of provider '{slug}' must map to a non-empty attribute name.");
            }
        }

        if (!string.IsNullOrWhiteSpace(AuthnContextClassRef) &&
            (AuthnContextClassRef.Any(char.IsControl) || AuthnContextClassRef.Length > 256))
        {
            throw new InvalidOperationException($"SAML AuthnContextClassRef for provider '{slug}' must be a non-empty context URN of at most 256 characters.");
        }

        if (MfaAuthnContextClassRefs is null)
        {
            throw new InvalidOperationException($"SAML MFA AuthnContext class references for provider '{slug}' cannot be null.");
        }

        var uniqueMfaContexts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var contextRef in MfaAuthnContextClassRefs)
        {
            if (string.IsNullOrWhiteSpace(contextRef) || contextRef.Any(char.IsControl) || !uniqueMfaContexts.Add(contextRef))
            {
                throw new InvalidOperationException($"SAML MFA AuthnContext class references for provider '{slug}' must be unique non-empty URNs.");
            }
        }

        if (AllowedClockSkewSeconds is < 0 or > MaxClockSkewSeconds)
        {
            throw new InvalidOperationException($"SAML allowed clock skew for provider '{slug}' must be between 0 and {MaxClockSkewSeconds} seconds.");
        }

        if (EmailDomains is null)
        {
            throw new InvalidOperationException($"SAML email domains for provider '{slug}' cannot be null.");
        }

        var uniqueDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var domain in EmailDomains)
        {
            if (string.IsNullOrWhiteSpace(domain) ||
                domain.StartsWith('@') ||
                !MailAddressDomainPattern().IsMatch(domain) ||
                !uniqueDomains.Add(domain))
            {
                throw new InvalidOperationException($"SAML email domains for provider '{slug}' must be unique DNS hostnames without '@'.");
            }
        }

        if (TenantIds is null)
        {
            throw new InvalidOperationException($"SAML tenant allowlist for provider '{slug}' cannot be null.");
        }

        var uniqueTenants = new HashSet<Guid>();
        foreach (var tenantId in TenantIds)
        {
            if (tenantId == Guid.Empty || !uniqueTenants.Add(tenantId))
            {
                throw new InvalidOperationException($"SAML tenant allowlist for provider '{slug}' must contain unique non-empty tenant IDs.");
            }
        }
    }

    /// <summary>
    ///     Logical claim names that may be remapped through <see cref="ClaimMapping" />.
    ///     The subject is always the assertion NameID and cannot be remapped.
    /// </summary>
    public static readonly IReadOnlySet<string> SupportedClaimMappingKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "email", "name" };

    private static void ValidateEntityId(string? entityId, string role, string slug)
    {
        if (string.IsNullOrWhiteSpace(entityId) || entityId.Any(char.IsControl) || entityId.Length > 1024)
        {
            throw new InvalidOperationException($"SAML {role} for provider '{slug}' is required (max 1024 characters).");
        }
    }

    private static void ValidateHttpsUrl(string? url, string role, bool allowQuery)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException($"SAML {role} is required for an enabled provider.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            (!allowQuery && !string.IsNullOrEmpty(uri.Query)) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException($"SAML {role} must be an absolute HTTPS URL without credentials, query, or fragment.");
        }
    }

    [GeneratedRegex(@"^(?=.{1,253}$)[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MailAddressDomainPattern();
}
