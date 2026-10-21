using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Registration extensions for the FX rate sync foundation (issue #412 specs A/B). Called
///     from <c>PaymentsModule</c> in place of the previous empty
///     <see cref="FxServiceCollectionExtensions.AddManualExchangeRates"/> wiring: the conversion
///     seam now resolves through persisted snapshots and operator overrides, still fail-closed
///     by default (<c>FxRates:Provider='none'</c> registers no HTTP adapter and no quotations).
/// </summary>
public static class FxRateSyncServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the FX provider stack, cache, sync service, scheduled worker and the
    ///     snapshot-backed <see cref="IExchangeRateProvider"/>. HTTP adapters are only created
    ///     for providers actually selected by configuration (primary or fallback).
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration (FxRates section).</param>
    public static IServiceCollection AddFxRateSync(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<FxSyncOptions>, FxSyncOptionsValidator>();
        services.AddOptions<FxSyncOptions>()
            .Bind(configuration.GetSection(FxSyncOptions.SectionName))
            .ValidateOnStart();

        // Config-gated adapter wiring: read the provider selection once for registration-time
        // decisions (validated again at startup by the options validator).
        var section = configuration.GetSection(FxSyncOptions.SectionName);
        var configuredProvider = section["Provider"];
        var configuredFallback = section["FallbackProvider"];

        services.AddSingleton<NoneFxRateProvider>();
        services.AddScoped<IFxRateProvider>(provider => provider.GetRequiredService<NoneFxRateProvider>());

        if (configuredProvider == FxSyncOptions.ProviderOpenExchangeRates || configuredFallback == FxSyncOptions.ProviderOpenExchangeRates)
        {
            services.AddHttpClient<OpenExchangeRatesFxRateProvider>((provider, client) =>
            {
                var settings = provider.GetRequiredService<IOptions<FxSyncOptions>>().Value.OpenExchangeRates;
                client.Timeout = TimeSpan.FromSeconds(Math.Max(5, settings.TimeoutSeconds));
            });
            services.AddScoped<OpenExchangeRatesFxRateProvider>();
            services.AddScoped<IFxRateProvider>(provider => provider.GetRequiredService<OpenExchangeRatesFxRateProvider>());
        }

        if (configuredProvider == FxSyncOptions.ProviderEcb || configuredFallback == FxSyncOptions.ProviderEcb)
        {
            services.AddHttpClient<EcbFxRateProvider>((provider, client) =>
            {
                var settings = provider.GetRequiredService<IOptions<FxSyncOptions>>().Value.Ecb;
                client.Timeout = TimeSpan.FromSeconds(Math.Max(5, settings.TimeoutSeconds));
            });
            services.AddScoped<EcbFxRateProvider>();
            services.AddScoped<IFxRateProvider>(provider => provider.GetRequiredService<EcbFxRateProvider>());
        }

        // Conversion-path stack: runtime source (singleton, hot path) + TTL cache (distributed
        // Redis/memory when registered, in-memory otherwise) + the IExchangeRateProvider bridge.
        services.TryAddSingleton<FxRuntimeRateSource>();
        services.TryAddSingleton<IFxRateCache>(provider => new FxRateCache(
            provider.GetService<IDistributedCache>(), // null in hosts without a distributed cache
            provider.GetRequiredService<IOptions<FxSyncOptions>>(),
            provider.GetRequiredService<ILogger<FxRateCache>>()));
        services.Replace(ServiceDescriptor.Singleton<IExchangeRateProvider, PersistedFxRateProvider>());

        // Sync orchestration + alerting + the opt-in scheduled worker.
        services.AddScoped<IFxRateSyncService, FxRateSyncService>();
        services.TryAddSingleton<IFxRateSyncAlerter, NotificationFxRateSyncAlerter>();
        services.AddHostedService<FxRateSyncBackgroundService>();

        return services;
    }
}
