using System.Text.Json;
using GameGuild.Configuration.PresentationLayer.Authentication;
using AuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Google OAuth 2.0 provider: authorization-code exchange (form-urlencoded body)
///     and profile normalization. Wire behavior lifted from the former
///     <c>OAuthService</c> Google switch (issues #235/#246).
/// </summary>
internal sealed class GoogleOAuthProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<GoogleOAuthProvider> logger,
    AuthenticationOptions? authenticationOptions = null
) : OAuthProviderBase("google", "Google", httpClient, configuration, logger, authenticationOptions)
{
    private const string GoogleAuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";

    private const string GoogleTokenUrl = "https://oauth2.googleapis.com/token";

    private const string GoogleUserUrl = "https://www.googleapis.com/oauth2/v2/userinfo";

    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };

    public override IReadOnlyList<string> DefaultScopes { get; } = ["openid", "email", "profile"];

    public override bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(Settings.GetClientId(ProviderKey)) &&
        !string.IsNullOrWhiteSpace(Settings.GetClientSecret(ProviderKey));

    public override string BuildAuthorizationUrl(string redirectUri, string state, string[]? scopes = null)
    {
        var clientId = GetRequiredClientId();

        var configuredScopes = ResolveScopes(scopes);
        var scopeString = configuredScopes is { Length: > 0 } ? string.Join(" ", configuredScopes) : string.Join(" ", DefaultScopes);

        var settings = Settings.FindProviderSettings(ProviderKey);
        var endpoint = ResolveEndpoint(settings?.AuthorizationEndpoint, GoogleAuthUrl);

        return $"{endpoint}?client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&state={Uri.EscapeDataString(state)}" +
               $"&scope={Uri.EscapeDataString(scopeString)}" +
               $"&response_type=code" +
               $"&access_type=offline";
    }

    public override async Task<string> ExchangeCodeAsync(string code, string redirectUri)
    {
        var (clientId, clientSecret) = GetRequiredClientCredentials();

        var tokenRequest = new Dictionary<string, string> { { "client_id", clientId }, { "client_secret", clientSecret }, { "code", code }, { "grant_type", "authorization_code" }, { "redirect_uri", redirectUri } };

        var settings = Settings.FindProviderSettings(ProviderKey);
        using var content = new FormUrlEncodedContent(tokenRequest);
        using var response = await HttpClient.PostAsync(new Uri(ResolveEndpoint(settings?.TokenEndpoint, GoogleTokenUrl)), content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

        return tokenResponse.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("Failed to get access token from Google");
    }

    public override async Task<OAuthUserProfile> GetUserProfileAsync(string accessToken)
    {
        var settings = Settings.FindProviderSettings(ProviderKey);
        using var response = await SendBearerRequestAsync(ResolveEndpoint(settings?.UserInformationEndpoint, GoogleUserUrl), accessToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var user = JsonSerializer.Deserialize<GoogleUserDto>(content, _jsonOptions) ?? throw new InvalidOperationException("Failed to parse Google user");

        return new OAuthUserProfile
        {
            ProviderId = user.Id,
            Provider = DisplayName,
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
}
