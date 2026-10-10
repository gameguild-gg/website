using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Linked external login as returned by the external-logins list endpoint.
/// </summary>
public sealed record ExternalLoginDto
{
    public required string Provider { get; init; }

    public required DateTime CreatedAt { get; init; }

    /// <summary>
    ///     OAuth scope tokens currently granted on this link. Empty for rows linked before
    ///     per-scope consent tracking existed (issue #250).
    /// </summary>
    public IReadOnlyList<string> GrantedScopes { get; init; } = [];

    /// <summary>UTC moment the scope consent was recorded, when one exists.</summary>
    public DateTime? ConsentedAt { get; init; }

    /// <summary>Version of the consent terms agreed to; 0 = legacy row without consent tracking.</summary>
    public int ConsentVersion { get; init; }
}

/// <summary>
///     Request to link the signed-in user's Google account via an ID token.
/// </summary>
public class LinkGoogleAccountRequest
{
    [Required]
    public string IdToken { get; set; } = string.Empty;
}

/// <summary>
///     Request to start the Discord link flow.
/// </summary>
public class DiscordLinkAuthorizeRequest
{
    [Required]
    public string RedirectUri { get; set; } = string.Empty;
}

/// <summary>
///     Request to complete the Discord link flow.
/// </summary>
public class DiscordLinkCallbackRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string State { get; set; } = string.Empty;

    [Required]
    public string RedirectUri { get; set; } = string.Empty;
}

/// <summary>
///     Request to revoke individual OAuth scope grants on a linked provider.
///     Revoking every remaining scope is allowed and leaves the link in place with an
///     empty grant list; removing the whole provider remains the unlink endpoint's job.
/// </summary>
public class RevokeExternalLoginScopesRequest
{
    /// <summary>Scope tokens to revoke. Each must be a valid scope token (non-empty, no whitespace).</summary>
    [Required]
    [MinLength(1)]
    public List<string> Scopes { get; set; } = [];
}

/// <summary>
///     Post-revocation snapshot of the link's grant state.
/// </summary>
public sealed record RevokeExternalLoginScopesResponse
{
    public required string Provider { get; init; }

    /// <summary>Scope grants remaining after the revocation.</summary>
    public required IReadOnlyList<string> GrantedScopes { get; init; }

    /// <summary>UTC moment the underlying consent was recorded (revocation does not re-stamp it), when one exists.</summary>
    public DateTime? ConsentedAt { get; init; }

    public int ConsentVersion { get; init; }
}

/// <summary>
///     Authorization-time consent preview: the exact scopes a link flow will request from
///     the provider, shown to the user before they continue (issue #250).
/// </summary>
public sealed record ExternalLoginLinkPreviewResponse
{
    public required string Provider { get; init; }

    /// <summary>Scope tokens that will appear on the authorization request.</summary>
    public required IReadOnlyList<string> RequestedScopes { get; init; }
}
