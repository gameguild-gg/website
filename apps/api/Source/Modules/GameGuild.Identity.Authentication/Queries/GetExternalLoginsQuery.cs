using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Query to list the external logins linked to a user, newest first.
/// </summary>
public sealed record GetExternalLoginsQuery : IQuery<List<ExternalLoginDto>>
{
    public required Guid UserId { get; init; }
}

/// <summary>
///     Query for the authorization-time consent preview of a provider link flow: the
///     exact scopes the authorization request will ask for (issue #250).
/// </summary>
public sealed record GetExternalLoginLinkPreviewQuery : IQuery<ExternalLoginLinkPreviewResponse>
{
    public required string Provider { get; init; }
}
