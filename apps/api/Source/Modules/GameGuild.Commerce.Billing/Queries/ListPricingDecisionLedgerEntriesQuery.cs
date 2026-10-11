using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Commerce;

namespace GameGuild.Commerce.Billing;

/// <summary>
/// Ledger list item (issue #400): one pricing decision with its correlation ids and
/// headline amounts. The full breakdown document is returned on the detail read only.
/// </summary>
/// <param name="Id">Ledger row identifier.</param>
/// <param name="DecisionType">Decision type (CheckoutLineItem, RenewalQuote, OrderPromoApply).</param>
/// <param name="TenantId">Tenant scope.</param>
/// <param name="OrderId">Order correlation.</param>
/// <param name="OrderLineItemId">Order line item correlation.</param>
/// <param name="SubscriptionId">Subscription correlation.</param>
/// <param name="BillingCycleNumber">Billing cycle for renewal quotes.</param>
/// <param name="ProductId">Product correlation.</param>
/// <param name="Quantity">Quantity priced.</param>
/// <param name="Currency">Currency (ISO 4217).</param>
/// <param name="BaseAmount">Baseline amount.</param>
/// <param name="RuleAdjustmentTotal">Pricing-rule reduction total.</param>
/// <param name="PromoDiscountTotal">Promo discount total.</param>
/// <param name="FinalAmount">Final committed amount.</param>
/// <param name="AppliedPromoCodes">Normalized applied promo codes.</param>
/// <param name="AppliedPricingRuleId">Winning pricing rule, when one applied.</param>
/// <param name="AppliedStackingRuleName">Stacking rule name, when one governed resolution.</param>
/// <param name="PricingEngineVersion">Engine version stamp.</param>
/// <param name="ActorUserId">User the price was computed for.</param>
/// <param name="DecidedAtUtc">Decision timestamp (UTC).</param>
/// <param name="DriftStatus">Drift validation status.</param>
/// <param name="ValidatedAtUtc">When drift validation last stamped the entry.</param>
public sealed record PricingDecisionLedgerEntryDto(
    Guid Id,
    string DecisionType,
    Guid? TenantId,
    Guid? OrderId,
    Guid? OrderLineItemId,
    Guid? SubscriptionId,
    int? BillingCycleNumber,
    Guid? ProductId,
    int Quantity,
    string Currency,
    decimal BaseAmount,
    decimal RuleAdjustmentTotal,
    decimal PromoDiscountTotal,
    decimal FinalAmount,
    IReadOnlyList<string> AppliedPromoCodes,
    Guid? AppliedPricingRuleId,
    string? AppliedStackingRuleName,
    string PricingEngineVersion,
    Guid? ActorUserId,
    DateTime DecidedAtUtc,
    string DriftStatus,
    DateTime? ValidatedAtUtc)
{
    /// <summary>Maps a ledger entity to its list DTO.</summary>
    public static PricingDecisionLedgerEntryDto FromEntity(GameGuild.Commerce.PricingDecisionLedgerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var codes = string.IsNullOrWhiteSpace(entry.AppliedPromoCodes)
            ? []
            : entry.AppliedPromoCodes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

        return new PricingDecisionLedgerEntryDto(
            Id: entry.Id,
            DecisionType: entry.DecisionType,
            TenantId: entry.TenantId,
            OrderId: entry.OrderId,
            OrderLineItemId: entry.OrderLineItemId,
            SubscriptionId: entry.SubscriptionId,
            BillingCycleNumber: entry.BillingCycleNumber,
            ProductId: entry.ProductId,
            Quantity: entry.Quantity,
            Currency: entry.Currency,
            BaseAmount: entry.BaseAmount,
            RuleAdjustmentTotal: entry.RuleAdjustmentTotal,
            PromoDiscountTotal: entry.PromoDiscountTotal,
            FinalAmount: entry.FinalAmount,
            AppliedPromoCodes: codes,
            AppliedPricingRuleId: entry.AppliedPricingRuleId,
            AppliedStackingRuleName: entry.AppliedStackingRuleName,
            PricingEngineVersion: entry.PricingEngineVersion,
            ActorUserId: entry.ActorUserId,
            DecidedAtUtc: entry.DecidedAtUtc,
            DriftStatus: entry.DriftStatus,
            ValidatedAtUtc: entry.ValidatedAtUtc);
    }
}

/// <summary>Query (issue #400): page through the pricing-decision ledger.</summary>
/// <param name="TenantId">Optional tenant filter.</param>
/// <param name="OrderId">Optional order correlation filter.</param>
/// <param name="SubscriptionId">Optional subscription correlation filter.</param>
/// <param name="PromoCode">Optional exact applied-code filter (case-insensitive).</param>
/// <param name="DecisionType">Optional decision type filter.</param>
/// <param name="DriftStatus">Optional drift status filter.</param>
/// <param name="FromUtc">Inclusive lower bound on the decision timestamp.</param>
/// <param name="ToUtc">Inclusive upper bound on the decision timestamp.</param>
/// <param name="Skip">Items to skip.</param>
/// <param name="Take">Items to take (1-100).</param>
public sealed record ListPricingDecisionLedgerEntriesQuery(
    Guid? TenantId = null,
    Guid? OrderId = null,
    Guid? SubscriptionId = null,
    string? PromoCode = null,
    string? DecisionType = null,
    string? DriftStatus = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Skip = 0,
    int Take = 20) : IQuery<PagedResult<PricingDecisionLedgerEntryDto>>;

/// <summary>Validator for <see cref="ListPricingDecisionLedgerEntriesQuery"/>.</summary>
public sealed class ListPricingDecisionLedgerEntriesQueryValidator : AbstractValidator<ListPricingDecisionLedgerEntriesQuery>
{
    /// <summary>Initializes the validator.</summary>
    public ListPricingDecisionLedgerEntriesQueryValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Take).InclusiveBetween(1, 100);
        RuleFor(query => query.DecisionType)
            .Must(type => type is null || PricingDecisionType.All.Contains(type.Trim()))
            .WithMessage($"DecisionType must be one of: {string.Join(", ", PricingDecisionType.All)}.");
        RuleFor(query => query.DriftStatus)
            .Must(status => status is null || PricingDecisionDriftStatus.All.Contains(status.Trim()))
            .WithMessage($"DriftStatus must be one of: {string.Join(", ", PricingDecisionDriftStatus.All)}.");
        RuleFor(query => query.PromoCode)
            .MaximumLength(100)
            .When(query => !string.IsNullOrWhiteSpace(query.PromoCode));
        RuleFor(query => query.ToUtc)
            .GreaterThanOrEqualTo(query => query.FromUtc)
            .When(query => query.FromUtc.HasValue && query.ToUtc.HasValue);
    }
}

/// <summary>Handler for <see cref="ListPricingDecisionLedgerEntriesQuery"/>.</summary>
public sealed class ListPricingDecisionLedgerEntriesQueryHandler(
    IPricingDecisionLedgerRepository ledgerRepository)
    : IQueryHandler<ListPricingDecisionLedgerEntriesQuery, PagedResult<PricingDecisionLedgerEntryDto>>
{
    /// <inheritdoc />
    public async Task<PagedResult<PricingDecisionLedgerEntryDto>> Handle(
        ListPricingDecisionLedgerEntriesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var criteria = new PricingDecisionLedgerSearchCriteria(
            TenantId: request.TenantId,
            OrderId: request.OrderId,
            SubscriptionId: request.SubscriptionId,
            PromoCode: request.PromoCode,
            DecisionType: string.IsNullOrWhiteSpace(request.DecisionType) ? null : request.DecisionType.Trim(),
            DriftStatus: string.IsNullOrWhiteSpace(request.DriftStatus) ? null : request.DriftStatus.Trim(),
            FromUtc: request.FromUtc?.UtcDateTime,
            ToUtc: request.ToUtc?.UtcDateTime);

        var (entries, totalCount) = await ledgerRepository
            .SearchAsync(criteria, request.Skip, request.Take, cancellationToken)
            .ConfigureAwait(false);

        var items = entries
            .Select(PricingDecisionLedgerEntryDto.FromEntity)
            .ToList();

        return new PagedResult<PricingDecisionLedgerEntryDto>(items, totalCount, request.Skip, request.Take);
    }
}

/// <summary>Query (issue #400): fetch one ledger entry with its full breakdown document.</summary>
/// <param name="EntryId">Ledger row identifier.</param>
public sealed record GetPricingDecisionLedgerEntryQuery(Guid EntryId) : IQuery<PricingDecisionLedgerEntryDetailDto?>;

/// <summary>Validator for <see cref="GetPricingDecisionLedgerEntryQuery"/>.</summary>
public sealed class GetPricingDecisionLedgerEntryQueryValidator : AbstractValidator<GetPricingDecisionLedgerEntryQuery>
{
    /// <summary>Initializes the validator.</summary>
    public GetPricingDecisionLedgerEntryQueryValidator()
    {
        RuleFor(query => query.EntryId).NotEmpty();
    }
}

/// <summary>Handler for <see cref="GetPricingDecisionLedgerEntryQuery"/>.</summary>
public sealed class GetPricingDecisionLedgerEntryQueryHandler(
    IPricingDecisionLedgerRepository ledgerRepository)
    : IQueryHandler<GetPricingDecisionLedgerEntryQuery, PricingDecisionLedgerEntryDetailDto?>
{
    /// <inheritdoc />
    public async Task<PricingDecisionLedgerEntryDetailDto?> Handle(
        GetPricingDecisionLedgerEntryQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entry = await ledgerRepository.GetByIdAsync(request.EntryId, cancellationToken).ConfigureAwait(false);

        return entry is null ? null : PricingDecisionLedgerEntryDetailDto.FromEntity(entry);
    }
}

/// <summary>
/// Ledger detail (issue #400): the list item plus the raw breakdown JSON document and
/// the drift-validation outcome (recomputed amount, reason).
/// </summary>
/// <param name="Entry">List item fields.</param>
/// <param name="BreakdownJson">Full calculation breakdown (JSON).</param>
/// <param name="RecomputedFinalAmount">Final amount recomputed from the breakdown by the drift validator.</param>
/// <param name="DriftReason">Why the entry drifted, when it did.</param>
/// <param name="IdempotencyKey">Calculation idempotency key.</param>
/// <param name="ActorIpAddress">Client IP captured with the decision.</param>
public sealed record PricingDecisionLedgerEntryDetailDto(
    PricingDecisionLedgerEntryDto Entry,
    string BreakdownJson,
    decimal? RecomputedFinalAmount,
    string? DriftReason,
    string IdempotencyKey,
    string? ActorIpAddress)
{
    /// <summary>Maps a ledger entity to its detail DTO.</summary>
    public static PricingDecisionLedgerEntryDetailDto FromEntity(GameGuild.Commerce.PricingDecisionLedgerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new PricingDecisionLedgerEntryDetailDto(
            Entry: PricingDecisionLedgerEntryDto.FromEntity(entry),
            BreakdownJson: entry.BreakdownJson,
            RecomputedFinalAmount: entry.RecomputedFinalAmount,
            DriftReason: entry.DriftReason,
            IdempotencyKey: entry.IdempotencyKey,
            ActorIpAddress: entry.ActorIpAddress);
    }
}
