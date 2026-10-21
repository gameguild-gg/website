using System.Text;
using System.Text.Json;
using GameGuild.Configuration.PresentationLayer.Authentication;
using AuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Epic Games OAuth 2.0 provider (Epic Online Services account system):
///     authorization-code exchange against <c>api.epicgames.dev</c> with HTTP Basic
///     client credentials, and <c>userInfo</c> claims normalization. Fail-closed:
///     without a configured client ID/secret every operation throws, so the API
///     surface returns 503 instead of half-working (issue #246).
/// </summary>
internal sealed class EpicGamesOAuthProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<EpicGamesOAuthProvider> logger,
    AuthenticationOptions? authenticationOptions = null
) : OAuthProviderBase("epic", "Epic", httpClient, configuration, logger, authenticationOptions)
{
    private const string EpicAuthUrl = "https://www.epicgames.com/id/authorize";

    private const string EpicTokenUrl = "https://api.epicgames.dev/epic/oauth/v2/token";

    private const string EpicUserInfoUrl = "https://api.epicgames.dev/epic/oauth/v2/userInfo";

    public override IReadOnlyList<string> DefaultScopes { get; } = ["basic_profile", "email"];

    public override bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(Settings.GetClientId(ProviderKey)) &&
        !string.IsNullOrWhiteSpace(Settings.GetClientSecret(ProviderKey));

    public override string BuildAuthorizationUrl(string redirectUri, string state, string[]? scopes = null)
    {
        var clientId = GetRequiredClientId();

        var configuredScopes = ResolveScopes(scopes);
        var scopeString = configuredScopes is { Length: > 0 } ? string.Join(" ", configuredScopes) : string.Join(" ", DefaultScopes);

        var settings = Settings.FindProviderSettings(ProviderKey);
        var endpoint = ResolveEndpoint(settings?.AuthorizationEndpoint, EpicAuthUrl);

        return $"{endpoint}?client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&scope={Uri.EscapeDataString(scopeString)}" +
               $"&state={Uri.EscapeDataString(state)}" +
               $"&response_type=code";
    }

    public override async Task<string> ExchangeCodeAsync(string code, string redirectUri)
    {
        var (clientId, clientSecret) = GetRequiredClientCredentials();

        // Epic's token endpoint authenticates the client with HTTP Basic credentials
        // (RFC 6749 section 2.3.1) and only accepts form-urlencoded grant parameters.
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        var tokenRequest = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri
        };

        var settings = Settings.FindProviderSettings(ProviderKey);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ResolveEndpoint(settings?.TokenEndpoint, EpicTokenUrl)))
        {
            Content = new FormUrlEncodedContent(tokenRequest)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        using var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

        return tokenResponse.TryGetProperty("access_token", out var accessToken) && accessToken.ValueKind == JsonValueKind.String
            ? accessToken.GetString() ?? throw new InvalidOperationException("Failed to get access token from Epic")
            : throw new InvalidOperationException("Epic token response did not include an access token");
    }

    public override async Task<OAuthUserProfile> GetUserProfileAsync(string accessToken)
    {
        var settings = Settings.FindProviderSettings(ProviderKey);
        using var response = await SendBearerRequestAsync(ResolveEndpoint(settings?.UserInformationEndpoint, EpicUserInfoUrl), accessToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var document = JsonDocument.Parse(content);
        var user = document.RootElement;

        static string? ReadString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        var providerId = ReadString(user, "sub");
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new InvalidOperationException("Epic user information did not include a subject identifier.");
        }

        return new OAuthUserProfile
        {
            ProviderId = providerId,
            Provider = DisplayName,
            Email = ReadString(user, "email"),
            // Epic attests verification only through the explicit email_verified claim;
            // an email claim alone must never be treated as verified.
            EmailVerified = user.TryGetProperty("email_verified", out var verified) && verified.ValueKind == JsonValueKind.True,
            Name = ReadString(user, "name"),
            AvatarUrl = ReadString(user, "picture"),
            AccessToken = accessToken
        };
    }
}
