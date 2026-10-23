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
///     GitHub OAuth 2.0 provider: authorization-code exchange (JSON body with an
///     <c>Accept: application/json</c> header) and profile normalization with the
///     primary-email fallback. Wire behavior lifted from the former
///     <c>OAuthService</c> GitHub switch (issues #235/#246).
/// </summary>
internal sealed class GitHubOAuthProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<GitHubOAuthProvider> logger,
    AuthenticationOptions? authenticationOptions = null
) : OAuthProviderBase("github", "GitHub", httpClient, configuration, logger, authenticationOptions)
{
    private const string GitHubAuthUrl = "https://github.com/login/oauth/authorize";

    private const string GitHubTokenUrl = "https://github.com/login/oauth/access_token";

    private const string GitHubUserUrl = "https://api.github.com/user";

    private const string GitHubEmailUrl = "https://api.github.com/user/emails";

    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };

    public override IReadOnlyList<string> DefaultScopes { get; } = ["read:user", "user:email"];

    public override bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(Settings.GetClientId(ProviderKey)) &&
        !string.IsNullOrWhiteSpace(Settings.GetClientSecret(ProviderKey));

    public override string BuildAuthorizationUrl(string redirectUri, string state, string[]? scopes = null)
    {
        var clientId = GetRequiredClientId();

        var configuredScopes = ResolveScopes(scopes);
        var scopeString = configuredScopes is { Length: > 0 } ? string.Join(" ", configuredScopes) : string.Join(" ", DefaultScopes);

        var settings = Settings.FindProviderSettings(ProviderKey);
        var endpoint = ResolveEndpoint(settings?.AuthorizationEndpoint, GitHubAuthUrl);

        return $"{endpoint}?client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&state={Uri.EscapeDataString(state)}" +
               $"&scope={Uri.EscapeDataString(scopeString)}";
    }

    public override async Task<string> ExchangeCodeAsync(string code, string redirectUri)
    {
        var (clientId, clientSecret) = GetRequiredClientCredentials();

        var tokenRequest = new { client_id = clientId, client_secret = clientSecret, code, redirect_uri = redirectUri };

        var settings = Settings.FindProviderSettings(ProviderKey);
        using var content = new StringContent(JsonSerializer.Serialize(tokenRequest), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ResolveEndpoint(settings?.TokenEndpoint, GitHubTokenUrl))) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

        return tokenResponse.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("Failed to get access token from GitHub");
    }

    public override async Task<OAuthUserProfile> GetUserProfileAsync(string accessToken)
    {
        var settings = Settings.FindProviderSettings(ProviderKey);
        using var response = await SendBearerRequestAsync(
            ResolveEndpoint(settings?.UserInformationEndpoint, GitHubUserUrl), accessToken, sendUserAgent: true).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var user = JsonSerializer.Deserialize<GitHubUserDto>(content, _jsonOptions) ?? throw new InvalidOperationException("Failed to parse GitHub user");

        string? email = user.Email;
        var emailVerified = false;

        // Get email if not public; only /user/emails entries with verified=true are attested by GitHub.
        if (string.IsNullOrEmpty(email))
        {
            var (primaryEmail, primaryEmailVerified) = await GetGitHubPrimaryEmailAsync(accessToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(primaryEmail))
            {
                email = primaryEmail;
                emailVerified = primaryEmailVerified;
            }
        }

        return new OAuthUserProfile
        {
            ProviderId = user.Id.ToString(CultureInfo.InvariantCulture),
            Provider = DisplayName,
            Email = email,
            // The public profile email is user-set arbitrary text — never treat it as attested.
            EmailVerified = emailVerified,
            Name = user.Name,
            Username = user.Login,
            AvatarUrl = user.AvatarUrl,
            AccessToken = accessToken
        };
    }

    /// <returns>The email and whether GitHub attests it (<c>verified=true</c>); null email when none qualifies.</returns>
    private async Task<(string? Email, bool Verified)> GetGitHubPrimaryEmailAsync(string accessToken)
    {
        try
        {
            var settings = Settings.FindProviderSettings(ProviderKey);
            var emailEndpoint = settings?.UserEmailEndpoint;
            if (string.IsNullOrWhiteSpace(emailEndpoint))
            {
                // Never send a GitHub access token to api.github.com when a custom user endpoint is configured.
                if (!string.IsNullOrWhiteSpace(settings?.UserInformationEndpoint))
                {
                    return (null, false);
                }

                emailEndpoint = GitHubEmailUrl;
            }

            // Ensure auth header is set with the correct access token
            using var emailResponse = await SendBearerRequestAsync(emailEndpoint, accessToken, sendUserAgent: true).ConfigureAwait(false);
            emailResponse.EnsureSuccessStatusCode();

            var emailContent = await emailResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var emails = JsonSerializer.Deserialize<JsonElement[]>(emailContent);

            if (emails != null)
            {
                // Prefer the primary email GitHub attests (primary && verified); fall back to the
                // primary entry so an unverified primary surfaces with Verified=false.
                var primary = emails.FirstOrDefault(e => e.GetProperty("primary").GetBoolean());
                var verifiedPrimary = emails.FirstOrDefault(e => e.GetProperty("primary").GetBoolean() && e.GetProperty("verified").GetBoolean());
                var selected = verifiedPrimary.ValueKind != JsonValueKind.Undefined ? verifiedPrimary : primary;

                if (selected.ValueKind != JsonValueKind.Undefined && selected.GetProperty("email").GetString() is { } selectedEmail)
                {
                    return (selectedEmail, verifiedPrimary.ValueKind != JsonValueKind.Undefined);
                }

                return (null, false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to fetch GitHub user emails");
            throw;
        }

        return (null, false);
    }
}
