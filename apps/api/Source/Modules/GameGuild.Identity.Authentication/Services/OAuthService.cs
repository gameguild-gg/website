using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GameGuild.Configuration.PresentationLayer.Authentication;
using AuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Service for OAuth authentication with various providers
/// </summary>
public class OAuthService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<OAuthService> logger,
    AuthenticationOptions? authenticationOptions = null
) : IOAuthService
{
    // OAuth endpoint constants
    private const string GitHubAuthUrl = "https://github.com/login/oauth/authorize";

    private const string GitHubTokenUrl = "https://github.com/login/oauth/access_token";

    private const string GitHubUserUrl = "https://api.github.com/user";

    private const string GitHubEmailUrl = "https://api.github.com/user/emails";

    private const string GoogleAuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";

    private const string GoogleTokenUrl = "https://oauth2.googleapis.com/token";

    private const string GoogleUserUrl = "https://www.googleapis.com/oauth2/v2/userinfo";

    private const string DiscordAuthUrl = "https://discord.com/oauth2/authorize";

    private const string DiscordTokenUrl = "https://discord.com/api/oauth2/token";

    private const string DiscordUserUrl = "https://discord.com/api/v10/users/@me";

    private const string MicrosoftUserInfoUrl = "https://graph.microsoft.com/oidc/userinfo";

    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };

    /// <summary>
    ///     Safe application defaults per provider, used when neither the caller nor the
    ///     typed provider configuration supplies scopes. These are the exact scope tokens
    ///     the authorization URL builders used to inline — single source of truth for both
    ///     URL construction and the granted-scope record persisted at callback/link time.
    /// </summary>
    private static readonly Dictionary<string, string[]> ProviderDefaultScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["github"] = ["read:user", "user:email"],
        ["google"] = ["openid", "email", "profile"],
        ["discord"] = ["identify", "email"],
        ["microsoft"] = ["openid", "email", "profile"]
    };

    public Task<string> GetAuthorizationUrlAsync(string provider, string redirectUri, string state, string[]? scopes = null)
    {
        EnsureProviderEnabled(provider);
        var clientId = GetClientId(provider);

        if (string.IsNullOrEmpty(clientId)) { throw new InvalidOperationException($"OAuth client ID not configured for provider: {provider}"); }

        var configuredScopes = ResolveAuthorizationScopes(provider, scopes);

        var url = provider.ToLower(CultureInfo.InvariantCulture) switch
        {
            "github" => BuildGitHubAuthUrl(clientId, redirectUri, state, configuredScopes),
            "google" => BuildGoogleAuthUrl(clientId, redirectUri, state, configuredScopes),
            "discord" => BuildDiscordAuthUrl(clientId, redirectUri, state, configuredScopes),
            "microsoft" => BuildMicrosoftAuthUrl(clientId, redirectUri, state, configuredScopes),
            _ => throw new NotSupportedException($"OAuth provider not supported: {provider}")
        };

        return Task.FromResult(url);
    }

    /// <inheritdoc />
    public string[] ResolveAuthorizationScopes(string provider, string[]? requestedScopes = null)
    {
        var key = provider.ToLower(CultureInfo.InvariantCulture);
        if (!ProviderDefaultScopes.ContainsKey(key))
        {
            throw new NotSupportedException($"OAuth provider not supported: {provider}");
        }

        if (requestedScopes is { Length: > 0 })
        {
            return NormalizeScopes(key, requestedScopes);
        }

        var configuredScopes = FindProviderSettings(provider)?.Scopes;
        return configuredScopes is { Count: > 0 }
            ? NormalizeScopes(key, configuredScopes.ToArray())
            : [.. ProviderDefaultScopes[key]];
    }

    /// <summary>
    ///     Deduplicates scope tokens preserving first-seen order and — for Microsoft —
    ///     unions the OIDC scopes the identity platform requires on every request
    ///     (mirrors the previous inline behavior of <c>BuildMicrosoftAuthUrl</c>).
    /// </summary>
    private static string[] NormalizeScopes(string providerKey, string[] scopes)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = scopes.Where(scope => seen.Add(scope)).ToList();

        if (providerKey.Equals("microsoft", StringComparison.Ordinal))
        {
            foreach (var requiredScope in new[] { "openid", "email", "profile" })
            {
                if (!seen.Contains(requiredScope))
                {
                    seen.Add(requiredScope);
                    normalized.Add(requiredScope);
                }
            }
        }

        return [.. normalized];
    }

    public async Task<OAuthUserProfile> HandleCallbackAsync(string provider, string code, string state, string redirectUri)
    {
        EnsureProviderEnabled(provider);

        // Validate state parameter for CSRF protection
        if (string.IsNullOrEmpty(state))
        {
            logger.LogWarning("OAuth callback received without state parameter — potential CSRF attack for provider {Provider}", provider);
            throw new InvalidOperationException("Missing OAuth state parameter. Request may have been tampered with.");
        }

        var accessToken = await ExchangeCodeForTokenAsync(provider, code, redirectUri).ConfigureAwait(false);

        return await GetUserProfileAsync(provider, accessToken).ConfigureAwait(false);
    }

    public async Task<OAuthUserProfile> GetUserProfileAsync(string provider, string accessToken)
    {
        EnsureProviderEnabled(provider);

        return provider.ToLower(CultureInfo.InvariantCulture) switch
        {
            "github" => await GetGitHubUserProfileAsync(accessToken).ConfigureAwait(false),
            "google" => await GetGoogleUserProfileAsync(accessToken).ConfigureAwait(false),
            "discord" => await GetDiscordUserProfileAsync(accessToken).ConfigureAwait(false),
            "microsoft" => await GetMicrosoftUserProfileAsync(accessToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Provider not supported: {provider}")
        };
    }

    public async Task<bool> RevokeTokenAsync(string provider, string token)
    {
        try
        {
            // Implementation varies by provider
            // For now, return true as most providers don't require explicit revocation
            logger.LogInformation("Token revocation requested for provider {Provider}", provider);
            await Task.CompletedTask.ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to revoke token for provider {Provider}", provider);

            return false;
        }
    }

    #region Private Helpers

    private async Task<string> ExchangeCodeForTokenAsync(string provider, string code, string redirectUri)
    {
        return provider.ToLower(CultureInfo.InvariantCulture) switch
        {
            "github" => await ExchangeGitHubCodeAsync(code, redirectUri).ConfigureAwait(false),
            "google" => await ExchangeGoogleCodeAsync(code, redirectUri).ConfigureAwait(false),
            "discord" => await ExchangeDiscordCodeAsync(code, redirectUri).ConfigureAwait(false),
            "microsoft" => await ExchangeMicrosoftCodeAsync(code, redirectUri).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Provider not supported: {provider}")
        };
    }

    private OAuthProviderOptions? FindProviderSettings(string provider)
    {
        var providers = authenticationOptions?.ExternalProviders?.Providers;
        if (providers is null)
        {
            return null;
        }

        return providers.FirstOrDefault(entry => entry.Key.Equals(provider, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private void EnsureProviderEnabled(string provider)
    {
        var settings = FindProviderSettings(provider);
        if (settings is not null && !settings.Enabled)
        {
            throw new InvalidOperationException($"OAuth provider '{provider}' is disabled.");
        }
    }

    private string? GetClientId(string provider) =>
        FindProviderSettings(provider)?.ClientId ?? GetLegacyProviderSetting(provider, "ClientId");

    private string? GetClientSecret(string provider) =>
        FindProviderSettings(provider)?.ClientSecret ?? GetLegacyProviderSetting(provider, "ClientSecret");

    private string? GetLegacyProviderSetting(string provider, string settingName)
    {
        var legacyKey = $"OAuth:{provider}:{settingName}";
        var value = configuration[legacyKey];
        if (value is not null)
        {
            return value;
        }

        var canonicalProvider = provider.ToLowerInvariant() switch
        {
            "discord" => "Discord",
            "github" => "GitHub",
            "google" => "Google",
            "microsoft" => "Microsoft",
            _ => provider
        };

        return configuration[$"OAuth:{canonicalProvider}:{settingName}"];
    }

    private string GetEndpoint(string provider, string endpointName, string fallback)
    {
        var settings = FindProviderSettings(provider);
        return endpointName switch
        {
            "authorization" => settings?.AuthorizationEndpoint ?? fallback,
            "token" => settings?.TokenEndpoint ?? fallback,
            "user-information" => settings?.UserInformationEndpoint ?? fallback,
            _ => throw new ArgumentOutOfRangeException(nameof(endpointName), endpointName, "Unsupported OAuth endpoint type.")
        };
    }

    private async Task<HttpResponseMessage> SendBearerRequestAsync(string uri, string accessToken, bool isGitHub = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(uri));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (isGitHub)
        {
            request.Headers.UserAgent.ParseAdd("GameGuild");
        }

        return await httpClient.SendAsync(request).ConfigureAwait(false);
    }

    #endregion

    #region GitHub OAuth

    private string BuildGitHubAuthUrl(string clientId, string redirectUri, string state, string[] scopes)
    {
        var scopeString = scopes is { Length: > 0 } ? string.Join(" ", scopes) : string.Join(" ", ProviderDefaultScopes["github"]);

        return $"{GetEndpoint("github", "authorization", GitHubAuthUrl)}?client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&state={Uri.EscapeDataString(state)}" +
               $"&scope={Uri.EscapeDataString(scopeString)}";
    }

    private async Task<string> ExchangeGitHubCodeAsync(string code, string redirectUri)
    {
        var clientId = GetClientId("github");
        var clientSecret = GetClientSecret("github");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("OAuth client credentials are not configured for provider: github");
        }

        var tokenRequest = new { client_id = clientId, client_secret = clientSecret, code, redirect_uri = redirectUri };

        using var content = new StringContent(JsonSerializer.Serialize(tokenRequest), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(GetEndpoint("github", "token", GitHubTokenUrl))) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

        return tokenResponse.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("Failed to get access token from GitHub");
    }

    private async Task<OAuthUserProfile> GetGitHubUserProfileAsync(string accessToken)
    {
        using var response = await SendBearerRequestAsync(GetEndpoint("github", "user-information", GitHubUserUrl), accessToken, isGitHub: true).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var user = JsonSerializer.Deserialize<GitHubUserDto>(content, _jsonOptions) ?? throw new InvalidOperationException("Failed to parse GitHub user");

        // Get email if not public
        if (string.IsNullOrEmpty(user.Email))
        {
            var primaryEmail = await GetGitHubPrimaryEmailAsync(accessToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(primaryEmail)) { user.Email = primaryEmail; }
        }

        return new OAuthUserProfile
        {
            ProviderId = user.Id.ToString(CultureInfo.InvariantCulture),
            Provider = "GitHub",
            Email = user.Email,
            EmailVerified = !string.IsNullOrEmpty(user.Email), // GitHub doesn't provide verification status
            Name = user.Name,
            Username = user.Login,
            AvatarUrl = user.AvatarUrl,
            AccessToken = accessToken
        };
    }

    private async Task<string?> GetGitHubPrimaryEmailAsync(string accessToken)
    {
        try
        {
            var settings = FindProviderSettings("github");
            var emailEndpoint = settings?.UserEmailEndpoint;
            if (string.IsNullOrWhiteSpace(emailEndpoint))
            {
                // Never send a GitHub access token to api.github.com when a custom user endpoint is configured.
                if (!string.IsNullOrWhiteSpace(settings?.UserInformationEndpoint))
                {
                    return null;
                }

                emailEndpoint = GitHubEmailUrl;
            }

            // Ensure auth header is set with the correct access token
            using var emailResponse = await SendBearerRequestAsync(emailEndpoint, accessToken, isGitHub: true).ConfigureAwait(false);
            emailResponse.EnsureSuccessStatusCode();

            var emailContent = await emailResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var emails = JsonSerializer.Deserialize<JsonElement[]>(emailContent);

            if (emails != null)
            {
                var primaryEmail = emails.Where(e => e.GetProperty("primary").GetBoolean()).Select(e => e.GetProperty("email").GetString()).FirstOrDefault();

                return primaryEmail;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch GitHub user emails");
            throw;
        }

        return null;
    }

    #endregion

    #region Google OAuth

    private string BuildGoogleAuthUrl(string clientId, string redirectUri, string state, string[] scopes)
    {
        var scopeString = scopes is { Length: > 0 } ? string.Join(" ", scopes) : string.Join(" ", ProviderDefaultScopes["google"]);

        return $"{GetEndpoint("google", "authorization", GoogleAuthUrl)}?client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&state={Uri.EscapeDataString(state)}" +
               $"&scope={Uri.EscapeDataString(scopeString)}" +
               $"&response_type=code" +
               $"&access_type=offline";
    }

    private async Task<string> ExchangeGoogleCodeAsync(string code, string redirectUri)
    {
        var clientId = GetClientId("google");
        var clientSecret = GetClientSecret("google");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("OAuth client credentials are not configured for provider: google");
        }

        var tokenRequest = new Dictionary<string, string> { { "client_id", clientId }, { "client_secret", clientSecret }, { "code", code }, { "grant_type", "authorization_code" }, { "redirect_uri", redirectUri } };

        using var content = new FormUrlEncodedContent(tokenRequest);
        using var response = await httpClient.PostAsync(new Uri(GetEndpoint("google", "token", GoogleTokenUrl)), content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

        return tokenResponse.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("Failed to get access token from Google");
    }

    private async Task<OAuthUserProfile> GetGoogleUserProfileAsync(string accessToken)
    {
        using var response = await SendBearerRequestAsync(GetEndpoint("google", "user-information", GoogleUserUrl), accessToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var user = JsonSerializer.Deserialize<GoogleUserDto>(content, _jsonOptions) ?? throw new InvalidOperationException("Failed to parse Google user");

        return new OAuthUserProfile
        {
            ProviderId = user.Id,
            Provider = "Google",
            Email = user.Email,
            EmailVerified = user.EmailVerified,
            Name = user.Name,
            FirstName = user.GivenName,
            LastName = user.FamilyName,
            AvatarUrl = user.Picture,
            Locale = null, // GoogleUserDto doesn't include locale
            AccessToken = accessToken
        };
    }

    #endregion

    #region Discord OAuth

    private string BuildDiscordAuthUrl(string clientId, string redirectUri, string state, string[] scopes)
    {
        var scopeString = scopes is { Length: > 0 } ? string.Join(" ", scopes) : string.Join(" ", ProviderDefaultScopes["discord"]);

        return $"{GetEndpoint("discord", "authorization", DiscordAuthUrl)}?client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&state={Uri.EscapeDataString(state)}" +
               $"&scope={Uri.EscapeDataString(scopeString)}" +
               $"&response_type=code";
    }

    private async Task<string> ExchangeDiscordCodeAsync(string code, string redirectUri)
    {
        var clientId = GetClientId("discord");
        var clientSecret = GetClientSecret("discord");

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret)) { throw new InvalidOperationException("Discord OAuth client ID or client secret not configured"); }

        // Discord's token endpoint only accepts form-urlencoded bodies (client credentials as form fields)
        var tokenRequest = new Dictionary<string, string> { { "client_id", clientId }, { "client_secret", clientSecret }, { "grant_type", "authorization_code" }, { "code", code }, { "redirect_uri", redirectUri } };

        using var content = new FormUrlEncodedContent(tokenRequest);
        using var response = await httpClient.PostAsync(new Uri(GetEndpoint("discord", "token", DiscordTokenUrl)), content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

        return tokenResponse.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("Failed to get access token from Discord");
    }

    private async Task<OAuthUserProfile> GetDiscordUserProfileAsync(string accessToken)
    {
        using var response = await SendBearerRequestAsync(GetEndpoint("discord", "user-information", DiscordUserUrl), accessToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var user = JsonSerializer.Deserialize<DiscordUserDto>(content, _jsonOptions) ?? throw new InvalidOperationException("Failed to parse Discord user");

        string avatarUrl;
        if (string.IsNullOrEmpty(user.Avatar))
        {
            // Default avatar index derived from the snowflake's upper bits (Discord docs: (id >> 22) % 6)
            avatarUrl = $"https://cdn.discordapp.com/embed/avatars/{(long.Parse(user.Id, CultureInfo.InvariantCulture) >> 22) % 6}.png";
        }
        else
        {
            var extension = user.Avatar.StartsWith("a_", StringComparison.Ordinal) ? "gif" : "png";
            avatarUrl = $"https://cdn.discordapp.com/avatars/{user.Id}/{user.Avatar}.{extension}?size=256";
        }

        return new OAuthUserProfile
        {
            ProviderId = user.Id,
            Provider = "Discord",
            Email = user.Email,
            EmailVerified = user.Verified ?? false,
            Name = !string.IsNullOrEmpty(user.GlobalName) ? user.GlobalName : user.Username,
            Username = user.Username,
            AvatarUrl = avatarUrl,
            AccessToken = accessToken
        };
    }

    #endregion

    #region Microsoft OAuth

    private string BuildMicrosoftAuthUrl(string clientId, string redirectUri, string state, string[] scopes)
    {
        var settings = FindProviderSettings("microsoft");
        var tenant = string.IsNullOrWhiteSpace(settings?.Tenant) ? "common" : settings.Tenant;
        var endpoint = GetEndpoint(
            "microsoft",
            "authorization",
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/oauth2/v2.0/authorize");
        // openid/email/profile are unioned into the resolved list by ResolveAuthorizationScopes,
        // so the URL and the persisted grant record stay identical.
        var scopeString = string.Join(" ", scopes);

        return $"{endpoint}?client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&response_type=code" +
               $"&response_mode=query" +
               $"&scope={Uri.EscapeDataString(scopeString)}" +
               $"&state={Uri.EscapeDataString(state)}";
    }

    private async Task<string> ExchangeMicrosoftCodeAsync(string code, string redirectUri)
    {
        var clientId = GetClientId("microsoft");
        var clientSecret = GetClientSecret("microsoft");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("OAuth client credentials are not configured for provider: microsoft");
        }

        var settings = FindProviderSettings("microsoft");
        var tenant = string.IsNullOrWhiteSpace(settings?.Tenant) ? "common" : settings.Tenant;
        var defaultEndpoint = $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/oauth2/v2.0/token";
        var tokenRequest = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri
        };

        using var content = new FormUrlEncodedContent(tokenRequest);
        using var response = await httpClient.PostAsync(new Uri(GetEndpoint("microsoft", "token", defaultEndpoint)), content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
        return tokenResponse.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Failed to get access token from Microsoft");
    }

    private async Task<OAuthUserProfile> GetMicrosoftUserProfileAsync(string accessToken)
    {
        using var response = await SendBearerRequestAsync(
            GetEndpoint("microsoft", "user-information", MicrosoftUserInfoUrl), accessToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var document = JsonDocument.Parse(content);
        var user = document.RootElement;
        var providerId = user.TryGetProperty("sub", out var subject) ? subject.GetString() : null;
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new InvalidOperationException("Microsoft user information did not include a subject identifier.");
        }

        static string? ReadString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        return new OAuthUserProfile
        {
            ProviderId = providerId,
            Provider = "Microsoft",
            Email = ReadString(user, "email"),
            // Microsoft UserInfo does not attest email verification; do not infer it from an email claim.
            EmailVerified = false,
            Name = ReadString(user, "name"),
            FirstName = ReadString(user, "given_name"),
            LastName = ReadString(user, "family_name"),
            AvatarUrl = ReadString(user, "picture"),
            AccessToken = accessToken
        };
    }

    #endregion
}
