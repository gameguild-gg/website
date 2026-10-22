using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.API.Core.Integration;

public static class CertificateEvmEvidenceApiRegistration
{
    public static IServiceCollection AddCertificateEvmEvidenceApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ICertificateEvmEvidenceReader, DisabledCertificateEvmEvidenceReader>();
        return services;
    }
}
