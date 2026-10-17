using System.Text.Json;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;

namespace GameGuild.API.Core.Security;

/// <summary>
/// Preference-gated owner notification for ordinary (non-suspicious) new sign-ins, mirroring
/// the <see cref="SuspiciousLoginAlertHandler"/> durable-consumer bridge. Unlike suspicious
/// alerts, this email is strictly opt-in: it is queued only when the user set the
/// <c>security.new_login_email</c> notification preference to <c>true</c> (default off).
/// Fail closed: when the preference cannot be read (repository/malformed payload errors), the
/// notification is NOT sent — an unreadable preference must never widen email volume.
/// </summary>
internal sealed class NewLoginAlertHandler(
    IUserRepository users,
    IUserPreferencesRepository preferences,
    INotificationService notifications,
    INotificationPreferenceService deliveryPreferences,
    IConfiguration configuration,
    ILogger<NewLoginAlertHandler> logger) : IIntegrationEventHandler<NewLoginDetectedV1>
{
    /// <summary>The opt-in user preference key (stored in the user's notification preferences JSON).</summary>
    public const string PreferenceKey = "security.new_login_email";

    private const string EnabledPath = "Authentication:NewLoginNotifications:Enabled";

    public async Task HandleAsync(NewLoginDetectedV1 @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (!configuration.GetValue(EnabledPath, true))
        {
            return;
        }

        var user = await users.GetByIdAsync(@event.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.IsDeleted || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        // Strict opt-in (default off). Any failure while reading the preference fails closed:
        // the email is skipped rather than guessed.
        bool optedIn;
        try
        {
            optedIn = await IsOptedInAsync(@event.UserId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "New-login preference could not be read for user {UserId}; failing closed (no email sent)",
                @event.UserId);
            return;
        }

        if (!optedIn)
        {
            return;
        }

        // Normal-priority routing: the user opted in, but channel toggles, mutes, and quiet
        // hours still apply through the standard delivery decision.
        var decision = await deliveryPreferences.DecideDeliveryAsync(
            user.Id, NotificationType.Security, NotificationChannel.Email, NotificationPriority.Normal, cancellationToken)
            .ConfigureAwait(false);
        if (decision.Action != NotificationDeliveryAction.Send)
        {
            return;
        }

        var tenantId = @event.TenantId == DurableIntegrationEventTenants.Platform ? (Guid?)null : @event.TenantId;
        var deviceSuffix = string.IsNullOrWhiteSpace(@event.DeviceSummary)
            ? string.Empty
            : $" from {@event.DeviceSummary}";
        var result = await notifications.SendAsync(user.Id, NotificationType.Security,
            "New sign-in to your account",
            $"A new sign-in to your account was recorded{deviceSuffix}. If this was not you, change your password and review your active sessions.",
            NotificationChannel.Email, tenantId, priority: NotificationPriority.Normal,
            referenceEntityId: @event.EventId, referenceEntityType: nameof(NewLoginDetectedV1),
            recipientEmail: user.Email, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result is null || result.IsFailure)
        {
            throw new InvalidOperationException("The new-login notification was not durably queued.");
        }
    }

    private async Task<bool> IsOptedInAsync(Guid userId, CancellationToken cancellationToken)
    {
        var userPreferences = await preferences.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (userPreferences is null)
        {
            return false;
        }

        // GetNotificationPreferences swallows malformed JSON into an empty dictionary; a
        // non-boolean value for the key is treated as not opted in.
        if (!userPreferences.GetNotificationPreferences().TryGetValue(PreferenceKey, out var raw) || raw is null)
        {
            return false;
        }

        return raw is JsonElement { ValueKind: JsonValueKind.True } || raw is bool and true;
    }
}
