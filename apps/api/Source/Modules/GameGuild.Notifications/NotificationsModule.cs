using GameGuild.Notifications.Services;
using GameGuild.Notifications.Services.Email;
using GameGuild.Notifications.Services.Email.Renderers;
using GameGuild.Notifications.Services.Email.Handlers;
using GameGuild.Identity.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Notifications;

/// <summary>
/// Module registration for Notification services
/// </summary>
public static class NotificationsModule
{
    /// <summary>
    /// Adds Notifications module services to the DI container
    /// </summary>
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        // Sub-services (focused, single-responsibility)
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
        services.AddScoped<INotificationDeliveryService, NotificationDeliveryService>();

        // Email dispatch pipeline
        services.AddOptions<EmailDispatcherOptions>()
            .BindConfiguration("Notifications:EmailDispatcher");
        services.AddScoped<IEmailRendererRegistry, EmailRendererRegistry>();
        services.AddScoped<IRecipientEmailResolver, RecipientEmailResolver>();
        services.AddScoped<EmailDispatcherService>();
        services.AddHostedService<EmailDispatcherBackgroundService>();

        // Email digest engine (daily/weekly/biweekly bundled delivery)
        services.AddOptions<DigestDispatcherOptions>()
            .BindConfiguration("Notifications:DigestDispatcher");
        services.AddScoped<DigestRenderer>();
        services.AddScoped<DigestDispatcherService>();
        services.AddHostedService<DigestDispatcherBackgroundService>();

        // Tenant invite renderer. Registered here (not in the producing Identity.Tenants module) because
        // Identity.Tenants cannot reference GameGuild.Notifications (circular: Tenants -> Notifications -> Users -> Tenants).
        services.AddScoped<IEmailRenderer, TenantInviteRenderer>();
        services.AddScoped<IIntegrationEventHandler<TenantMemberInviteRequestedV1>, TenantInviteRequestedHandler>();

        // One-click unsubscribe tokens (IDataProtectionProvider is registered by the API host)
        services.AddScoped<IUnsubscribeTokenService, UnsubscribeTokenService>();

        // Footer injection for suppressible emails (consumed by renderers via constructor injection)
        services.AddScoped<IEmailFooterService, EmailFooterService>();

        // Platform-level deliverability administration (event feed, suppressions, requeue, timeline)
        services.AddScoped<IEmailDeliveryAdminService, EmailDeliveryAdminService>();

        // SES/SNS email-events webhook: signature verification + inline suppression processing
        // (the webhook command owns the single transactional SaveChanges for both)
        services.AddScoped<ISnsMessageVerifier, SnsMessageVerifier>();
        services.AddScoped<IEmailEventProcessor, EmailEventProcessor>();
        services.AddHttpClient(Controllers.EmailEventsController.SubscriptionConfirmationClientName, client =>
            client.Timeout = TimeSpan.FromSeconds(15));

        // Facade for backward compatibility
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
