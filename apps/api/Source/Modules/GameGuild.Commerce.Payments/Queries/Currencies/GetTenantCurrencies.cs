using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>One currency as configured (or available) for the tenant, joined with catalog metadata.</summary>
/// <param name="CurrencyCode">ISO 4217 code (upper-case).</param>
/// <param name="IsEnabled">Whether the tenant enabled the currency.</param>
/// <param name="IsBaseCurrency">Whether it is the tenant's base currency.</param>
/// <param name="DisplaySymbol">Effective display symbol (preference override or catalog default).</param>
/// <param name="DisplayOrder">Selector ordering hint.</param>
/// <param name="CatalogSymbol">Catalog symbol (selector-UI data).</param>
/// <param name="EnglishName">Official English name (selector-UI data).</param>
/// <param name="MinorUnitExponent">Minor-unit digits (formatting helper data).</param>
/// <param name="NumericCode">ISO numeric code.</param>
public sealed record TenantCurrencyDto(
    string CurrencyCode,
    bool IsEnabled,
    bool IsBaseCurrency,
    string DisplaySymbol,
    int DisplayOrder,
    string CatalogSymbol,
    string EnglishName,
    int MinorUnitExponent,
    string NumericCode);

/// <summary>
///     Query (issue #412 spec A): the tenant's currency configuration surface. Returns every
///     catalog currency (150+) annotated with the tenant's enablement/base/display preferences —
///     including the catalog symbol/EnglishName data the selector UI needs — plus which code is
///     the current base (null when none is set yet: fail-closed default).
/// </summary>
public sealed record GetTenantCurrenciesQuery : IQuery<IReadOnlyList<TenantCurrencyDto>>;

/// <summary>Handler for <see cref="GetTenantCurrenciesQuery"/>.</summary>
public sealed class GetTenantCurrenciesQueryHandler(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetTenantCurrenciesQuery, IReadOnlyList<TenantCurrencyDto>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TenantCurrencyDto>> Handle(GetTenantCurrenciesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = actorContextAccessor.ActorContext.TenantId
            ?? throw new UnauthorizedAccessException("Currency configuration requires a tenant context.");

        var configurations = await context.Set<CurrencyConfiguration>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .ToDictionaryAsync(item => item.CurrencyCode, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        return Iso4217CurrencyCatalog.All
            .Select(catalog => configurations.TryGetValue(catalog.Code, out var configuration)
                ? new TenantCurrencyDto(
                    catalog.Code,
                    configuration.IsEnabled,
                    configuration.IsBaseCurrency,
                    configuration.DisplaySymbol ?? catalog.Symbol,
                    configuration.DisplayOrder,
                    catalog.Symbol,
                    catalog.EnglishName,
                    catalog.MinorUnitExponent,
                    catalog.NumericCode)
                : new TenantCurrencyDto(
                    catalog.Code,
                    IsEnabled: false,
                    IsBaseCurrency: false,
                    DisplaySymbol: catalog.Symbol,
                    DisplayOrder: 0,
                    CatalogSymbol: catalog.Symbol,
                    EnglishName: catalog.EnglishName,
                    MinorUnitExponent: catalog.MinorUnitExponent,
                    NumericCode: catalog.NumericCode))
            .OrderByDescending(dto => dto.IsEnabled)
            .ThenBy(dto => dto.IsBaseCurrency ? 0 : 1)
            .ThenBy(dto => dto.DisplayOrder)
            .ThenBy(dto => dto.CurrencyCode, StringComparer.Ordinal)
            .ToList();
    }
}
