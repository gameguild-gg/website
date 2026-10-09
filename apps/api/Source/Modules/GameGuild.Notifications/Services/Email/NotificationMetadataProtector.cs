using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace GameGuild.Notifications.Services.Email;

/// <summary>Protects recoverable identity-email metadata before durable storage.</summary>
public sealed class NotificationMetadataProtector(IDataProtectionProvider provider)
{
    public const string ProtectedPrefix = "gg-notification-metadata:v1:";
    private const string Purpose = "GameGuild.Notifications.IdentityEmailMetadata.v1";

    public static bool RequiresProtection(NotificationType type) =>
        type is NotificationType.EmailVerification or NotificationType.PasswordReset or NotificationType.MagicLink;

    public bool ProtectForStorage(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (!RequiresProtection(notification.Type) || string.IsNullOrEmpty(notification.Metadata))
        {
            return false;
        }

        if (notification.Metadata.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            _ = Unprotect(notification);
            return false;
        }

        var ciphertext = GetProtector(notification).Protect(notification.Metadata);
        notification.SetProtectedMetadata(ProtectedPrefix + ciphertext);
        return true;
    }

    public string? GetForRendering(Notification notification, NotificationType expectedType)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (notification.Type != expectedType || !RequiresProtection(expectedType))
        {
            throw InvalidPayload();
        }

        return string.IsNullOrWhiteSpace(notification.Metadata) ? notification.Metadata : Unprotect(notification);
    }

    private string Unprotect(Notification notification)
    {
        if (notification.Metadata is null || !notification.Metadata.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            throw InvalidPayload();
        }

        try
        {
            return GetProtector(notification).Unprotect(notification.Metadata[ProtectedPrefix.Length..]);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            throw InvalidPayload();
        }
    }

    private IDataProtector GetProtector(Notification notification)
    {
        if (notification.Id == Guid.Empty)
        {
            throw InvalidPayload();
        }

        return provider.CreateProtector(Purpose,
            notification.Id.ToString("N"),
            notification.Type.ToString(),
            notification.Channel.ToString(),
            notification.RecipientId?.ToString("N") ?? "none",
            notification.TenantId?.ToString("N") ?? "none",
            notification.RecipientEmail ?? string.Empty);
    }

    private static CryptographicException InvalidPayload() =>
        new("Notification credential metadata is invalid or unavailable.");
}
