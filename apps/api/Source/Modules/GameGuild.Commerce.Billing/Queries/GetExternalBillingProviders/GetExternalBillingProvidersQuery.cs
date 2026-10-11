using GameGuild.CQRS;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Lists every supported external billing provider with configuration health and
///     runtime enabled state (issue #397 provider-management API).
/// </summary>
public sealed record GetExternalBillingProvidersQuery : IQuery<IReadOnlyList<ExternalBillingProviderStatusDto>>;

/// <summary>
///     Handler for <see cref="GetExternalBillingProvidersQuery"/>.
/// </summary>
public sealed class GetExternalBillingProvidersQueryHandler(IExternalBillingProviderRegistry registry)
    : IQueryHandler<GetExternalBillingProvidersQuery, IReadOnlyList<ExternalBillingProviderStatusDto>>
{
    public Task<IReadOnlyList<ExternalBillingProviderStatusDto>> Handle(GetExternalBillingProvidersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return registry.ListAsync(cancellationToken);
    }
}
