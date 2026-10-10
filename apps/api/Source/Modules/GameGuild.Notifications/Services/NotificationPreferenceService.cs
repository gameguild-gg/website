using Microsoft.EntityFrameworkCore;

namespace GameGuild.Notifications.Services;

/// <summary>
/// Manages user notification preferences and quiet hours
/// </summary>
public class NotificationPreferenceService(
    IApplicationDbContext context) : INotificationPreferenceService
{
    public async Task<Result<NotificationPreference>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var preferences = await context.Set<NotificationPreference>()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken).ConfigureAwait(false);

        if (preferences == null)
        {
            preferences = NotificationPreference.CreateDefault(userId);
            context.Set<NotificationPreference>().Add(preferences);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(preferences);
    }

    public async Task<Result<NotificationPreference>> UpdatePreferencesAsync(
        Guid userId,
        bool? emailEnabled = null,
        bool? pushEnabled = null,
        bool? inAppEnabled = null,
        bool? smsEnabled = null,
        bool? marketingEnabled = null,
        bool? socialEnabled = null,
        bool? learningEnabled = null,
        bool? achievementsEnabled = null,
        CancellationToken cancellationToken = default)
    {
        var preferencesResult = await GetPreferencesAsync(userId, cancellationToken).ConfigureAwait(false);
        if (!preferencesResult.IsSuccess)
        {
            return preferencesResult;
        }

        var preferences = preferencesResult.Value;

        preferences.UpdateChannelPreferences(
            emailEnabled ?? preferences.EmailEnabled,
            pushEnabled ?? preferences.PushEnabled,
            inAppEnabled ?? preferences.InAppEnabled,
            smsEnabled ?? preferences.SmsEnabled);

        preferences.UpdateCategoryPreferences(
            marketingEnabled ?? preferences.MarketingEnabled,
            socialEnabled ?? preferences.SocialEnabled,
            learningEnabled ?? preferences.LearningEnabled,
            achievementsEnabled ?? preferences.AchievementsEnabled);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(preferences);
    }

    public async Task<Result> SetQuietHoursAsync(
        Guid userId,
        TimeOnly? start,
        TimeOnly? end,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var preferencesResult = await GetPreferencesAsync(userId, cancellationToken).ConfigureAwait(false);
        if (!preferencesResult.IsSuccess)
        {
            return Result.Failure(preferencesResult.Error);
        }

        var preferences = preferencesResult.Value;

        if (start.HasValue && end.HasValue)
        {
            preferences.SetQuietHours(start, end, timezone);
        }
        else
        {
            preferences.ClearQuietHours();
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<NotificationPreference>> SetMutedTypesAsync(
        Guid userId,
        IEnumerable<string> typeNames,
        CancellationToken cancellationToken = default)
    {
        var preferencesResult = await GetPreferencesAsync(userId, cancellationToken).ConfigureAwait(false);
        if (!preferencesResult.IsSuccess)
        {
            return preferencesResult;
        }

        var distinctNames = new HashSet<string>(typeNames, StringComparer.OrdinalIgnoreCase);
        preferencesResult.Value.SetMutedTypes(
            distinctNames.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(distinctNames));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(preferencesResult.Value);
    }

    public async Task<Result<NotificationPreference>> SetEmailDigestFrequencyAsync(
        Guid userId,
        DigestFrequency? frequency,
        CancellationToken cancellationToken = default)
    {
        var preferencesResult = await GetPreferencesAsync(userId, cancellationToken).ConfigureAwait(false);
        if (!preferencesResult.IsSuccess)
        {
            return preferencesResult;
        }

        preferencesResult.Value.SetEmailDigestFrequency(frequency);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(preferencesResult.Value);
    }

    /// <summary>
    /// Notification types that are always delivered regardless of any preference (account-critical emails).
    /// </summary>
    private static readonly HashSet<NotificationType> TransactionalTypes =
    [
        NotificationType.EmailVerification,
        NotificationType.PasswordReset,
        NotificationType.MagicLink,
        NotificationType.EmailCode,
        NotificationType.TenantInvite
    ];

    public async Task<NotificationDeliveryDecision> DecideDeliveryAsync(
        Guid userId,
        NotificationType type,
        NotificationChannel channel,
        NotificationPriority priority,
        CancellationToken cancellationToken = default)
    {
        var preferences = await context.Set<NotificationPreference>()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken).ConfigureAwait(false);

        if (preferences == null)
        {
            return NotificationDeliveryDecision.Send();
        }

        // Evaluation order (fixed): gate-then-route.
        // 1. Transactional bypass: account-critical types and Urgent priority are never gated.
        if (TransactionalTypes.Contains(type) || priority == NotificationPriority.Urgent)
        {
            return NotificationDeliveryDecision.Send();
        }

        // 2. Per-type mute (JSON array of type names, case-insensitive, malformed JSON treated as empty).
        if (preferences.GetMutedTypeNames().Contains(type.ToString()))
        {
            return NotificationDeliveryDecision.Drop("muted");
        }

        // 3. Channel toggle.
        var channelEnabled = channel switch
        {
            NotificationChannel.Email => preferences.EmailEnabled,
            NotificationChannel.Push => preferences.PushEnabled,
            NotificationChannel.InApp => preferences.InAppEnabled,
            NotificationChannel.Sms => preferences.SmsEnabled,
            _ => true
        };

        if (!channelEnabled)
        {
            return NotificationDeliveryDecision.Drop("channel-disabled");
        }

        // 4. Category toggle.
        var categoryEnabled = type switch
        {
            NotificationType.Marketing => preferences.MarketingEnabled,
            NotificationType.SocialInteraction => preferences.SocialEnabled,
            NotificationType.CourseEnrollment or NotificationType.CourseCompletion or NotificationType.AssessmentReminder or NotificationType.AssessmentGraded => preferences.LearningEnabled,
            NotificationType.AchievementUnlocked or NotificationType.ProgressMilestone => preferences.AchievementsEnabled,
            _ => true
        };

        if (!categoryEnabled)
        {
            return NotificationDeliveryDecision.Drop("category-disabled");
        }

        // 5. Digest routing: every "do not send" gate has already run, so unsubscribed/muted/
        //    category-disabled types never enter the digest. Digestible email never lands in
        //    quiet hours, it lands in the digest.
        if (channel == NotificationChannel.Email
            && preferences.EmailDigestFrequency.HasValue
            && priority < preferences.QuietHoursBypassPriority)
        {
            return NotificationDeliveryDecision.Digest();
        }

        // 6. Quiet hours: InApp drops (existing behavior), Email holds until the quiet window ends, other channels send.
        if (priority < preferences.QuietHoursBypassPriority && IsInQuietHours(preferences, SystemClock.UtcNow))
        {
            if (channel == NotificationChannel.InApp)
            {
                return NotificationDeliveryDecision.Drop("quiet-hours");
            }

            if (channel == NotificationChannel.Email)
            {
                return NotificationDeliveryDecision.HoldUntil(ComputeQuietHoursEndUtc(preferences, SystemClock.UtcNow));
            }
        }

        return NotificationDeliveryDecision.Send();
    }

    [Obsolete("Use DecideDeliveryAsync instead. Returns true only for the Send decision.")]
    public async Task<bool> ShouldSendNotificationAsync(
        Guid userId,
        NotificationType type,
        NotificationChannel channel,
        NotificationPriority priority,
        CancellationToken cancellationToken = default)
    {
        var decision = await DecideDeliveryAsync(userId, type, channel, priority, cancellationToken).ConfigureAwait(false);
        return decision.Action == NotificationDeliveryAction.Send;
    }

    private static bool IsInQuietHours(NotificationPreference preferences, DateTime nowUtc)
    {
        if (!preferences.QuietHoursStart.HasValue || !preferences.QuietHoursEnd.HasValue)
        {
            return false;
        }

        var now = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, ResolveTimeZone(preferences.Timezone)));
        var start = preferences.QuietHoursStart.Value;
        var end = preferences.QuietHoursEnd.Value;

        if (start > end)
        {
            return now >= start || now <= end;
        }

        return now >= start && now <= end;
    }

    private static DateTime ComputeQuietHoursEndUtc(NotificationPreference preferences, DateTime nowUtc)
    {
        var zone = ResolveTimeZone(preferences.Timezone);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);
        var start = preferences.QuietHoursStart!.Value;
        var end = preferences.QuietHoursEnd!.Value;

        var endLocal = localNow.Date + end.ToTimeSpan();
        if (start > end && localNow.TimeOfDay >= start.ToTimeSpan())
        {
            // Overnight window entered after start: the end time falls on the next day.
            endLocal = endLocal.AddDays(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(endLocal, zone);
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
