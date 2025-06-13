using MediatR;

namespace GameGuild.Modules.Auth.Notifications;

/// <summary>
/// Notification sent when a user successfully signs up
/// </summary>
public class UserSignedUpNotification : INotification
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime SignUpTime { get; set; } = DateTime.UtcNow;
}
