using System.Globalization;
using System.Text.Json;
using GameGuild.Configuration.PresentationLayer.Authentication;
using AuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Discord OAuth 2.0 provider: authorization-code exchange (credentials as
///     form-urlencoded fields — Discord's token endpoint rejects JSON) and profile
///     normalization with the snowflake-derived default avatar. Wire behavior lifted
///     from the former <c>OAuthService</c> Discord switch (issues #235/#246).
/// </summary>
internal sealed class DiscordOAuthProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<DiscordOAuthProvider> logger,
    AuthenticationOptions? authenticationOptions = null
) : OAuthProviderBase("discord", "Discord", httpClient, configuration, logger, authenticationOptions)
{
    private const string DiscordAuthUrl = "https://discord.com/oauth2/authorize";

    private const string DiscordTokenUrl = "https://discord.com/api/oauth2/token";

    private const string DiscordUserUrl = "https://discord.com/api/v10/users/@me";

    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };

    public override IReadOnlyList<string> DefaultScopes { get; } = ["identify", "email"];

    public override bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(Settings.GetClientId(ProviderKey)) &&
        !string.IsNullOrWhiteSpace(Settings.GetClientSecret(ProviderKey));

    public override string BuildAuthorizationUrl(string redirectUri, string state, string[]? scopes = null)
    {
        var clientId = GetRequiredClientId();

        var configuredScopes = ResolveScopes(scopes);
        var scopeString = configuredScopes is { Length: > 0 } ? string.Join(" ", configuredScopes) : string.Join(" ", DefaultScopes);

        var settings = Settings.FindProviderSettings(ProviderKey);
        var endpoint = ResolveEndpoint(settings?.AuthorizationEndpoint, DiscordAuthUrl);

        return $"{endpoint}?client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&state={Uri.EscapeDataString(state)}" +
               $"&scope={Uri.EscapeDataString(scopeString)}" +
               $"&response_type=code";
    }

    public override async Task<string> ExchangeCodeAsync(string code, string redirectUri)
    {
        var clientId = Settings.GetClientId(ProviderKey);
        var clientSecret = Settings.GetClientSecret(ProviderKey);

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret)) { throw new InvalidOperationException("Discord OAuth client ID or client secret not configured"); }

        // Discord's token endpoint only accepts form-urlencoded bodies (client credentials as form fields)
        var tokenRequest = new Dictionary<string, string> { { "client_id", clientId }, { "client_secret", clientSecret }, { "grant_type", "authorization_code" }, { "code", code }, { "redirect_uri", redirectUri } };

        var settings = Settings.FindProviderSettings(ProviderKey);
        using var content = new FormUrlEncodedContent(tokenRequest);
        using var response = await HttpClient.PostAsync(new Uri(ResolveEndpoint(settings?.TokenEndpoint, DiscordTokenUrl)), content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

        return tokenResponse.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("Failed to get access token from Discord");
    }

    public override async Task<OAuthUserProfile> GetUserProfileAsync(string accessToken)
    {
        var settings = Settings.FindProviderSettings(ProviderKey);
        using var response = await SendBearerRequestAsync(ResolveEndpoint(settings?.UserInformationEndpoint, DiscordUserUrl), accessToken).ConfigureAwait(false);
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
            Provider = DisplayName,
            Email = user.Email,
            EmailVerified = user.Verified ?? false,
            Name = !string.IsNullOrEmpty(user.GlobalName) ? user.GlobalName : user.Username,
            Username = user.Username,
            AvatarUrl = avatarUrl,
            AccessToken = accessToken
        };
    }
}
