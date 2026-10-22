using System.Globalization;
using System.Net.Http.Headers;
using GameGuild.Configuration.PresentationLayer.Authentication;
using AuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Shared plumbing for <see cref="IOAuthProvider" /> implementations: settings
///     resolution, credential gates, scope normalization, and bearer requests. The wire
///     behavior (URL shapes, request bodies, exception messages) is lifted verbatim from
///     the former per-provider switches in <c>OAuthService</c> so existing clients and
///     tests observe no contract change (issues #235/#246).
/// </summary>
internal abstract class OAuthProviderBase(
    string providerKey,
    string displayName,
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger logger,
    AuthenticationOptions? authenticationOptions = null
) : IOAuthProvider
{
    protected readonly HttpClient HttpClient = httpClient;

    protected readonly ILogger Logger = logger;

    protected readonly OAuthProviderSettingsResolver Settings = new(authenticationOptions, configuration);

    public string ProviderKey { get; } = providerKey;

    public string DisplayName { get; } = displayName;

    public abstract IReadOnlyList<string> DefaultScopes { get; }

    /// <summary>
    ///     Protocol-required scope tokens unioned into every resolved list after
    ///     deduplication (Microsoft unions openid/email/profile; empty by default).
    /// </summary>
    protected virtual IReadOnlyList<string> RequiredScopes => [];

    public abstract bool IsConfigured();

    public abstract string BuildAuthorizationUrl(string redirectUri, string state, string[]? scopes = null);

    public abstract Task<string> ExchangeCodeAsync(string code, string redirectUri);

    public abstract Task<OAuthUserProfile> GetUserProfileAsync(string accessToken);

    /// <inheritdoc />
    public string[] ResolveScopes(string[]? requestedScopes = null)
    {
        if (requestedScopes is { Length: > 0 })
        {
            return NormalizeScopes(requestedScopes);
        }

        var configuredScopes = Settings.FindProviderSettings(ProviderKey)?.Scopes;
        return configuredScopes is { Count: > 0 }
            ? NormalizeScopes([.. configuredScopes])
            : [.. DefaultScopes];
    }

    /// <summary>
    ///     Deduplicates scope tokens preserving first-seen order, then unions the
    ///     protocol-required scopes of the provider (mirrors the former
    ///     <c>OAuthService.NormalizeScopes</c> inline behavior).
    /// </summary>
    protected string[] NormalizeScopes(string[] scopes)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = scopes.Where(scope => seen.Add(scope)).ToList();

        foreach (var requiredScope in RequiredScopes)
        {
            if (!seen.Contains(requiredScope))
            {
                seen.Add(requiredScope);
                normalized.Add(requiredScope);
            }
        }

        return [.. normalized];
    }

    /// <summary>
    ///     Fail-closed credential gate for OAuth 2.0 providers: both a client ID and a
    ///     client secret must be present before any provider call is attempted.
    /// </summary>
    protected (string ClientId, string ClientSecret) GetRequiredClientCredentials()
    {
        var clientId = Settings.GetClientId(ProviderKey);
        var clientSecret = Settings.GetClientSecret(ProviderKey);

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException($"OAuth client credentials are not configured for provider: {ProviderKey}");
        }

        return (clientId, clientSecret);
    }

    /// <summary>
    ///     Fail-closed credential gate for the authorize step: a client ID must be
    ///     present to build a redirect URL (message kept identical to the former
    ///     <c>OAuthService</c> check so the API surface keeps returning the same 503).
    /// </summary>
    protected string GetRequiredClientId()
    {
        var clientId = Settings.GetClientId(ProviderKey);

        if (string.IsNullOrEmpty(clientId)) { throw new InvalidOperationException($"OAuth client ID not configured for provider: {ProviderKey}"); }

        return clientId;
    }

    protected string ResolveEndpoint(string? configuredEndpoint, string fallback) =>
        string.IsNullOrWhiteSpace(configuredEndpoint) ? fallback : configuredEndpoint;

    protected async Task<HttpResponseMessage> SendBearerRequestAsync(string uri, string accessToken, bool sendUserAgent = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(uri));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (sendUserAgent)
        {
            request.Headers.UserAgent.ParseAdd("GameGuild");
        }

        return await HttpClient.SendAsync(request).ConfigureAwait(false);
    }

    protected string BuildQueryString(IEnumerable<KeyValuePair<string, string>> parameters) =>
        string.Join("&", parameters.Select(parameter => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));

    protected string ToTitleCaseProvider() => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(ProviderKey);
}
