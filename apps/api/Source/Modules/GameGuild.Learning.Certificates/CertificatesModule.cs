using GameGuild.Learning.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.Learning.Certificates;

/// <summary>
/// Module registration for certificates
/// </summary>
public static class CertificatesModule
{
    /// <summary>
    /// Registers all certificate module services
    /// </summary>
    public static IServiceCollection AddCertificatesModule(this IServiceCollection services)
    {
        // Register services
        services.AddScoped<CertificateService>();
        services.AddScoped<ICertificateService>(sp => sp.GetRequiredService<CertificateService>());
        services.AddScoped<ICertificateIssuanceService>(sp => sp.GetRequiredService<CertificateService>());
        services.AddScoped<ICertificateTemplateService, CertificateTemplateService>();

        // Default no-op anchoring; the host may register a blockchain-backed
        // ICertificateAnchoring adapter after the module (last registration wins).
        services.TryAddScoped<ICertificateAnchoring, NoOpCertificateAnchoring>();

        return services;
    }
}
