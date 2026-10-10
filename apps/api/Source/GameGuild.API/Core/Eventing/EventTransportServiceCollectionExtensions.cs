using GameGuild.API.Core.Security;
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
        services.AddScoped<IInboxStore, InboxStore>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.AddScoped<IEventReplayService, EventReplayService>();
        services.AddHostedService<OutboxBackgroundService>();
        return services;
    }
}
