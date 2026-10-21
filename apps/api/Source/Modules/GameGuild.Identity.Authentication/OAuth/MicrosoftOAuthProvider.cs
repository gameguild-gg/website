using System.Text.Json;
using GameGuild.Configuration.PresentationLayer.Authentication;
using AuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Microsoft identity-platform (OIDC v2 common/multi-tenant) provider:
///     authorization-code exchange against <c>login.microsoftonline.com</c> and
///     Graph userinfo claims normalization. openid/email/profile are unioned into
///     every scope list. Wire behavior lifted from the former <c>OAuthService</c>
///     Microsoft switch (issues #235/#246).
/// </summary>
internal sealed class MicrosoftOAuthProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<MicrosoftOAuthProvider> logger,
    AuthenticationOptions? authenticationOptions = null
) : OAuthProviderBase("microsoft", "Microsoft", httpClient, configuration, logger, authenticationOptions)
{
    private const string MicrosoftUserInfoUrl = "https://graph.microsoft.com/oidc/userinfo";

    public override IReadOnlyList<string> DefaultScopes { get; } = ["openid", "email", "profile"];

    protected override IReadOnlyList<string> RequiredScopes { get; } = ["openid", "email", "profile"];

    public override bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(Settings.GetClientId(ProviderKey)) &&
        !string.IsNullOrWhiteSpace(Settings.GetClientSecret(ProviderKey));

    private string ResolveTenant() => ResolveTenant(Settings.FindProviderSettings(ProviderKey));

    private static string ResolveTenant(OAuthProviderOptions? settings) =>
        string.IsNullOrWhiteSpace(settings?.Tenant) ? "common" : settings.Tenant;

    public override string BuildAuthorizationUrl(string redirectUri, string state, string[]? scopes = null)
    {
        var clientId = GetRequiredClientId();

        var settings = Settings.FindProviderSettings(ProviderKey);
        var tenant = ResolveTenant(settings);
        var endpoint = ResolveEndpoint(
            settings?.AuthorizationEndpoint,
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenant)}/oauth2/v2.0/authorize");
        // openid/email/profile are unioned into the resolved list by ResolveScopes,
        // so the URL and the persisted grant record stay identical.
        var scopeString = string.Join(" ", ResolveScopes(scopes));

        return $"{endpoint}?client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&response_type=code" +
               $"&response_mode=query" +
               $"&scope={Uri.EscapeDataString(scopeString)}" +
               $"&state={Uri.EscapeDataString(state)}";
    }

    public override async Task<string> ExchangeCodeAsync(string code, string redirectUri)
    {
        var (clientId, clientSecret) = GetRequiredClientCredentials();

        var settings = Settings.FindProviderSettings(ProviderKey);
        var tenant = ResolveTenant(settings);
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
        using var response = await HttpClient.PostAsync(new Uri(ResolveEndpoint(settings?.TokenEndpoint, defaultEndpoint)), content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
        return tokenResponse.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Failed to get access token from Microsoft");
    }

    public override async Task<OAuthUserProfile> GetUserProfileAsync(string accessToken)
    {
        var settings = Settings.FindProviderSettings(ProviderKey);
        using var response = await SendBearerRequestAsync(
            ResolveEndpoint(settings?.UserInformationEndpoint, MicrosoftUserInfoUrl), accessToken).ConfigureAwait(false);
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
            Provider = DisplayName,
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
}
