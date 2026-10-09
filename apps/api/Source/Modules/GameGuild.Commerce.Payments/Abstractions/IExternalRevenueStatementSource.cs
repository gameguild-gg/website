namespace GameGuild.Commerce.Payments;

/// <summary>
///     Integration seam for external accounting/ERP systems (issue #404 acceptance criterion
///     "Integration with external accounting and ERP systems"). Adapters expose settled
///     statement lines for a period; the default implementation
///     (<see cref="ConfigurationExternalRevenueStatementSource" />) serves lines supplied
///     through configuration and performs no outbound network calls.
/// </summary>
public interface IExternalRevenueStatementSource
{
    /// <summary>Provider name reported by the source (for example "stripe-payouts").</summary>
    string ProviderName { get; }

    /// <summary>Return the settled statement lines whose settlement moment falls in the inclusive period.</summary>
    /// <param name="periodStartUtc">Inclusive period start (UTC).</param>
    /// <param name="periodEndUtc">Inclusive period end (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Statement lines for the period, in provider order.</returns>
    Task<IReadOnlyList<ExternalRevenueStatementLine>> GetLinesAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default);
}
