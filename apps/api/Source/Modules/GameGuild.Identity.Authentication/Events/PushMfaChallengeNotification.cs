using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Notification raised when a push MFA approval challenge should be delivered (issue #259).
/// </summary>
public sealed class PushMfaChallengeNotification : INotification
{
    public required Guid UserId { get; init; }

    /// <summary>Enrolled device registration token; carried in protected notification metadata.</summary>
    public required string DeviceToken { get; init; }

    /// <summary>Opaque challenge identifier the device answers with.</summary>
    public required Guid ChallengeId { get; init; }
}
