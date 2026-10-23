using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command to handle a SAML 2.0 federation provider callback (Assertion Consumer
///     Service): the Base64 <c>SAMLResponse</c> posted by the identity provider is
///     signature-verified and validated (issuer, audience, destination, lifetimes,
///     InResponseTo correlation, replay), then sign-in completes through the shared
///     external-identity auto-link/JIT policy.
/// </summary>
public sealed class SamlCallbackCommand : ICommand<SignInResponse>
{
    /// <summary>
    ///     The configured federation provider slug.
    /// </summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>
    ///     The Base64-encoded <c>SAMLResponse</c> XML from the provider's HTTP-POST.
    /// </summary>
    public string SamlResponse { get; init; } = string.Empty;

    /// <summary>
    ///     The RelayState echoed by the provider (correlation is enforced server-side
    ///     through the response's InResponseTo, not this value).
    /// </summary>
    public string RelayState { get; init; } = string.Empty;

    /// <summary>
    ///     Optional tenant context.
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    ///     Whether the caller asked for a persistent ("remember me") refresh-token lifetime.
    /// </summary>
    public bool? RememberMe { get; init; }
}
