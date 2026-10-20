using GameGuild.Configuration.PresentationLayer.Authentication;
using AuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Provider-agnostic OAuth dispatcher. Delegates every provider concern
///     (authorization URL, code exchange, profile normalization) to the registered
///     <see cref="IOAuthProvider" /> set keyed by provider name — there is no
///     per-provider switch to extend: adding a provider is one implementation class
///     plus one DI registration (issues #235/#246). When constructed without a
///     provider set (unit tests, explicitly composed hosts) the built-in provider
///     implementations are used.
/// </summary>
public class OAuthService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<OAuthService> logger,
    AuthenticationOptions? authenticationOptions = null,
    IEnumerable<IOAuthProvider>? providers = null
) : IOAuthService
{
    private readonly OAuthProviderSettingsResolver _settings = new(authenticationOptions, configuration);

    private readonly IReadOnlyDictionary<string, IOAuthProvider> _providers = BuildProviderRegistry(providers, httpClient, configuration, authenticationOptions);

    public Task<string> GetAuthorizationUrlAsync(string provider, string redirectUri, string state, string[]? scopes = null)
    {
        EnsureProviderEnabled(provider);

        var resolved = TryFindProvider(provider);
        if (resolved is null)
        {
            // Preserves the legacy failure order: an unknown provider without credentials
            // failed on the generic client-id check before the unsupported-provider guard.
            if (string.IsNullOrEmpty(_settings.GetClientId(provider))) { throw new InvalidOperationException($"OAuth client ID not configured for provider: {provider}"); }

            throw new NotSupportedException($"OAuth provider not supported: {provider}");
        }

        return Task.FromResult(resolved.BuildAuthorizationUrl(redirectUri, state, scopes));
    }

    /// <inheritdoc />
    public string[] ResolveAuthorizationScopes(string provider, string[]? requestedScopes = null)
    {
        var resolved = TryFindProvider(provider);

        return resolved is null
            ? throw new NotSupportedException($"OAuth provider not supported: {provider}")
            : resolved.ResolveScopes(requestedScopes);
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

        var resolved = TryFindProvider(provider) ?? throw new NotSupportedException($"Provider not supported: {provider}");
        var accessToken = await resolved.ExchangeCodeAsync(code, redirectUri).ConfigureAwait(false);

        return await resolved.GetUserProfileAsync(accessToken).ConfigureAwait(false);
    }

    public async Task<OAuthUserProfile> GetUserProfileAsync(string provider, string accessToken)
    {
        EnsureProviderEnabled(provider);

        var resolved = TryFindProvider(provider) ?? throw new NotSupportedException($"Provider not supported: {provider}");

        return await resolved.GetUserProfileAsync(accessToken).ConfigureAwait(false);
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

    private IOAuthProvider? TryFindProvider(string provider) =>
        _providers.TryGetValue(provider, out var resolved) ? resolved : null;

    private void EnsureProviderEnabled(string provider) => _settings.EnsureProviderEnabled(provider);

    /// <summary>
    ///     Builds the case-insensitive provider registry. Duplicate provider keys are a
    ///     composition bug and fail loudly at construction.
    /// </summary>
    private static IReadOnlyDictionary<string, IOAuthProvider> BuildProviderRegistry(
        IEnumerable<IOAuthProvider>? providers,
        HttpClient httpClient,
        IConfiguration configuration,
        AuthenticationOptions? authenticationOptions)
    {
        var registered = providers?.ToList();
        if (registered is { Count: > 0 })
        {
            return registered.ToDictionary(
                provider => provider.ProviderKey,
                static provider => provider,
                StringComparer.OrdinalIgnoreCase);
        }

        // Legacy construction path (no DI provider set): fall back to the built-in
        // provider implementations so the service contract is unchanged.
        var builtIn = new List<IOAuthProvider>
        {
            new GitHubOAuthProvider(httpClient, configuration, NullLogger<GitHubOAuthProvider>.Instance, authenticationOptions),
            new GoogleOAuthProvider(httpClient, configuration, NullLogger<GoogleOAuthProvider>.Instance, authenticationOptions),
            new DiscordOAuthProvider(httpClient, configuration, NullLogger<DiscordOAuthProvider>.Instance, authenticationOptions),
            new MicrosoftOAuthProvider(httpClient, configuration, NullLogger<MicrosoftOAuthProvider>.Instance, authenticationOptions),
            new SteamOpenIdProvider(httpClient, configuration, NullLogger<SteamOpenIdProvider>.Instance, authenticationOptions),
            new EpicGamesOAuthProvider(httpClient, configuration, NullLogger<EpicGamesOAuthProvider>.Instance, authenticationOptions),
            new FacebookOAuthProvider(httpClient, configuration, NullLogger<FacebookOAuthProvider>.Instance, authenticationOptions)
        };

        return builtIn.ToDictionary(
            provider => provider.ProviderKey,
            static provider => provider,
            StringComparer.OrdinalIgnoreCase);
    }
}
