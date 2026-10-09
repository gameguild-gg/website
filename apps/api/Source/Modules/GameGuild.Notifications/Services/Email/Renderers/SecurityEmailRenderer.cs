using System.Net;
using GameGuild.Email;

namespace GameGuild.Notifications.Services.Email.Renderers;

/// <summary>Renders credential-containment alerts without including persisted metadata.</summary>
public sealed class SecurityEmailRenderer : IEmailRenderer
{
    public NotificationType Type => NotificationType.Security;

    public Task<EmailMessage?> RenderAsync(Notification notification) => RenderAsync(notification, CancellationToken.None);

    public Task<EmailMessage?> RenderAsync(Notification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        if (notification.Type != NotificationType.Security)
        {
            throw new ArgumentException("A security renderer requires a security notification.", nameof(notification));
        }
        var title = string.IsNullOrWhiteSpace(notification.Title) ? "Account security alert" : notification.Title;
        var instructions = "Open the application using your usual trusted address, sign in again and review your account security. If you did not initiate this activity, reset your password and contact support.";
        var plain = notification.Message + "\n\n" + instructions;
        var html = "<p>" + WebUtility.HtmlEncode(notification.Message) + "</p><p>" + WebUtility.HtmlEncode(instructions) + "</p>";
        return Task.FromResult<EmailMessage?>(new EmailMessage(string.Empty, title, plain, html));
    }
}
