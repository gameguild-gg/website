using GameGuild.Configuration;
using GameGuild.Configuration.ApplicationLayer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Config-gated registration for certificate blockchain anchoring.
/// </summary>
/// <remarks>
///     Safe default: with no configuration the provider is <c>"none"</c> and the disabled
///     no-op implementation is registered, so existing deployments are unaffected. Setting
///     <c>BlockchainCertificates:Provider</c> to <c>"local"</c> activates the deterministic
///     local/dev provider. Unknown provider values fail fast at startup (fail closed).
/// </remarks>
public static class BlockchainCertificateServiceRegistration
{
    /// <summary>
    ///     Binds and validates <see cref="BlockchainCertificateOptions" /> and registers the
    ///     selected <see cref="IBlockchainCertificateService" /> implementation.
    /// </summary>
    public static IServiceCollection AddBlockchainCertificateAnchoring(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = OptionBuilderUtilities.CreateAndBind(
            configuration,
            BlockchainCertificateOptions.SectionName,
            BlockchainCertificateOptions.CreateDefault);

        var validation = options.Validate();
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Invalid {BlockchainCertificateOptions.SectionName} configuration: {string.Join("; ", validation.Errors)}");
        }

        services.AddSingleton(options);

        switch (options.Provider)
        {
            case BlockchainCertificateOptions.ProviderNone:
                services.AddScoped<IBlockchainCertificateService>(_ => new DisabledBlockchainCertificateService());
                break;
            case BlockchainCertificateOptions.ProviderLocal:
                services.AddScoped<IBlockchainCertificateService>(provider => new LocalBlockchainCertificateService(
                    provider.GetRequiredService<IApplicationDbContext>(),
                    provider.GetRequiredService<BlockchainCertificateOptions>(),
                    provider.GetRequiredService<ILogger<LocalBlockchainCertificateService>>()));
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown {BlockchainCertificateOptions.SectionName}:Provider '{options.Provider}'. " +
                    $"Supported providers: '{BlockchainCertificateOptions.ProviderNone}', '{BlockchainCertificateOptions.ProviderLocal}'.");
        }

        return services;
    }
}
