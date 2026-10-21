using GameGuild.Configuration.PresentationLayer.Authentication;
using AuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;
using Microsoft.Extensions.Configuration;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Shared configuration plumbing for OAuth providers: typed provider settings from
///     <c>Authentication:ExternalProviders:Providers</c> plus the legacy
///     <c>OAuth:&lt;Provider&gt;:&lt;ClientId|ClientSecret&gt;</c> fallback keys. Extracted from the
///     former <c>OAuthService</c> switches so every <see cref="IOAuthProvider" /> and the
///     <see cref="OAuthService" /> dispatcher resolve credentials identically (issue #235).
/// </summary>
internal sealed class OAuthProviderSettingsResolver(
    AuthenticationOptions? authenticationOptions,
    IConfiguration configuration
)
{
    public OAuthProviderOptions? FindProviderSettings(string provider)
    {
        var providers = authenticationOptions?.ExternalProviders?.Providers;
        if (providers is null)
        {
            return null;
        }

        return providers.FirstOrDefault(entry => entry.Key.Equals(provider, StringComparison.OrdinalIgnoreCase)).Value;
    }

    /// <summary>
    ///     Fail-closed enablement gate: a provider explicitly disabled in configuration
    ///     refuses authorization-code and profile requests.
    /// </summary>
    public void EnsureProviderEnabled(string provider)
    {
        var settings = FindProviderSettings(provider);
        if (settings is not null && !settings.Enabled)
        {
            throw new InvalidOperationException($"OAuth provider '{provider}' is disabled.");
        }
    }

    public string? GetClientId(string provider) =>
        FindProviderSettings(provider)?.ClientId ?? GetLegacyProviderSetting(provider, "ClientId");

    public string? GetClientSecret(string provider) =>
        FindProviderSettings(provider)?.ClientSecret ?? GetLegacyProviderSetting(provider, "ClientSecret");

    /// <summary>
    ///     Provider API key used by protocols whose credential is not an OAuth client
    ///     secret (Steam OpenID 2.0 uses a Steam Web API key for profile summaries).
    /// </summary>
    public string? GetApiKey(string provider) =>
        FindProviderSettings(provider)?.ApiKey ?? GetLegacyProviderSetting(provider, "ApiKey");

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
            "epic" => "Epic",
            "facebook" => "Facebook",
            "github" => "GitHub",
            "google" => "Google",
            "microsoft" => "Microsoft",
            "steam" => "Steam",
            _ => provider
        };

        return configuration[$"OAuth:{canonicalProvider}:{settingName}"];
    }
}
