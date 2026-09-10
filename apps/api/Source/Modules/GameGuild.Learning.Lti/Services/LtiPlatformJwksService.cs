using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace GameGuild.Learning.Lti;

/// <summary>
/// Fetches platform JWKS (cached 15 minutes) and validates launch id_tokens:
/// RS256 signature against the platform keys, plus iss/aud/lifetime against the deployment.
/// </summary>
public sealed class LtiPlatformJwksService(
    IHttpClientFactory httpClientFactory,
    ILogger<LtiPlatformJwksService> logger)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, (DateTimeOffset ExpiresAt, List<SecurityKey> Keys)> _cache = new();

    public async Task<ClaimsPrincipal?> ValidateIdTokenAsync(string idToken, LtiDeployment deployment, CancellationToken cancellationToken = default)
    {
        List<SecurityKey> keys;
        try
        {
            keys = await GetSigningKeysAsync(deployment.PlatformJwksUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LTI: failed to fetch platform JWKS from {JwksUrl}", deployment.PlatformJwksUrl);
            return null;
        }

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        if (!handler.CanReadToken(idToken))
        {
            return null;
        }

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = deployment.Issuer,
            ValidAudiences = [deployment.ClientId],
            IssuerSigningKeys = keys.Count == 1 ? keys : null,
            IssuerSigningKeyResolver = keys.Count == 1
                ? null
                : (_, _, kid, _) => keys.Where(k => k.KeyId == kid),
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(60),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        };

        try
        {
            return handler.ValidateToken(idToken, parameters, out _);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            logger.LogWarning("LTI: id_token validation failed for deployment {DeploymentId}: {Message}", deployment.Id, ex.Message);
            return null;
        }
    }

    private async Task<List<SecurityKey>> GetSigningKeysAsync(string jwksUrl, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(jwksUrl, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Keys;
        }

        using var client = httpClientFactory.CreateClient(LtiModule.HttpClientName);
        using var response = await client.GetAsync(jwksUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var keys = ParseKeys(json);
        if (keys.Count == 0)
        {
            throw new InvalidOperationException("Platform JWKS contained no usable RSA keys.");
        }

        _cache[jwksUrl] = (DateTimeOffset.UtcNow.Add(CacheDuration), keys);
        return keys;
    }

    private static List<SecurityKey> ParseKeys(string json)
    {
        var keys = new List<SecurityKey>();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("keys", out var keyArray))
        {
            return keys;
        }

        foreach (var key in keyArray.EnumerateArray())
        {
            if (!string.Equals(key.GetPropertyString("kty") ?? "", "RSA", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var kid = key.GetPropertyString("kid");
            var modulus = key.GetPropertyString("n");
            var exponent = key.GetPropertyString("e");
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
}

file static class JsonElementExtensions
{
    public static string? GetPropertyString(this System.Text.Json.JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;
}
