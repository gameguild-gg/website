using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Returns this service provider's SAML 2.0 metadata document for exchange with the
///     identity provider (entity ID, POST-binding assertion consumer service).
/// </summary>
public sealed record GetSamlServiceProviderMetadataQuery : IQuery<SamlMetadataResponse>
{
    /// <summary>
    ///     The configured federation provider slug.
    /// </summary>
    public required string Slug { get; init; }
}

/// <summary>
///     Returns the provider's front-channel single-logout redirect (an unsigned
///     LogoutRequest via HTTP-Redirect binding) for logout forwarding; null when the
///     provider has no configured single logout service and logout stays local.
/// </summary>
public sealed record GetSamlLogoutUrlQuery : IQuery<SamlLogoutUrlResponse>
{
    /// <summary>
    ///     The configured federation provider slug.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    ///     Where the provider should return the browser after its logout completes
    ///     (carried as RelayState on the redirect binding).
    /// </summary>
    public string? PostLogoutRedirectUri { get; init; }
}
