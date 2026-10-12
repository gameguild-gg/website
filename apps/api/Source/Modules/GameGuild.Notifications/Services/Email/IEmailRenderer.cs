using GameGuild.Email;

namespace GameGuild.Notifications.Services.Email;

/// <summary>
/// Renders an email-channel <see cref="Notification"/> into an <see cref="EmailMessage"/> at send time.
/// Rendering context comes from <see cref="Notification.Metadata"/>; identity credentials require
/// <see cref="NotificationMetadataProtector"/> before JSON parsing. Renderers must not
/// reach out to services other than their constructor-injected dependencies.
/// </summary>
/// <remarks>
/// Footers (unsubscribe links) are deliberately NOT part of this contract; they are injected later by the
/// footer-injection layer for suppressible types only. The dispatcher overwrites <see cref="EmailMessage.ToEmail"/>
/// with the independently resolved recipient address.
/// </remarks>
public interface IEmailRenderer
{
    /// <summary>
    /// The notification type this renderer handles.
    /// </summary>
    NotificationType Type { get; }

    /// <summary>
    /// Renders the email for the given notification. Returning null means "nothing to send";
    /// ordinary notifications are marked as Sent for that skip. Security notifications instead fail
    /// and retry because their delivery requires both a rendered message and provider acceptance.
    /// </summary>
    Task<EmailMessage?> RenderAsync(Notification notification, CancellationToken cancellationToken = default);
}
