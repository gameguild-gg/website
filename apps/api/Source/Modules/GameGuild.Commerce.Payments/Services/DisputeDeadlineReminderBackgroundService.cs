using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Sends deadline reminders for open disputes whose evidence deadline is approaching
///     (issue #419: "reminder emails before deadline"). Runs daily, opt-in via
///     <c>Payments:Disputes:DeadlineReminderEnabled</c> (safe default: disabled); reminders go
///     to the configured <c>Payments:Disputes:TeamAlertEmails</c> for disputes whose
///     <see cref="PaymentDispute.EvidenceDueBy"/> falls within
///     <c>Payments:Disputes:ReminderDaysBeforeDeadline</c> days (default 7).
/// </summary>
public sealed class DisputeDeadlineReminderBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<DisputeNotificationOptions> options,
    ILogger<DisputeDeadlineReminderBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.DeadlineReminderEnabled)
        {
            logger.LogInformation(
                "DisputeDeadlineReminderBackgroundService is disabled (Payments:Disputes:DeadlineReminderEnabled=false); dispute deadline reminders stay off.");
            return;
        }

        if (settings.TeamAlertEmails.Count == 0)
        {
            logger.LogInformation(
                "DisputeDeadlineReminderBackgroundService has no TeamAlertEmails configured; reminders would have no recipient and stay off.");
            return;
        }

        logger.LogInformation(
            "DisputeDeadlineReminderBackgroundService started with a daily cycle reminding {Days} days before evidence deadlines.",
            settings.ReminderDaysBeforeDeadline);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var notifications = scope.ServiceProvider.GetRequiredService<IDisputeNotificationService>();
                var approaching = await notifications
                    .ListOpenDisputesApproachingDeadlineAsync(settings.ReminderDaysBeforeDeadline, stoppingToken)
                    .ConfigureAwait(false);

                foreach (var dispute in approaching)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    await notifications.SendDeadlineReminderAsync(dispute, stoppingToken).ConfigureAwait(false);
                }

                if (approaching.Count > 0)
                {
                    logger.LogInformation(
                        "Dispute deadline reminder cycle sent {Count} reminder(s).",
                        approaching.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Dispute deadline reminder cycle failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
