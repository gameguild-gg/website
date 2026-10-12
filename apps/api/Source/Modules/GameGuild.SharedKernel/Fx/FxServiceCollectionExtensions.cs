using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild;

/// <summary>
///     Registration extensions for the FX core (<see cref="IExchangeRateProvider"/> stack and
///     <see cref="CurrencyConversionService"/>). All FX state is immutable reference data, so
///     everything registers as a singleton.
/// </summary>
public static class FxServiceCollectionExtensions
{
    /// <summary>
    ///     Registers a <see cref="ManualExchangeRateProvider"/> as the base
    ///     <see cref="IExchangeRateProvider"/> using operator-entered quotations.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="rates">Manual quotations to serve; exact directions only, duplicates throw.</param>
    public static IServiceCollection AddManualExchangeRates(this IServiceCollection services, IEnumerable<ExchangeRate> rates)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(rates);

        services.Replace(ServiceDescriptor.Singleton<IExchangeRateProvider>(new ManualExchangeRateProvider(rates)));
        return services;
    }

    /// <summary>
    ///     Wraps the currently registered <see cref="IExchangeRateProvider"/> with a
    ///     <see cref="FixedOverrideExchangeRateProvider"/> so the given pairs are pinned.
    ///     Call after a base provider (e.g. <see cref="AddManualExchangeRates"/>) has been registered.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="overrides">Quotations that must win over the wrapped provider.</param>
    public static IServiceCollection AddFixedExchangeRateOverrides(this IServiceCollection services, IEnumerable<ExchangeRate> overrides)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(overrides);

        var previous = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(IExchangeRateProvider));
        if (previous?.ImplementationInstance is not IExchangeRateProvider inner)
        {
            throw new InvalidOperationException(
                "AddFixedExchangeRateOverrides requires an instance-registered base IExchangeRateProvider "
                + "(register one first, e.g. via AddManualExchangeRates).");
        }

        services.Replace(ServiceDescriptor.Singleton<IExchangeRateProvider>(
            new FixedOverrideExchangeRateProvider(overrides, inner)));
        return services;
    }

    /// <summary>
    ///     Registers the fail-closed <see cref="CurrencyConversionService"/> against whatever
    ///     <see cref="IExchangeRateProvider"/> is (or later becomes) registered.
    /// </summary>
    /// <param name="services">Service collection.</param>
    public static IServiceCollection AddCurrencyConversion(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<CurrencyConversionService>();
        return services;
    }
}
