using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Notification raised when a one-time SMS MFA code should be delivered (issue #244).
/// </summary>
public sealed class SmsMfaCodeRequestedNotification : INotification
{
    public required Guid UserId { get; init; }

    /// <summary>Destination phone in E.164; carried in protected notification metadata.</summary>
    public required string PhoneNumber { get; init; }

    public required string Code { get; init; }

    /// <summary>True for enrollment verification, false for a sign-in challenge.</summary>
    public bool IsEnrollment { get; init; }
}
