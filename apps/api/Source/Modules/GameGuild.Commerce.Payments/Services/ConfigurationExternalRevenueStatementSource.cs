using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Default <see cref="IExternalRevenueStatementSource" /> that serves statement lines
///     supplied through <c>RevenueAuditing:StatementSource</c> configuration. This keeps the
///     ERP integration contract testable and network-free; production adapters for specific
///     accounting systems replace this registration.
/// </summary>
public sealed class ConfigurationExternalRevenueStatementSource(
    IOptions<RevenueAuditingOptions> options) : IExternalRevenueStatementSource
{
    /// <inheritdoc />
    public string ProviderName =>
        options.Value.StatementSource.ProviderName is { Length: > 0 } providerName
            ? providerName
            : "manual-export";

    /// <inheritdoc />
    public Task<IReadOnlyList<ExternalRevenueStatementLine>> GetLinesAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lines = options.Value.StatementSource.Lines
            .Where(line => line.OccurredAtUtc >= periodStartUtc && line.OccurredAtUtc <= periodEndUtc)
            .OrderBy(line => line.OccurredAtUtc)
            .ThenBy(line => line.ReferenceId, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult<IReadOnlyList<ExternalRevenueStatementLine>>(lines);
    }
}
