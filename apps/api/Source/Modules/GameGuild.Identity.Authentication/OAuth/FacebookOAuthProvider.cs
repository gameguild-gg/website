using System.Text.Json;
using GameGuild.Configuration.PresentationLayer.Authentication;
using AuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Facebook Graph OAuth 2.0 provider: authorization-code exchange (client
///     credentials exchanged as query parameters against the Graph token endpoint)
///     and <c>/me</c> field-projection claims normalization. Fail-closed: without a
///     configured app ID/secret every operation throws, so the API surface returns
///     503 instead of half-working (issue #246).
/// </summary>
internal sealed class FacebookOAuthProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<FacebookOAuthProvider> logger,
    AuthenticationOptions? authenticationOptions = null
) : OAuthProviderBase("facebook", "Facebook", httpClient, configuration, logger, authenticationOptions)
{
    private const string FacebookAuthUrl = "https://www.facebook.com/v19.0/dialog/oauth";

    private const string FacebookTokenUrl = "https://graph.facebook.com/v19.0/oauth/access_token";

    private const string FacebookUserUrl = "https://graph.facebook.com/v19.0/me";

    private const string FacebookProfileFields = "id,name,email,first_name,last_name,picture.type(large)";

    public override IReadOnlyList<string> DefaultScopes { get; } = ["email", "public_profile"];

    public override bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(Settings.GetClientId(ProviderKey)) &&
        !string.IsNullOrWhiteSpace(Settings.GetClientSecret(ProviderKey));

    public override string BuildAuthorizationUrl(string redirectUri, string state, string[]? scopes = null)
    {
        var clientId = GetRequiredClientId();

        var configuredScopes = ResolveScopes(scopes);
        var scopeString = configuredScopes is { Length: > 0 } ? string.Join(",", configuredScopes) : string.Join(",", DefaultScopes);

        var settings = Settings.FindProviderSettings(ProviderKey);
        var endpoint = ResolveEndpoint(settings?.AuthorizationEndpoint, FacebookAuthUrl);

        return $"{endpoint}?client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&state={Uri.EscapeDataString(state)}" +
               $"&scope={Uri.EscapeDataString(scopeString)}" +
               $"&response_type=code";
    }

    public override async Task<string> ExchangeCodeAsync(string code, string redirectUri)
    {
        var (clientId, clientSecret) = GetRequiredClientCredentials();

        // Facebook's Graph token endpoint takes the client credentials and the code
        // as query parameters (server-side flow); no Basic auth variant is offered.
        var settings = Settings.FindProviderSettings(ProviderKey);
        var query = BuildQueryString(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["code"] = code
        });

        using var response = await HttpClient.GetAsync(new Uri($"{ResolveEndpoint(settings?.TokenEndpoint, FacebookTokenUrl)}?{query}")).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

        return tokenResponse.TryGetProperty("access_token", out var accessToken) && accessToken.ValueKind == JsonValueKind.String
            ? accessToken.GetString() ?? throw new InvalidOperationException("Failed to get access token from Facebook")
            : throw new InvalidOperationException("Facebook token response did not include an access token");
    }

    public override async Task<OAuthUserProfile> GetUserProfileAsync(string accessToken)
    {
        var settings = Settings.FindProviderSettings(ProviderKey);
        var query = BuildQueryString(new Dictionary<string, string>
        {
            ["fields"] = FacebookProfileFields,
            ["access_token"] = accessToken
        });

        using var response = await HttpClient.GetAsync(new Uri($"{ResolveEndpoint(settings?.UserInformationEndpoint, FacebookUserUrl)}?{query}")).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var document = JsonDocument.Parse(content);
        var user = document.RootElement;

        static string? ReadString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        var providerId = ReadString(user, "id");
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new InvalidOperationException("Facebook user information did not include an identifier.");
        }

        // Graph API picture returns { data: { url } } for field projections.
        var avatarUrl = ReadString(user, "picture");
        if (avatarUrl is null && user.TryGetProperty("picture", out var picture) &&
            picture.TryGetProperty("data", out var pictureData))
        {
            avatarUrl = ReadString(pictureData, "url");
        }

        return new OAuthUserProfile
        {
            ProviderId = providerId,
            Provider = DisplayName,
            Email = ReadString(user, "email"),
            // The modern Graph API no longer exposes a verified flag; an email claim
            // alone must never be treated as verified.
            EmailVerified = user.TryGetProperty("verified", out var verified) && verified.ValueKind == JsonValueKind.True,
            Name = ReadString(user, "name"),
            FirstName = ReadString(user, "first_name"),
            LastName = ReadString(user, "last_name"),
            AvatarUrl = avatarUrl,
            AccessToken = accessToken
        };
    }
}
