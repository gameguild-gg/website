using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Dispute team notifications (issue #419): alerts the configured team addresses when a
///     dispute is created and when an open dispute's evidence deadline approaches. All sends are
///     best-effort — a delivery problem must never fail the dispute workflow that triggered it.
/// </summary>
public interface IDisputeNotificationService
{
    /// <summary>Alerts the configured team addresses that a new dispute was recorded.</summary>
    Task SendDisputeCreatedAlertAsync(PaymentDispute dispute, Payment? payment, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists open disputes whose evidence deadline (<see cref="PaymentDispute.EvidenceDueBy"/>
    ///     or <see cref="PaymentDispute.DueDate"/>) falls within the next
    ///     <paramref name="daysWithin"/> days, soonest first.
    /// </summary>
    Task<IReadOnlyList<PaymentDispute>> ListOpenDisputesApproachingDeadlineAsync(int daysWithin, CancellationToken cancellationToken = default);

    /// <summary>Sends one deadline-reminder email for an open dispute to the configured team addresses.</summary>
    Task SendDeadlineReminderAsync(PaymentDispute dispute, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class DisputeNotificationService(
    IApplicationDbContext context,
    INotificationService notificationService,
    IOptions<DisputeNotificationOptions> options,
    ILogger<DisputeNotificationService> logger) : IDisputeNotificationService
{
    private readonly DisputeNotificationOptions _options = options.Value;

    /// <inheritdoc />
    public async Task SendDisputeCreatedAlertAsync(PaymentDispute dispute, Payment? payment, CancellationToken cancellationToken = default)
    {
        if (_options.TeamAlertEmails.Count == 0)
        {
            return;
        }

        var amountSuffix = payment is null
            ? dispute.Amount.ToString("0.##")
            : $"{dispute.Amount:0.##} {payment.Currency}";
        var reference = dispute.GatewayDisputeId is null
            ? dispute.Id.ToString()
            : $"{dispute.Id} (gateway {dispute.GatewayName}:{dispute.GatewayDisputeId})";

        foreach (var email in _options.TeamAlertEmails)
        {
            try
            {
                await notificationService.SendAsync(
                    recipientId: null,
                    type: NotificationType.DisputeCreated,
                    title: "New payment dispute requires attention",
                    message:
                        $"Dispute {reference} for {amountSuffix} (reason: {dispute.Reason}) " +
                        $"was recorded for payment {dispute.PaymentId} and is now {dispute.Status}. " +
                        (dispute.EvidenceDueBy.HasValue
                            ? $"Evidence is due by {dispute.EvidenceDueBy:u}."
                            : "No gateway evidence deadline was provided."),
                    channel: NotificationChannel.Email,
                    tenantId: payment?.TenantId,
                    priority: NotificationPriority.High,
                    referenceEntityId: dispute.Id,
                    referenceEntityType: nameof(PaymentDispute),
                    recipientEmail: email,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    "Dispute-created alert for dispute {DisputeId} failed; the dispute itself stays recorded. Error type: {ErrorType}",
                    dispute.Id,
                    exception.GetType().FullName);
            }
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaymentDispute>> ListOpenDisputesApproachingDeadlineAsync(
        int daysWithin,
        CancellationToken cancellationToken = default)
    {
        var deadlineCutoff = SystemClock.UtcNow.AddDays(daysWithin);
        return await context.Set<PaymentDispute>()
            .AsNoTracking()
            .Where(dispute => dispute.Status != DisputeStatus.Resolved
                && dispute.Status != DisputeStatus.Won
                && dispute.Status != DisputeStatus.Lost
                && dispute.Status != DisputeStatus.Cancelled
                && dispute.EvidenceDueBy != null
                && dispute.EvidenceDueBy <= deadlineCutoff)
            .OrderBy(dispute => dispute.EvidenceDueBy)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SendDeadlineReminderAsync(PaymentDispute dispute, CancellationToken cancellationToken = default)
    {
        if (_options.TeamAlertEmails.Count == 0)
        {
            return;
        }

        var deadline = dispute.EvidenceDueBy ?? dispute.DueDate;
        foreach (var email in _options.TeamAlertEmails)
        {
            try
            {
                await notificationService.SendAsync(
                    recipientId: null,
                    type: NotificationType.DisputeDeadlineReminder,
                    title: "Dispute evidence deadline approaching",
                    message:
                        $"Dispute {dispute.Id} (payment {dispute.PaymentId}, status {dispute.Status}) " +
                        (deadline.HasValue
                            ? $"has its evidence deadline at {deadline:u}. Submit or accept the dispute before the funds are lost."
                            : "is still open. Submit or accept the dispute to close it out."),
                    channel: NotificationChannel.Email,
                    tenantId: null,
                    priority: NotificationPriority.High,
                    referenceEntityId: dispute.Id,
                    referenceEntityType: nameof(PaymentDispute),
                    recipientEmail: email,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    "Dispute deadline reminder for dispute {DisputeId} failed. Error type: {ErrorType}",
                    dispute.Id,
                    exception.GetType().FullName);
            }
        }
    }
}
