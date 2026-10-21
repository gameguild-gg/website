using System.Globalization;
using System.Text.Json;
using GameGuild.Configuration.PresentationLayer.Authentication;
using AuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Steam OpenID 2.0 provider (redirect flow): builds a <c>checkid_setup</c>
///     redirect to <c>steamcommunity.com/openid/login</c>, verifies the signed
///     callback assertion through the OP's <c>check_authentication</c> endpoint, and
///     normalizes player-summary claims from the Steam Web API. OpenID 2.0 issues no
///     access token, so the verified 64-bit SteamID takes the token slot and the
///     protocol supplies no email claim — the profile intentionally carries none.
///     Fail-closed: without a Steam Web API key every operation throws (issue #246).
/// </summary>
internal sealed class SteamOpenIdProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<SteamOpenIdProvider> logger,
    AuthenticationOptions? authenticationOptions = null
) : OAuthProviderBase("steam", "Steam", httpClient, configuration, logger, authenticationOptions)
{
    private const string SteamLoginUrl = "https://steamcommunity.com/openid/login";

    private const string SteamUserInfoUrl = "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/";

    private const string OpenIdNamespace = "http://specs.openid.net/auth/2.0";

    private const string OpenIdIdentifierSelect = "http://specs.openid.net/auth/2.0/identifier_select";

    /// <summary>OpenID 2.0 has no scope concept.</summary>
    public override IReadOnlyList<string> DefaultScopes { get; } = [];

    public override bool IsConfigured() => !string.IsNullOrWhiteSpace(Settings.GetApiKey(ProviderKey));

    public override string BuildAuthorizationUrl(string redirectUri, string state, string[]? scopes = null)
    {
        GetRequiredApiKey();

        // The realm must cover the return_to URL; derive it from the redirect target.
        var returnUri = new Uri(redirectUri, UriKind.Absolute);
        var realm = $"{returnUri.Scheme}://{returnUri.Authority}";

        var query = BuildQueryString(new Dictionary<string, string>
        {
            ["openid.ns"] = OpenIdNamespace,
            ["openid.mode"] = "checkid_setup",
            ["openid.return_to"] = redirectUri,
            ["openid.realm"] = realm,
            ["openid.identity"] = OpenIdIdentifierSelect,
            ["openid.claimed_id"] = OpenIdIdentifierSelect
        });

        var settings = Settings.FindProviderSettings(ProviderKey);
        var endpoint = ResolveEndpoint(settings?.AuthorizationEndpoint, SteamLoginUrl);

        // OpenID 2.0 carries CSRF protection through the exact return_to match; the
        // state parameter is appended for surface parity with the OAuth providers.
        return $"{endpoint}?{query}&state={Uri.EscapeDataString(state)}";
    }

    public override async Task<string> ExchangeCodeAsync(string code, string redirectUri)
    {
        var assertion = ParseAssertion(code);

        // The assertion must answer the request we made: an exact return_to match is the
        // OpenID 2.0 CSRF/redirect protection, so a mismatched reply is rejected.
        if (!assertion.TryGetValue("openid.return_to", out var assertedReturnTo) ||
            !string.Equals(assertedReturnTo, redirectUri, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Steam OpenID assertion return_to does not match the expected redirect URI.");
        }

        var claimedId = assertion.GetValueOrDefault("openid.claimed_id");
        if (string.IsNullOrWhiteSpace(claimedId) || !claimedId.StartsWith("https://steamcommunity.com/openid/id/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Steam OpenID assertion did not include a Steam claimed identity.");
        }

        // Re-verify the signed assertion with the OP: every field is echoed back with
        // openid.mode switched to check_authentication (Steam signs the full field set).
        var verificationFields = assertion
            .Where(field => field.Key.StartsWith("openid.", StringComparison.Ordinal))
            .Select(field => field.Key == "openid.mode"
                ? new KeyValuePair<string, string>(field.Key, "check_authentication")
                : field)
            .ToList();

        var settings = Settings.FindProviderSettings(ProviderKey);
        var verificationEndpoint = ResolveEndpoint(settings?.TokenEndpoint, SteamLoginUrl);

        using var content = new FormUrlEncodedContent(verificationFields);
        using var response = await HttpClient.PostAsync(new Uri(verificationEndpoint), content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var verification = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!verification.Contains("is_valid:true", StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Steam OpenID assertion failed provider verification.");
        }

        var steamId = claimedId.Substring("https://steamcommunity.com/openid/id/".Length);
        if (steamId.Length == 0 || !ulong.TryParse(steamId, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            throw new InvalidOperationException("Steam OpenID claimed identity did not include a numeric SteamID.");
        }

        return steamId;
    }

    public override async Task<OAuthUserProfile> GetUserProfileAsync(string accessToken)
    {
        var steamId = accessToken;
        var apiKey = GetRequiredApiKey();

        var settings = Settings.FindProviderSettings(ProviderKey);
        var query = BuildQueryString(new Dictionary<string, string>
        {
            ["steamids"] = steamId,
            ["key"] = apiKey
        });

        using var response = await HttpClient.GetAsync(new Uri($"{ResolveEndpoint(settings?.UserInformationEndpoint, SteamUserInfoUrl)}?{query}")).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        if (!root.TryGetProperty("response", out var responseBody) ||
            !responseBody.TryGetProperty("players", out var players) ||
            players.ValueKind != JsonValueKind.Array || players.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Steam player summary did not include the requested account.");
        }

        var player = players[0];

        static string? ReadString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        var profileSteamId = ReadString(player, "steamid") ?? steamId;
        if (!string.Equals(profileSteamId, steamId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Steam player summary did not match the verified SteamID.");
        }

        var personaName = ReadString(player, "personaname");

        return new OAuthUserProfile
        {
            ProviderId = steamId,
            Provider = DisplayName,
            // OpenID 2.0 supplies no email claim; the internal unverified identifier is
            // synthesized by the sign-in service (same policy as wallet sign-in).
            Email = null,
            EmailVerified = false,
            Name = ReadString(player, "realname") is { Length: > 0 } realName ? realName : personaName,
            Username = personaName,
            AvatarUrl = ReadString(player, "avatarfull") ?? ReadString(player, "avatar"),
            AccessToken = steamId
        };
    }

    /// <summary>
    ///     Fail-closed credential gate: the Steam Web API key gates the profile lookup
    ///     and is required up-front at authorize time so a half-configured provider is
    ///    refused loudly instead of failing mid-flow.
    /// </summary>
    private string GetRequiredApiKey()
    {
        var apiKey = Settings.GetApiKey(ProviderKey);

        if (string.IsNullOrWhiteSpace(apiKey)) { throw new InvalidOperationException("Steam Web API key not configured for provider: steam"); }

        return apiKey;
    }

    private static Dictionary<string, string> ParseAssertion(string encodedAssertion)
    {
        if (string.IsNullOrWhiteSpace(encodedAssertion))
        {
            throw new InvalidOperationException("Steam OpenID callback assertion is required.");
        }

        var assertion = QueryHelpers.ParseQuery(encodedAssertion.TrimStart('?'))
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToString(),
                StringComparer.Ordinal);

        if (!assertion.ContainsKey("openid.ns"))
        {
            throw new InvalidOperationException("Steam OpenID callback assertion is malformed (no openid.ns field).");
        }

        return assertion;
    }
}
