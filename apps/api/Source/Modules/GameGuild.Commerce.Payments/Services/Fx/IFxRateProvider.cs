namespace GameGuild.Commerce.Payments;

/// <summary>
///     Sync-side FX rate source (issue #412 spec B). Distinct from the SharedKernel
/// <see cref="IExchangeRateProvider"/> conversion seam on purpose: this interface fetches
/// complete rate grids (with an effective-date parameter, which the SharedKernel interface
/// deliberately lacks) while the SharedKernel interface answers single-pair conversion
/// queries fail-closed. Adapters are config-gated: nothing dials out unless
/// <c>FxRates:Provider</c> selects it.
/// </summary>
public interface IFxRateProvider
{
    /// <summary>Provider identifier persisted with every snapshot (e.g. "openexchangerates").</summary>
    string Name { get; }

    /// <summary>
    ///     Fetches the provider's rate grid. When <paramref name="asOfUtc"/> is null the latest
    ///     grid is returned; otherwise providers that support historical data return the grid
    ///     for that effective date and providers that do not throw
    ///     <see cref="FxRateProviderUnavailableException"/> rather than substituting latest data.
    /// </summary>
    /// <param name="asOfUtc">Optional effective date for historical grids.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The fetched rate set including the raw provider response.</returns>
    /// <exception cref="FxRateProviderUnavailableException">
    ///     Thrown when the provider is disabled, unconfigured, or unreachable.
    /// </exception>
    Task<FxRateSet> GetRatesAsync(DateTimeOffset? asOfUtc = null, CancellationToken cancellationToken = default);
}

/// <summary>
///     Config-gated "no provider" adapter (the default). Produces no rates: the fail-closed
///     equivalent of the manual empty rate set previously wired in
///     <c>PaymentsModule</c>, now explicit configuration instead of a hard-coded empty list.
/// </summary>
public sealed class NoneFxRateProvider : IFxRateProvider
{
    /// <summary>Provider identifier.</summary>
    public string Name { get => "none"; }

    /// <inheritdoc />
    public Task<FxRateSet> GetRatesAsync(DateTimeOffset? asOfUtc = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new FxRateProviderUnavailableException(
            Name,
            "No FX rate provider is configured (FxRates:Provider='none'); cross-currency conversion stays fail-closed "
            + "with no quotations. Configure FxRates:Provider='openexchangerates' or 'ecb' to enable rate sync.");
    }
}
