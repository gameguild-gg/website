using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.Configuration.PresentationLayer.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Generic OpenID Connect federation for enterprise identity providers.
///     Discovers <c>{authority}/.well-known/openid-configuration</c> (cached 15 minutes),
///     drives the authorization-code flow against the discovered endpoints, and validates the
///     returned ID token cryptographically (RS256 via the provider JWKS, also cached) with
///     issuer/audience/lifetime checks — the same pattern as the in-repo LTI JWKS service.
///     Fails closed for unknown or disabled provider slugs.
/// </summary>
public sealed class OidcFederationService(
    IHttpClientFactory httpClientFactory,
    AuthenticationOptions? authenticationOptions = null,
    ILogger<OidcFederationService>? logger = null
) : IOidcFederationService
{
    public const string HttpClientName = "oidc-federation";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, (DateTimeOffset ExpiresAt, OidcProviderMetadata Metadata)> _metadataCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, (DateTimeOffset ExpiresAt, List<SecurityKey> Keys)> _jwksCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly JwtSecurityTokenHandler _tokenHandler = new() { MapInboundClaims = false };

    public async Task<OidcSignInChallenge> BuildAuthorizationUrlAsync(string slug, string redirectUri, string state, CancellationToken cancellationToken = default)
    {
        var options = RequireEnabledProvider(slug);
        var metadata = await DiscoverAsync(options, cancellationToken).ConfigureAwait(false);

        var scope = string.Join(" ", options.Scopes);
        var authUrl = $"{metadata.AuthorizationEndpoint}" +
                      $"?client_id={Uri.EscapeDataString(options.ClientId!)}" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      $"&response_type=code" +
                      $"&scope={Uri.EscapeDataString(scope)}" +
                      $"&state={Uri.EscapeDataString(state)}";

        return new OidcSignInChallenge { AuthUrl = authUrl, State = state };
    }

    public async Task<OidcFederatedIdentity> AuthenticateCallbackAsync(string slug, string code, string state, string redirectUri, CancellationToken cancellationToken = default)
    {
        var options = RequireEnabledProvider(slug);

        if (string.IsNullOrEmpty(state))
        {
            logger?.LogWarning("OIDC callback received without state parameter — potential CSRF attack for provider {Slug}", slug);
            throw new InvalidOperationException("Missing OIDC state parameter. Request may have been tampered with.");
        }

        var metadata = await DiscoverAsync(options, cancellationToken).ConfigureAwait(false);
        var idToken = await ExchangeCodeForIdTokenAsync(options, metadata, code, redirectUri, cancellationToken).ConfigureAwait(false);
        var principal = await ValidateIdTokenAsync(options, metadata, slug, idToken, cancellationToken).ConfigureAwait(false);

        var providerKey = ReadMappedClaim(options, principal, "sub")
            ?? throw new UnauthorizedAccessException($"OIDC ID token for provider '{slug}' did not include a subject claim.");

        var emailVerifiedClaim = ReadMappedClaim(options, principal, "email_verified");
        var identity = new OidcFederatedIdentity
        {
            Slug = slug,
            ProviderKey = providerKey,
            Email = ReadMappedClaim(options, principal, "email"),
            EmailVerified = bool.TryParse(emailVerifiedClaim, out var emailVerified) && emailVerified,
            Name = ReadMappedClaim(options, principal, "name"),
            Amr = principal.FindAll("amr").Select(claim => claim.Value).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray(),
            Acr = principal.FindFirst("acr")?.Value,
            GrantedScopes = options.Scopes.ToArray()
        };

        EnforceEmailDomainGate(options, identity);

        logger?.LogInformation("OIDC federation sign-in validated for provider {Slug}", slug);
        return identity;
    }

    /// <summary>
    ///     When the provider restricts sign-in to configured email domains, an identity whose
    ///     email is outside that allowlist is rejected — fail closed.
    /// </summary>
    private static void EnforceEmailDomainGate(OidcProviderOptions options, OidcFederatedIdentity identity)
    {
        if (options.EmailDomains.Count == 0 || string.IsNullOrWhiteSpace(identity.Email))
        {
            return;
        }

        var atIndex = identity.Email.LastIndexOf('@');
        var domain = atIndex >= 0 && atIndex < identity.Email.Length - 1 ? identity.Email[(atIndex + 1)..] : string.Empty;
        if (options.EmailDomains.All(allowed =>
                !allowed.Trim().TrimStart('@').Equals(domain, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnauthorizedAccessException($"Email domain '{domain}' is not served by OIDC provider '{identity.Slug}'.");
        }
    }

    public async Task<string?> GetEndSessionEndpointAsync(string slug, CancellationToken cancellationToken = default)
    {
        var options = RequireEnabledProvider(slug);
        var metadata = await DiscoverAsync(options, cancellationToken).ConfigureAwait(false);
        return metadata.EndSessionEndpoint;
    }

    public IReadOnlyList<OidcDiscoveredProvider> FindProvidersForEmailDomain(string emailDomain)
    {
        if (string.IsNullOrWhiteSpace(emailDomain))
        {
            return [];
        }

        var normalized = emailDomain.Trim().TrimStart('@').ToLowerInvariant();
        return EnabledProviders()
            .Where(entry => entry.Options.EmailDomains.Any(domain => domain.Trim().TrimStart('@').Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            .Select(entry => new OidcDiscoveredProvider
            {
                Slug = entry.Slug,
                DisplayName = string.IsNullOrWhiteSpace(entry.Options.DisplayName) ? entry.Slug : entry.Options.DisplayName!
            })
           .OrderBy(provider => provider.Slug, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IEnumerable<(string Slug, OidcProviderOptions Options)> EnabledProviders()
    {
        var providers = authenticationOptions?.ExternalProviders?.Oidc;
        if (providers is null)
        {
            return [];
        }

        return providers.Where(entry => entry.Value?.Enabled == true).Select(entry => (entry.Key, entry.Value));
    }

    private OidcProviderOptions RequireEnabledProvider(string slug)
    {
        var providers = authenticationOptions?.ExternalProviders?.Oidc;
        var options = providers?.FirstOrDefault(entry => entry.Key.Equals(slug, StringComparison.OrdinalIgnoreCase)).Value;

        if (options is null)
        {
            throw new InvalidOperationException($"OIDC federation provider '{slug}' is not configured.");
        }

        if (!options.Enabled)
        {
            throw new InvalidOperationException($"OIDC federation provider '{slug}' is disabled.");
        }

        return options;
    }

    private async Task<OidcProviderMetadata> DiscoverAsync(OidcProviderOptions options, CancellationToken cancellationToken)
    {
        var authority = options.Authority!.TrimEnd('/');
        var cacheKey = authority;
        if (_metadataCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Metadata;
        }

        var discoveryUrl = $"{authority}/.well-known/openid-configuration";
        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(discoveryUrl, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OIDC discovery failed for authority '{authority}' with HTTP {(int)response.StatusCode}.");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var metadata = OidcProviderMetadata.Parse(json, authority);

        _metadataCache[cacheKey] = (DateTimeOffset.UtcNow.Add(CacheDuration), metadata);
        return metadata;
    }

    private async Task<string> ExchangeCodeForIdTokenAsync(
        OidcProviderOptions options,
        OidcProviderMetadata metadata,
        string code,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        var tokenRequest = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = options.ClientId!,
            ["client_secret"] = options.ClientSecret!
        };

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var content = new FormUrlEncodedContent(tokenRequest);
        using var response = await client.PostAsync(metadata.TokenEndpoint, content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OIDC token exchange failed for provider '{options.Authority}' with HTTP {(int)response.StatusCode}.");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("id_token", out var idTokenElement) ||
            idTokenElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrEmpty(idTokenElement.GetString()))
        {
            throw new UnauthorizedAccessException($"OIDC token response for provider '{options.Authority}' did not include an id_token.");
        }

        return idTokenElement.GetString()!;
    }

    private async Task<ClaimsPrincipal> ValidateIdTokenAsync(OidcProviderOptions options, OidcProviderMetadata metadata, string slug, string idToken, CancellationToken cancellationToken)
    {
        // Structural rejection happens before any JWKS round-trip: a token that cannot be
        // parsed at all is malformed no matter which key would have signed it, so there is
        // no reason to touch the provider's key set (or trust its availability) first.
        if (!_tokenHandler.CanReadToken(idToken))
        {
            throw new UnauthorizedAccessException($"OIDC ID token for provider '{slug}' is malformed.");
        }

        try
        {
            _ = _tokenHandler.ReadToken(idToken);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or System.Text.Json.JsonException)
        {
            logger?.LogWarning(ex, "OIDC: id_token for provider {Slug} could not be parsed", slug);
            throw new UnauthorizedAccessException($"OIDC ID token for provider '{slug}' is malformed.");
        }

        List<SecurityKey> keys;
        try
        {
            keys = await GetSigningKeysAsync(metadata.JwksUri, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "OIDC: failed to fetch provider JWKS from {JwksUri}", metadata.JwksUri);
            throw new UnauthorizedAccessException($"OIDC signing keys for provider '{slug}' are unavailable.");
        }

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = metadata.Issuer,
            ValidAudiences = [options.ClientId!],
            IssuerSigningKeys = keys.Count == 1 ? keys : null,
            IssuerSigningKeyResolver = keys.Count == 1
                ? null
                : (_, _, kid, _) => keys.Where(key => key.KeyId == kid),
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(60),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        };

        try
        {
            return _tokenHandler.ValidateToken(idToken, parameters, out _);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            logger?.LogWarning(ex, "OIDC: id_token validation failed for provider {Slug}", slug);
            throw new UnauthorizedAccessException($"OIDC ID token for provider '{slug}' failed validation.");
        }
    }

    private async Task<List<SecurityKey>> GetSigningKeysAsync(string jwksUrl, CancellationToken cancellationToken)
    {
        if (_jwksCache.TryGetValue(jwksUrl, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Keys;
        }

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(jwksUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var keys = ParseKeys(json);
        if (keys.Count == 0)
        {
            throw new InvalidOperationException("Provider JWKS contained no usable RSA keys.");
        }

        _jwksCache[jwksUrl] = (DateTimeOffset.UtcNow.Add(CacheDuration), keys);
        return keys;
    }

    private static List<SecurityKey> ParseKeys(string json)
    {
        var keys = new List<SecurityKey>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("keys", out var keyArray))
        {
            return keys;
        }

        foreach (var key in keyArray.EnumerateArray())
        {
            if (!string.Equals(GetPropertyString(key, "kty") ?? string.Empty, "RSA", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var kid = GetPropertyString(key, "kid");
            var modulus = GetPropertyString(key, "n");
            var exponent = GetPropertyString(key, "e");
            if (kid is null || modulus is null || exponent is null)
            {
                continue;
            }

            var rsa = RSA.Create(new RSAParameters
            {
                Modulus = Base64UrlEncoder.DecodeBytes(modulus),
                Exponent = Base64UrlEncoder.DecodeBytes(exponent)
            });
            keys.Add(new RsaSecurityKey(rsa) { KeyId = kid });
        }

        return keys;
    }

    private static string? GetPropertyString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadMappedClaim(OidcProviderOptions options, ClaimsPrincipal principal, string logicalName)
    {
        var claimType = options.ClaimMapping.TryGetValue(logicalName, out var mapped) ? mapped : logicalName;
        return principal.FindFirst(claimType)?.Value;
    }

    /// <summary>
    ///     Parsed OpenID Connect discovery document. The issuer must equal the configured
    ///     authority and all required endpoints must be absolute HTTPS URLs.
    /// </summary>
    private sealed record OidcProviderMetadata
    {
        public required string Issuer { get; init; }

        public required string AuthorizationEndpoint { get; init; }

        public required string TokenEndpoint { get; init; }

        public required string JwksUri { get; init; }

        public string? EndSessionEndpoint { get; init; }

        public static OidcProviderMetadata Parse(string json, string authority)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var issuer = GetPropertyString(root, "issuer");
            var authorizationEndpoint = GetPropertyString(root, "authorization_endpoint");
            var tokenEndpoint = GetPropertyString(root, "token_endpoint");
            var jwksUri = GetPropertyString(root, "jwks_uri");
            var endSessionEndpoint = GetPropertyString(root, "end_session_endpoint");

            if (string.IsNullOrEmpty(issuer) || !issuer.TrimEnd('/').Equals(authority, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"OIDC discovery document issuer '{issuer}' does not match the configured authority '{authority}'.");
            }

            foreach (var (name, value) in new[]
                     {
                         ("authorization_endpoint", authorizationEndpoint),
                         ("token_endpoint", tokenEndpoint),
                         ("jwks_uri", jwksUri)
                     })
            {
                if (string.IsNullOrWhiteSpace(value) ||
                    !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                    uri.Scheme != Uri.UriSchemeHttps ||
                    !string.IsNullOrEmpty(uri.UserInfo))
                {
                    throw new InvalidOperationException($"OIDC discovery document '{name}' must be an absolute HTTPS URL without embedded credentials.");
                }
            }

            if (!string.IsNullOrWhiteSpace(endSessionEndpoint) &&
                (!Uri.TryCreate(endSessionEndpoint, UriKind.Absolute, out var endSessionUri) ||
                 endSessionUri.Scheme != Uri.UriSchemeHttps ||
                 !string.IsNullOrEmpty(endSessionUri.UserInfo)))
            {
                throw new InvalidOperationException("OIDC discovery document 'end_session_endpoint' must be an absolute HTTPS URL without embedded credentials.");
            }

            return new OidcProviderMetadata
            {
                Issuer = issuer!,
                AuthorizationEndpoint = authorizationEndpoint!,
                TokenEndpoint = tokenEndpoint!,
                JwksUri = jwksUri!,
                EndSessionEndpoint = string.IsNullOrWhiteSpace(endSessionEndpoint) ? null : endSessionEndpoint
            };
        }
    }
}
