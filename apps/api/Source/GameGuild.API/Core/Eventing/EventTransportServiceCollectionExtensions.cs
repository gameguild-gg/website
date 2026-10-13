using GameGuild.API.Core.Billing;
using GameGuild.API.Core.Security;
using GameGuild.Commerce.Billing;
using GameGuild.Identity.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.Eventing;

internal static class EventTransportServiceCollectionExtensions
{
    public static IServiceCollection AddDurableEventTransport(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IUseCaseOperationContextAccessor, UseCaseOperationContextAccessor>();
        services.AddSingleton<IUseCaseEventContractRegistry, UseCaseEventContractRegistry>();
        services.AddScoped<IUseCaseEventVerifier, UseCaseEventVerifier>();
        services.AddScoped<IDurableEventProducer, DurableEventProducer>();
        services.AddScoped<IIntegrationEventHandler<RefreshTokenReplayContainedV1>, RefreshTokenReplayAlertHandler>();
        services.AddScoped<IIntegrationEventHandler<SuspiciousLoginDetectedV1>, SuspiciousLoginAlertHandler>();
        services.AddScoped<IIntegrationEventHandler<SuspiciousLoginDetectedV1>, RiskEventSessionRevocationHandler>();
        // Issue #278: ordinary new sign-ins notify the owner only when they opted in
        // (default off; the per-user preference is evaluated host-side, fail-closed).
        services.AddScoped<IIntegrationEventHandler<NewLoginDetectedV1>, NewLoginAlertHandler>();
        // Issue #406: completed provider invoice payments credit the paying user's billing
        // wallet (idempotent per provider event identifier).
        services.AddScoped<IIntegrationEventHandler<BillingInvoicePaidV1>, BillingInvoicePaidWalletCreditHandler>();
        services.AddScoped<IInboxStore, InboxStore>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.AddScoped<IEventReplayService, EventReplayService>();
        services.AddHostedService<OutboxBackgroundService>();
        return services;
    }
}
