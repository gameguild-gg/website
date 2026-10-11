using GameGuild.CQRS;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Gets the management and health status of a single external billing provider.
///     Fail-closed: unknown provider keys resolve to a null result (404 at the API).
/// </summary>
public sealed record GetExternalBillingProviderQuery(string ProviderKey) : IQuery<ExternalBillingProviderStatusDto?>;

/// <summary>
///     Handler for <see cref="GetExternalBillingProviderQuery"/>.
/// </summary>
public sealed class GetExternalBillingProviderQueryHandler(IExternalBillingProviderRegistry registry)
    : IQueryHandler<GetExternalBillingProviderQuery, ExternalBillingProviderStatusDto?>
{
    public Task<ExternalBillingProviderStatusDto?> Handle(GetExternalBillingProviderQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return registry.GetAsync(query.ProviderKey, cancellationToken);
    }
}
