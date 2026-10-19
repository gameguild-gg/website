using GameGuild.API.Database;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.API.Core.Integration;

/// <summary>Explicit domain-to-platform outbox composition. The host separately supplies journal persistence and worker/RPC/key configuration.</summary>
public static class CertificateEvmOutboxServiceRegistration
{
    public static IServiceCollection AddCertificateEvmDurableEvidence(this IServiceCollection services,
        EvmCertificatePublicationNetwork network, string canonicalIssuer)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(network);
        CertificateEvmEvidenceProducer.ValidateIssuer(canonicalIssuer);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICertificateDurableEvidenceProducer>(provider => new CertificateEvmEvidenceProducer(network, canonicalIssuer,
            provider.GetRequiredService<IActorContextAccessor>(), provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<CertificateEvmPublicationConsumer>(provider => new CertificateEvmPublicationConsumer(
            provider.GetRequiredService<ApplicationDbContext>(), provider.GetRequiredService<EvmCertificateJournal>(), network));
        services.AddScoped<IIntegrationEventHandler<CertificateEvmAnchorRequestedV1>>(provider => provider.GetRequiredService<CertificateEvmPublicationConsumer>());
        services.AddScoped<IIntegrationEventHandler<CertificateEvmRevocationRequestedV1>>(provider => provider.GetRequiredService<CertificateEvmPublicationConsumer>());
        return services;
    }
}
