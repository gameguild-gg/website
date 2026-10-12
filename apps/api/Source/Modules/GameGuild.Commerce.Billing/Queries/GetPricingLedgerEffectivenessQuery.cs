using FluentValidation;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Billing;

/// <summary>
/// Query (issue #400): discount effectiveness and pricing analytics over the
/// pricing-decision ledger — redemption counts, discount given versus baseline revenue,
/// top promo codes and pricing rules by impact, stacking outcomes and drift health
/// for a window. Feeds the monetization reporting dashboard.
/// </summary>
/// <param name="FromUtc">Inclusive window start (null = unbounded).</param>
/// <param name="ToUtc">Inclusive window end (null = unbounded).</param>
/// <param name="TenantId">Optional tenant scope.</param>
public sealed record GetPricingLedgerEffectivenessQuery(
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    Guid? TenantId = null) : IQuery<PricingLedgerEffectivenessAggregate>;

/// <summary>Validator for <see cref="GetPricingLedgerEffectivenessQuery"/>.</summary>
public sealed class GetPricingLedgerEffectivenessQueryValidator : AbstractValidator<GetPricingLedgerEffectivenessQuery>
{
    /// <summary>Initializes the validator.</summary>
    public GetPricingLedgerEffectivenessQueryValidator()
    {
        RuleFor(query => query.ToUtc)
            .GreaterThanOrEqualTo(query => query.FromUtc)
            .When(query => query.FromUtc.HasValue && query.ToUtc.HasValue);
    }
}

/// <summary>Handler for <see cref="GetPricingLedgerEffectivenessQuery"/>.</summary>
public sealed class GetPricingLedgerEffectivenessQueryHandler(
    IPricingDecisionLedgerRepository ledgerRepository)
    : IQueryHandler<GetPricingLedgerEffectivenessQuery, PricingLedgerEffectivenessAggregate>
{
    /// <inheritdoc />
    public async Task<PricingLedgerEffectivenessAggregate> Handle(
        GetPricingLedgerEffectivenessQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await ledgerRepository.GetEffectivenessAsync(
            request.FromUtc?.UtcDateTime,
            request.ToUtc?.UtcDateTime,
            request.TenantId,
            cancellationToken).ConfigureAwait(false);
    }
}
