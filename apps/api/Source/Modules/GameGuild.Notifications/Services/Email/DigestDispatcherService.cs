using GameGuild.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Notifications.Services.Email;

/// <summary>
/// Core digest engine, unit-testable without a BackgroundService host. Every <see cref="DigestDispatcherOptions.Interval"/>
/// it evaluates each user with an <see cref="NotificationPreference.EmailDigestFrequency"/> set:
/// Daily at the fire time user-local, Weekly on Mondays, BiWeekly on ISO-week-even Mondays. When the fire
/// time has passed and the user holds HeldForDigest rows created inside the elapsed window, the rows are
/// ATOMICALLY claimed (HeldForDigest → Sending via the Version concurrency token: the UPDATE only matches
/// for one racer), bundled into a single digest email, and marked Sent — or returned to HeldForDigest on send
/// failure so the next tick retries.
/// </summary>
/// <remarks>
/// Marker-less "since last digest": rows leave HeldForDigest only via this engine (Sent/DeadLettered) or
/// requeue, so every remaining HeldForDigest row is un-digested by construction and no persisted
/// last-digest marker is needed. The fire-window filter (CreatedAt &lt;= fire time) keeps rows that arrive
/// after a window's fire time out of that window — they wait for the next one.
/// Crash window: rows claimed (Sending) but never finalized are picked up by the email dispatcher's
/// existing Sending-reclaim arm after its 10-minute staleness window (they then deliver individually or
/// deadletter if no single-item renderer exists) — visible, never silently lost.
/// </remarks>
public sealed class DigestDispatcherService(
    IApplicationDbContext context,
    IRecipientEmailResolver recipientResolver,
    IEmailSender emailSender,
    DigestRenderer renderer,
    IOptions<DigestDispatcherOptions> options,
    ILogger<DigestDispatcherService> logger)
{
    /// <summary>
    /// Runs one evaluation pass and returns the number of digest emails sent. Per-user failures are
    /// logged and retried on the next tick; a single user never blocks the pass.
    /// </summary>
    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken = default)
    {
        var candidateUserIds = await context.Set<Notification>()
            .Where(n => n.RecipientId != null
                && n.Channel == NotificationChannel.Email
                && n.DeliveryStatus == NotificationDeliveryStatus.HeldForDigest)
            .Select(n => n.RecipientId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var digestsSent = 0;
        foreach (var userId in candidateUserIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (await DispatchDigestForUserAsync(userId, cancellationToken).ConfigureAwait(false))
                {
                    digestsSent++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Digest dispatch failed for user {UserId}; will retry on next tick", userId);
            }
        }

        return digestsSent;
    }

    private async Task<bool> DispatchDigestForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var preferences = await context.Set<NotificationPreference>()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken).ConfigureAwait(false);

        // Digest turned off (or prefs gone) while rows were held: requeue for individual delivery
        // instead of leaving the rows quarantined forever.
        if (preferences?.EmailDigestFrequency is not { } frequency)
        {
            return await RequeueOrphanedRowsAsync(userId, cancellationToken).ConfigureAwait(false);
        }

        var fireUtc = ComputeMostRecentFireUtc(
            frequency, SystemClock.UtcNow, ResolveTimeZone(preferences.Timezone), options.Value.FireTime);

        var claimed = await ClaimDueRowsAsync(userId, fireUtc, cancellationToken).ConfigureAwait(false);
        if (claimed.Count == 0)
        {
            return false;
        }

        var toEmail = await recipientResolver.ResolveAsync(claimed[0], cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            // Permanent condition (no address on any row) — same policy as the email dispatcher.
            foreach (var row in claimed)
            {
                row.MarkDeadLettered("digest recipient email could not be resolved");
            }
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogError("Deadlettered {Count} digest rows for user {UserId} without a resolvable email", claimed.Count, userId);
            return false;
        }

        // Suppression: actively suppressed address (hard bounce / complaint) — deadletter the whole
        // claimed bundle without sending (rows are claimed/Sending here; MarkDeadLettered is an
        // unguarded state write). Same policy as the email dispatcher's pre-send check.
        // ponytail: per-user indexed lookup; batch-prefetch if digest scale ever demands it.
        var activeSuppression = await EmailDispatcherService.FindActiveSuppressionAsync(context, toEmail, cancellationToken)
            .ConfigureAwait(false);
        if (activeSuppression is not null)
        {
            foreach (var row in claimed)
            {
                row.MarkDeadLettered("suppressed");
            }
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogWarning("Deadlettered {Count} digest row(s) to suppressed address. UserId: {UserId}, Recipient: {RecipientEmail}, Reason: {Reason}",
                claimed.Count, LogRedaction.Sanitize(userId.ToString()), LogRedaction.MaskEmail(toEmail), activeSuppression.Reason);
            return false;
        }

        try
        {
            await emailSender.SendAsync(renderer.Render(toEmail, claimed), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Roll the whole bundle back to HeldForDigest (NOT left in Sending — the email dispatcher's
            // reclaim arm would steal claimed rows and deliver them individually); retried next tick.
            // Digest failures are bundle-level, so retry attempts are not consumed.
            logger.LogWarning(ex, "Digest email send failed for user {UserId}; {Count} row(s) returned to HeldForDigest",
                userId, claimed.Count);
            foreach (var row in claimed)
            {
                SetDeliveryStatus(row, NotificationDeliveryStatus.HeldForDigest);
            }
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        foreach (var row in claimed)
        {
            row.MarkDeliverySent();
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Digest delivered. UserId: {UserId}, Rows: {RowCount}, Recipient: {RecipientEmail}",
            LogRedaction.Sanitize(userId.ToString()), claimed.Count, LogRedaction.MaskEmail(toEmail));
        return true;
    }

    /// <summary>
    /// ATOMIC CLAIM (conditional HeldForDigest → Sending): each row's Version concurrency token is bumped
    /// in the same UPDATE, so the save only matches for ONE racer on relational providers (the loser gets
    /// <see cref="DbUpdateConcurrencyException"/> and skips this tick). The Pending-only email dispatcher
    /// never touches HeldForDigest rows, so the two engines cannot double-claim.
    /// </summary>
    private async Task<List<Notification>> ClaimDueRowsAsync(Guid userId, DateTime fireUtc, CancellationToken cancellationToken)
    {
        var dueRows = await context.Set<Notification>()
            .Where(n => n.RecipientId == userId
                && n.Channel == NotificationChannel.Email
                && n.DeliveryStatus == NotificationDeliveryStatus.HeldForDigest
                && n.CreatedAt <= fireUtc)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (dueRows.Count == 0)
        {
            return dueRows;
        }

        foreach (var row in dueRows)
        {
            SetDeliveryStatus(row, NotificationDeliveryStatus.Sending);
            row.Version += 1; // concurrency-token bump turns the save into compare-and-swap
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return dueRows;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another digest instance claimed this user's rows — drop them (detach so the poisoned
            // entries never leak into later saves) and let the winner finish.
            logger.LogInformation("Digest claim lost the race for user {UserId}; skipping this tick", userId);
            if (context is DbContext dbContext)
            {
                foreach (var row in dueRows)
                {
                    dbContext.Entry(row).State = EntityState.Detached;
                }
            }
            return [];
        }
    }

    private async Task<bool> RequeueOrphanedRowsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var orphaned = await context.Set<Notification>()
            .Where(n => n.RecipientId == userId
                && n.Channel == NotificationChannel.Email
                && n.DeliveryStatus == NotificationDeliveryStatus.HeldForDigest)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (orphaned.Count == 0)
        {
            return false;
        }

        foreach (var row in orphaned)
        {
            SetDeliveryStatus(row, NotificationDeliveryStatus.Pending);
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Requeued {Count} digest row(s) for user {UserId}: digest no longer enabled",
            orphaned.Count, userId);
        return false;
    }

    /// <summary>
    /// Most recent scheduled fire time (in UTC) at or before <paramref name="nowUtc"/> for the given
    /// frequency: Daily every day, Weekly every Monday, BiWeekly every Monday whose ISO week number is
    /// even (parity anchor — arbitrary but stable across years).
    /// </summary>
    public static DateTime ComputeMostRecentFireUtc(
        DigestFrequency frequency, DateTime nowUtc, TimeZoneInfo zone, TimeOnly fireTime)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);
        var fireLocal = nowLocal.Date.Add(fireTime.ToTimeSpan());

        if (frequency != DigestFrequency.Daily)
        {
            var daysFromMonday = ((int)nowLocal.DayOfWeek + 6) % 7; // Monday = 0
            fireLocal = nowLocal.Date.AddDays(-daysFromMonday).Add(fireTime.ToTimeSpan());

            if (frequency == DigestFrequency.BiWeekly)
            {
                while (System.Globalization.ISOWeek.GetWeekOfYear(fireLocal) % 2 != 0)
                {
                    fireLocal = fireLocal.AddDays(-7);
                }
            }
        }

        if (fireLocal > nowLocal)
        {
            fireLocal = fireLocal.AddDays(frequency switch
            {
                DigestFrequency.Weekly => -7,
                DigestFrequency.BiWeekly => -14,
                _ => -1
            });
        }

        return TimeZoneInfo.ConvertTimeToUtc(fireLocal, zone);
    }

    /// <summary>Timezone resolution with UTC fallback, same pattern as NotificationPreferenceService.</summary>
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
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

    private void SetDeliveryStatus(Notification notification, NotificationDeliveryStatus status)
    {
        if (context is DbContext dbContext)
        {
            // DeliveryStatus has a private setter; written through the change API like
            // EmailDispatcherService.Hold. UpdatedAt is refreshed so the email dispatcher's
            // Sending-reclaim arm never sees a freshly claimed digest row as stale.
            dbContext.Entry(notification).Property(n => n.DeliveryStatus).CurrentValue = status;
            dbContext.Entry(notification).Property(n => n.UpdatedAt).CurrentValue = SystemClock.UtcNow;
        }
    }
}
