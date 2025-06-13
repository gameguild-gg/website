using MediatR;

namespace GameGuild.Modules.UserProfile.Notifications;

/// <summary>
/// Notification sent when a user profile is updated
/// </summary>
public class UserProfileUpdatedNotification : INotification
{
    public Guid UserProfileId { get; set; }
    public Guid UserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Changes { get; set; } = new();
}
