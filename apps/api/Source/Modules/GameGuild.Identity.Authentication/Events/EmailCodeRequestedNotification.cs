using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Notification raised when a one-time email sign-in code should be delivered.
/// </summary>
public sealed class EmailCodeRequestedNotification : INotification
{
    public required string Email { get; init; }

    public required string Code { get; init; }

    public string? UserName { get; init; }

    public Guid? TenantId { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}
