using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.Identity.Authentication;

/// <summary>Starts publication only when a host explicitly supplies the complete worker composition.</summary>
public static class EvmCertificateWorkerServiceRegistration
{
    public static IServiceCollection AddEvmCertificatePublicationWorker(this IServiceCollection services, EvmCertificatePublicationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(policy);
        if (services.Any(s => s.ServiceType == typeof(EvmCertificatePublicationPolicy)))
        {
            throw new ArgumentException("Only one explicitly configured publication profile is supported per worker host.", nameof(services));
        }
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(policy);
        services.AddScoped<EvmCertificatePublicationWorker>();
        services.AddHostedService<EvmCertificatePublicationBackgroundService>();
        return services;
    }
}
