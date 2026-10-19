using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Notification raised when a magic sign-in link should be delivered.
/// </summary>
public sealed class MagicLinkRequestedNotification : INotification
{
    public required string Email { get; init; }

    public required string Token { get; init; }

    public string? UserName { get; init; }

    public string? RedirectTo { get; init; }

    public string? Locale { get; init; }

    public Guid? TenantId { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}
