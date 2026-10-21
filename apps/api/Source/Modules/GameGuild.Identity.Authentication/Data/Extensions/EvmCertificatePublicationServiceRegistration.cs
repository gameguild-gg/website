using GameGuild.Configuration.ApplicationLayer;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Identity.Authentication;

/// <summary>Explicit single-network transport composition. Does not select a provider, resolve a key or start a worker.</summary>
public static class EvmCertificatePublicationServiceRegistration
{
    public static IServiceCollection AddEvmCertificatePublicationTransport(this IServiceCollection services, EvmCertificateNetworkOptions options)
    {
        services.AddEvmCertificateVerification(options);
        services.AddScoped<IEvmCertificatePublicationRpc>(provider => new EvmCertificatePublicationRpcClient(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(EvmCertificateRpcClient.HttpClientName),
            provider.GetRequiredService<EvmCertificateNetworkOptions>(),
            provider.GetRequiredService<IEvmCertificateProofVerifier>()));
        return services;
    }
}
