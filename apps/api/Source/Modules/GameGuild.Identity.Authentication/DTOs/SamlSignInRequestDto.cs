using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Request to initiate a SAML federation provider sign-in
/// </summary>
public class SamlAuthorizeRequestDto
{
    /// <summary>
    ///     Optional RelayState the provider echoes on callback (defaults to a generated token)
    /// </summary>
    public string? RelayState { get; set; }
}

/// <summary>
///     Request body for the SAML federation Assertion Consumer Service endpoint
/// </summary>
public class SamlCallbackRequestDto
{
    /// <summary>
    ///     The Base64-encoded SAMLResponse XML posted by the federation provider
    /// </summary>
    [Required]
    public string SamlResponse { get; set; } = string.Empty;

    /// <summary>
    ///     The RelayState echoed by the provider (correlation is enforced server-side
    ///     through the response's InResponseTo, not this value)
    /// </summary>
    public string RelayState { get; set; } = string.Empty;

    /// <summary>
    ///     Optional tenant context
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    ///     Whether the caller asked for a persistent ("remember me") refresh-token lifetime
    /// </summary>
    public bool? RememberMe { get; set; }
}

/// <summary>
///     Domain request for SAML federation callback sign-in
/// </summary>
public class SamlSignInRequest
{
    /// <summary>
    ///     The configured federation provider slug (<c>Authentication:ExternalProviders:Saml:&lt;slug&gt;</c>)
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    public string SamlResponse { get; set; } = string.Empty;

    public string RelayState { get; set; } = string.Empty;

    public Guid? TenantId { get; set; }

    public bool? RememberMe { get; set; }
}

/// <summary>
///     Response carrying the service provider's SAML 2.0 metadata document
/// </summary>
public sealed record SamlMetadataResponse
{
    /// <summary>
    ///     The SAML 2.0 metadata XML (<c>application/samlmetadata+xml</c>)
    /// </summary>
    public required string MetadataXml { get; init; }
}

/// <summary>
///     Response for SAML logout forwarding
/// </summary>
public sealed record SamlLogoutUrlResponse
{
    /// <summary>
    ///     The provider's single-logout redirect URL (an unsigned LogoutRequest via the
    ///     redirect binding); null when the provider has no configured single logout
    ///     service and logout stays local.
    /// </summary>
    public string? LogoutUrl { get; init; }
}
