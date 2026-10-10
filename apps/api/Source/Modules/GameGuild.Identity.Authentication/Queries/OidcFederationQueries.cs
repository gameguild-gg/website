using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Domain-to-provider discovery: lists the enabled federation providers whose configured
///     email domains match the requested address (or bare domain).
/// </summary>
public sealed record DiscoverOidcProvidersQuery : IQuery<OidcDiscoverProviderResponse>
{
    /// <summary>
    ///     The email address (or bare domain) to route.
    /// </summary>
    public required string Email { get; init; }
}

/// <summary>
///     Returns the provider's front-channel logout URL (discovered <c>end_session_endpoint</c>
    /// with the post-logout redirect applied) for logout forwarding; null when the provider
///     does not advertise one.
/// </summary>
public sealed record GetOidcEndSessionUrlQuery : IQuery<OidcEndSessionUrlResponse>
{
    /// <summary>
    ///     The configured federation provider slug.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    ///     Where the provider should return the browser after its logout completes.
    /// </summary>
    public string? PostLogoutRedirectUri { get; init; }
}
