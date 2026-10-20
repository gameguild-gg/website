using GameGuild.API.Database;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Certificates;
using GameGuild.Learning;
using GameGuild.Learning.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
        // Explicit construction must not fall back to the legacy constructor when an optional adapter is absent.
        services.AddScoped<CertificateService>(provider => new CertificateService(provider.GetRequiredService<IApplicationDbContext>(),
            provider.GetRequiredService<ILogger<CertificateService>>(), provider.GetRequiredService<ILearningAuditEventSink>(),
            provider.GetService<ICertificateAnchoring>(), provider.GetService<IRequestContextAccessor>(),
            provider.GetRequiredService<ICertificateDurableEvidenceProducer>()));
        services.AddScoped<ICertificateService>(provider => provider.GetRequiredService<CertificateService>());
        services.AddScoped<ICertificateIssuanceService>(provider => provider.GetRequiredService<CertificateService>());
        services.AddScoped<CertificateEvmPublicationConsumer>(provider => new CertificateEvmPublicationConsumer(
            provider.GetRequiredService<ApplicationDbContext>(), provider.GetRequiredService<EvmCertificateJournal>(), network));
        services.AddScoped<IIntegrationEventHandler<CertificateEvmAnchorRequestedV1>>(provider => provider.GetRequiredService<CertificateEvmPublicationConsumer>());
        services.AddScoped<IIntegrationEventHandler<CertificateEvmRevocationRequestedV1>>(provider => provider.GetRequiredService<CertificateEvmPublicationConsumer>());
        return services;
    }
}
