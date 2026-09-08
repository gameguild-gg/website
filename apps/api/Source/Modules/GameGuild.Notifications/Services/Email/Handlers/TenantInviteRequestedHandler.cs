using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Tenants;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace GameGuild.Notifications.Services.Email.Handlers;

/// <summary>
/// Creates an email-channel Notification row for a tenant invite (new or resend). RecipientId is null
/// (the invitee may not have an account); the address lives on RecipientEmail. Row-creation failures are
/// retried by the durable outbox independently of the originating invite command.
/// </summary>
public sealed class TenantInviteRequestedHandler(
    INotificationService notificationService,
    ILogger<TenantInviteRequestedHandler> logger,
    ITenantMemberRepository memberRepository,
    ITenantRepository tenantRepository,
    IConfiguration configuration) : IDomainEventHandler<TenantInviteRequestedNotification>, IIntegrationEventHandler<TenantMemberInviteRequestedV1>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task HandleAsync(TenantMemberInviteRequestedV1 @event, CancellationToken cancellationToken = default)
    {
        var member = await memberRepository.GetByIdAsync(@event.MemberId, cancellationToken).ConfigureAwait(false);
        if (member is null || member.IsDeleted || member.TenantId != @event.TenantId)
            return;

        var metadata = TenantMemberInviteMetadata.FromJson(member.Metadata);
        if (metadata.InviteStatus == TenantMemberInviteStatuses.Cancelled || string.IsNullOrWhiteSpace(metadata.InviteeEmail))
            return;

        var tenant = await tenantRepository.GetByIdAsync(member.TenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null || tenant.IsDeleted)
            return;

        var appBaseUrl = (configuration["App:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var path = configuration["Identity:Invitations:ReviewPath"];
        var callback = string.IsNullOrWhiteSpace(path) ? "/invitations" : $"/{path.Trim().TrimStart('/')}";
        await Handle(new TenantInviteRequestedNotification(
            member.TenantId, metadata.InviteeEmail, metadata.InviteeName, metadata.InvitedByEmail,
            tenant.Name, member.Role,
            $"{appBaseUrl}/sign-in?callbackUrl={Uri.EscapeDataString(callback)}",
            $"{appBaseUrl}/forgot-password?email={Uri.EscapeDataString(metadata.InviteeEmail)}",
            @event.Resend), cancellationToken).ConfigureAwait(false);
    }

    public async Task Handle(TenantInviteRequestedNotification notification, CancellationToken cancellationToken)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            inviteeName = notification.InviteeName,
            invitedByEmail = notification.InvitedByEmail,
            tenantName = notification.TenantName,
            role = notification.Role,
            reviewUrl = notification.ReviewUrl,
            activationUrl = notification.ActivationUrl,
            resend = notification.Resend
        }, JsonOptions);

        try
        {
            var result = await notificationService.SendAsync(
                recipientId: null,
                type: NotificationType.TenantInvite,
                title: notification.Resend
                    ? $"Reminder: you were invited to {notification.TenantName} on GameGuild"
                    : $"You were invited to {notification.TenantName} on GameGuild",
                message: string.Empty,
                channel: NotificationChannel.Email,
                tenantId: notification.TenantId,
                metadata: metadata,
                recipientEmail: notification.InviteeEmail.Trim(),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result is null || !result.IsSuccess)
            {
                throw new InvalidOperationException("Tenant invite notification was not durably queued.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue tenant invite notification for {Email}", notification.InviteeEmail);
            throw;
        }
    }
}
