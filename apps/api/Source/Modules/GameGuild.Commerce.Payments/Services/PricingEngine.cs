using System.Globalization;
using System.Text;
using GameGuild.Commerce;
using Microsoft.Extensions.Caching.Memory;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     The unified pricing engine (issue #417): ONE calculation path for base plan pricing,
///     independently priced add-ons, progressive usage tiers, promo discounts (Products promo
///     engine), mid-cycle proration (Subscriptions BillingCalculator), tax
///     (<see cref="TaxCalculationService"/>), wallet credits, and currency conversion
///     (SharedKernel FX core). Recorded calculations are appended to the pricing-decision
///     ledger (issue #400) so every price can be explained and replayed.
/// </summary>
/// <remarks>
///     Fail-closed posture: a plan that does not resolve, a cross-currency conversion without
///     a quotation, or tax inputs requested without <see cref="PricingTaxInfo"/> are errors —
///     never silently zeroed. Amounts are computed as raw decimals and clamped before Money
///     construction (Money rejects negatives by design); every money boundary rounds through
///     the ISO 4217 minor-unit rules of the calculation currency.
/// </remarks>
public sealed class PricingEngine(
    IPlanPricingResolver planPricingResolver,
    IPlanProrationCalculator prorationCalculator,
    IPromoDiscountCalculator promoDiscountCalculator,
    ITaxCalculationService taxCalculationService,
    IWalletService walletService,
    CurrencyConversionService currencyConversion,
    IPricingDecisionLedgerWriter ledgerWriter,
    IMemoryCache cache) : IPricingEngine
{
    private const string PlanPriceCacheKeyPrefix = "PricingEngine:PlanPrice:";

    private static readonly TimeSpan PlanPriceCacheDuration = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public async Task<PricingCalculationResult> CalculateAsync(
        PricingCalculationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currency = NormalizeCurrency(request.Currency);
        var explanation = new StringBuilder();
        var lineItems = new List<PricingLineItem>();
        var validationErrors = new List<string>();

        var effectiveDate = request.EffectiveDate ?? SystemClock.UtcNow;

        // ---- Step 1: base price (plan-resolved or explicit custom amount), FX-converted ----
        decimal baseAmount;
        if (request.PlanId.HasValue)
        {
            var planPrice = await GetPlanPriceAsync(request.PlanId.Value, request.BillingCycle, currency, cancellationToken)
                .ConfigureAwait(false);

            if (planPrice is null)
            {
                throw new InvalidOperationException($"Subscription plan {request.PlanId} not found or has no pricing.");
            }

            baseAmount = planPrice.Amount;
            explanation.Append(CultureInfo.InvariantCulture, $"Base plan {request.PlanId} ({request.BillingCycle}) = {Format(baseAmount, currency)}");
        }
        else if (request.BaseAmount.HasValue)
        {
            if (request.BaseAmount.Value < 0)
            {
                throw new InvalidOperationException("Custom base amount cannot be negative.");
            }

            baseAmount = request.BaseAmount.Value;
            explanation.Append(CultureInfo.InvariantCulture, $"Custom amount = {Format(baseAmount, currency)}");
        }
        else
        {
            // Fail-closed: a calculation without a plan or a custom amount is a caller error.
            throw new InvalidOperationException("A pricing calculation requires either a PlanId or a BaseAmount.");
        }

        lineItems.Add(new PricingLineItem("base_plan", request.PlanId?.ToString("N") ?? "custom", 1, baseAmount, MoneyOf(baseAmount, currency)));

        // ---- Step 2: add-ons with independent pricing (own unit price, own quantity, FX-converted) ----
        decimal addOnsTotal = 0;
        if (request.AddOns is { Count: > 0 })
        {
            foreach (var addOn in request.AddOns)
            {
                if (addOn.Quantity < 0)
                {
                    throw new InvalidOperationException($"Add-on {addOn.AddOnId} has a negative quantity.");
                }

                var unitPrice = await ConvertToCalculationCurrencyAsync(addOn.UnitPrice, currency, cancellationToken)
                    .ConfigureAwait(false);
                var addOnTotal = decimal.Round(unitPrice.Amount * addOn.Quantity, MinorUnits(currency), MidpointRounding.AwayFromZero);

                addOnsTotal += addOnTotal;
                lineItems.Add(new PricingLineItem("add_on", addOn.Name, addOn.Quantity, unitPrice.Amount, MoneyOf(addOnTotal, currency)));
                explanation.Append(CultureInfo.InvariantCulture, $"\n  + Add-on {addOn.Name} x{addOn.Quantity} = {Format(addOnTotal, currency)}");
            }
        }

        // ---- Step 3: usage-based charges with progressive tiers ----
        decimal usageTotal = 0;
        if (request.UsageCharges is { Count: > 0 })
        {
            foreach (var usage in request.UsageCharges)
            {
                var charge = UsageTierCalculator.Calculate(usage.Quantity, usage.PerUnitPrice, usage.Tiers);
                usageTotal += charge;
                lineItems.Add(new PricingLineItem("usage", usage.Metric, usage.Quantity, usage.PerUnitPrice, MoneyOf(charge, currency)));
                explanation.Append(CultureInfo.InvariantCulture, $"\n  + Usage {usage.Metric} x{usage.Quantity} = {Format(charge, currency)}");
            }
        }

        var subtotal = decimal.Round(baseAmount + addOnsTotal + usageTotal, MinorUnits(currency), MidpointRounding.AwayFromZero);
        explanation.Append(CultureInfo.InvariantCulture, $"\nSubtotal = {Format(subtotal, currency)}");

        // ---- Step 4: promo discounts through the Products promo engine (validation, stacking, caps) ----
        var appliedDiscounts = new List<AppliedDiscount>();
        var rejectedPromoCodes = new List<RejectedPromoDiscount>();
        decimal totalDiscounts = 0;
        if (request.PromoCodes is { Count: > 0 })
        {
            var discountableAmount = request.DiscountScope == PricingDiscountScope.PlanOnly
                ? baseAmount
                : subtotal;

            var promoResult = await promoDiscountCalculator
                .ApplyAsync(new PromoDiscountRequest(discountableAmount, request.PromoCodes, request.PlanId, request.UserId), cancellationToken)
                .ConfigureAwait(false);

            foreach (var applied in promoResult.Applied)
            {
                appliedDiscounts.Add(new AppliedDiscount
                {
                    Code = applied.Code,
                    Description = applied.Description,
                    Amount = MoneyOf(applied.Amount, currency),
                    Percentage = applied.Percentage ?? 0,
                    Type = applied.Type,
                    Priority = applied.Priority,
                    IsStackable = applied.IsStackable
                });
                explanation.Append(CultureInfo.InvariantCulture, $"\n  - Discount {applied.Code} ({applied.Type}) = -{Format(applied.Amount, currency)}");
            }

            // The promo engine already caps at the order amount; the clamp below is the
            // fail-closed invariant (discounts can never exceed what they were applied to).
            totalDiscounts = Math.Min(promoResult.TotalDiscount, discountableAmount);
            rejectedPromoCodes.AddRange(promoResult.Rejected);
        }

        var subtotalAfterDiscounts = Math.Max(0, subtotal - totalDiscounts);
        explanation.Append(CultureInfo.InvariantCulture, $"\nSubtotal after discounts = {Format(subtotalAfterDiscounts, currency)}");

        // ---- Step 5: mid-cycle proration via the existing BillingCalculator ----
        decimal prorationAdjustment = 0;
        PricingProrationResult? proration = null;
        if (request.Proration is not null)
        {
            var oldAmount = await GetPlanPriceAsync(request.Proration.CurrentPlanId, request.BillingCycle, currency, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Proration requested but plan {request.Proration.CurrentPlanId} has no pricing.");

            var newAmount = await GetPlanPriceAsync(request.Proration.NewPlanId, request.BillingCycle, currency, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Proration requested but plan {request.Proration.NewPlanId} has no pricing.");

            var prorationResult = await prorationCalculator.CalculateProrationAsync(
                new PlanProrationInput(oldAmount, newAmount, request.Proration.PeriodStart, request.Proration.PeriodEnd, request.Proration.EffectiveDate),
                cancellationToken).ConfigureAwait(false);

            proration = new PricingProrationResult(
                prorationResult.CreditForUnused,
                prorationResult.ChargeForNew,
                prorationResult.NetAdjustment,
                prorationResult.EffectiveDate,
                prorationResult.TotalDays,
                prorationResult.DaysRemaining);

            prorationAdjustment = decimal.Round(proration.NetAdjustment, MinorUnits(currency), MidpointRounding.AwayFromZero);
            explanation.Append(CultureInfo.InvariantCulture,
                $"\nProration ({proration.DaysRemaining}/{proration.TotalDays} days remaining) = {Format(prorationAdjustment, currency)} (credit {Format(proration.CreditForUnused, currency)}, charge {Format(proration.ChargeForNew, currency)})");

            lineItems.Add(new PricingLineItem("proration", "Plan change proration", 1, prorationAdjustment, MoneyOf(Math.Max(0, prorationAdjustment), currency)));
        }

        var taxableAmount = Math.Max(0, subtotalAfterDiscounts + prorationAdjustment);

        // ---- Step 6: tax through the existing TaxCalculationService, applied AFTER discounts ----
        decimal taxAmount = 0;
        TaxCalculationResult? taxResult = null;
        var taxInclusive = false;
        if (request.IncludeTax)
        {
            if (request.TaxInfo is null)
            {
                // Fail-closed: tax was requested without inputs; guessing a jurisdiction is worse than failing.
                throw new InvalidOperationException("IncludeTax is true but TaxInfo was not supplied.");
            }

            var exemptions = request.TaxInfo.ApplicableExemptions?.ToList();

            // Exemption check: an exempt customer produces a zero-tax, flagged result.
            var isExempt = false;
            if (request.UserId.HasValue)
            {
                isExempt = await taxCalculationService
                    .ValidateTaxExemptionAsync(request.UserId.Value, request.TaxInfo.JurisdictionCode, cancellationToken)
                    .ConfigureAwait(false);
            }

            taxResult = await taxCalculationService.CalculateTaxAsync(
                new TaxCalculationRequest
                {
                    JurisdictionCode = request.TaxInfo.JurisdictionCode,
                    Amount = taxableAmount,
                    Currency = currency,
                    CustomerType = request.TaxInfo.CustomerType,
                    ProductCategory = request.TaxInfo.ProductCategory,
                    CustomerVatNumber = request.TaxInfo.CustomerVatNumber,
                    IsTaxInclusive = request.TaxInfo.IsTaxInclusive,
                    TransactionDate = effectiveDate,
                    ApplicableExemptions = exemptions ?? []
                },
                cancellationToken).ConfigureAwait(false);

            taxInclusive = request.TaxInfo.IsTaxInclusive;
            taxAmount = taxResult.TaxAmount;
            explanation.Append(CultureInfo.InvariantCulture, $"\n  + Tax {request.TaxInfo.JurisdictionCode} ({taxResult.EffectiveTaxRate * 100:0.##}%) = {Format(taxAmount, currency)}");

            if (isExempt && !taxResult.IsTaxExempt)
            {
                // Surface the exemption finding; the tax service result stays authoritative.
                validationErrors.Add($"Customer exemption validated for {request.TaxInfo.JurisdictionCode} but the tax service did not flag the result exempt.");
            }
        }

        var total = taxInclusive
            ? Math.Max(0, taxableAmount) // tax-inclusive: the taxable amount already contains the tax
            : decimal.Round(taxableAmount + taxAmount, MinorUnits(currency), MidpointRounding.AwayFromZero);
        explanation.Append(CultureInfo.InvariantCulture, $"\nTotal = {Format(total, currency)}");

        // ---- Step 7: wallet credits, applied after tax, capped by availability and the caller's limit ----
        decimal creditsApplied = 0;
        CreditApplicationResult? creditApplication = null;
        if (request.ApplyCredits && request.UserId.HasValue)
        {
            var wallet = await walletService.GetWalletByUserIdAsync(request.UserId.Value, cancellationToken).ConfigureAwait(false);
            var available = wallet is not null && string.Equals(wallet.Currency, currency, StringComparison.OrdinalIgnoreCase)
                ? wallet.Balance
                : 0;

            if (wallet is not null && !string.Equals(wallet.Currency, currency, StringComparison.OrdinalIgnoreCase))
            {
                // Graceful, visible degradation: a currency-mismatched wallet is never silently spent.
                validationErrors.Add($"Wallet currency {wallet.Currency} does not match calculation currency {currency}; credits skipped.");
            }

            var cap = request.MaxCreditUsage.HasValue ? Math.Min(request.MaxCreditUsage.Value, available) : available;
            creditsApplied = Math.Max(0, Math.Min(Math.Min(cap, total), total));

            if (creditsApplied > 0)
            {
                creditApplication = new CreditApplicationResult(available, creditsApplied, available - creditsApplied);
                explanation.Append(CultureInfo.InvariantCulture, $"\n  - Credits applied = -{Format(creditsApplied, currency)} (available {Format(available, currency)})");

                if (request.ConsumeCredits)
                {
                    await walletService.DeductFundsAsync(
                        request.UserId.Value,
                        creditsApplied,
                        $"Pricing engine credit application",
                        referenceId: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }
        }

        var amountDue = Math.Max(0, total - creditsApplied);
        explanation.Append(CultureInfo.InvariantCulture, $"\nAmount due = {Format(amountDue, currency)}");

        // ---- Step 8: self-validation invariants (never silently pass a broken calculation) ----
        var lineItemSum = decimal.Round(lineItems.Where(li => li.Type != "proration").Sum(li => li.Amount.Amount), MinorUnits(currency), MidpointRounding.AwayFromZero);
        if (lineItemSum != subtotal)
        {
            validationErrors.Add($"Line item sum {lineItemSum} does not match subtotal {subtotal}.");
        }

        if (totalDiscounts > subtotal)
        {
            validationErrors.Add($"Total discounts {totalDiscounts} exceed subtotal {subtotal}.");
        }

        if (taxResult is not null && (taxResult.EffectiveTaxRate < 0 || taxResult.EffectiveTaxRate > 1))
        {
            validationErrors.Add($"Effective tax rate {taxResult.EffectiveTaxRate} is outside the plausible 0-100% band.");
        }

        if (proration is not null && Math.Abs(proration.NetAdjustment) > Math.Max(baseAmount, subtotal))
        {
            validationErrors.Add($"Proration adjustment {proration.NetAdjustment} exceeds the plan price bound.");
        }

        if (creditsApplied > total)
        {
            validationErrors.Add($"Credits applied {creditsApplied} exceed the total {total}.");
        }

        if (amountDue < 0)
        {
            validationErrors.Add($"Amount due {amountDue} is negative.");
        }

        var result = new PricingCalculationResult
        {
            CalculationId = Guid.NewGuid(),
            CalculatedAt = SystemClock.UtcNow,
            Currency = currency,
            BasePlanPrice = MoneyOf(baseAmount, currency),
            AddOnsTotal = MoneyOf(addOnsTotal, currency),
            UsageCharges = MoneyOf(usageTotal, currency),
            Subtotal = MoneyOf(subtotal, currency),
            AppliedDiscounts = appliedDiscounts,
            RejectedPromoCodes = rejectedPromoCodes,
            TotalDiscounts = MoneyOf(totalDiscounts, currency),
            SubtotalAfterDiscounts = MoneyOf(subtotalAfterDiscounts, currency),
            Proration = proration,
            ProrationAdjustment = prorationAdjustment,
            Tax = taxResult,
            TaxAmount = MoneyOf(taxAmount, currency),
            Credits = creditApplication,
            CreditsApplied = MoneyOf(creditsApplied, currency),
            Total = MoneyOf(total, currency),
            AmountDue = MoneyOf(amountDue, currency),
            LineItems = lineItems,
            CalculationExplanation = explanation.ToString(),
            ValidationErrors = validationErrors,
            BillingCycle = request.BillingCycle
        };

        // ---- Step 9: durable audit trail in the pricing-decision ledger ----
        if (request.RecordDecision)
        {
            var (status, entry) = await RecordDecisionAsync(request, result, cancellationToken).ConfigureAwait(false);
            if (entry is not null)
            {
                result = result with { LedgerEntryId = entry.Id };
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<PricingPreviewResult> PreviewAsync(
        PricingPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var effectiveDate = request.EffectiveDate ?? SystemClock.UtcNow;

        // Immediate charge: the new plan for the remaining period, prorated against the old plan.
        var immediate = await CalculateAsync(
            new PricingCalculationRequest(
                PlanId: request.NewPlanId,
                UserId: request.UserId,
                BillingCycle: request.BillingCycle,
                Currency: request.Currency,
                EffectiveDate: effectiveDate,
                PromoCodes: request.PromoCodes,
                TaxInfo: request.TaxInfo,
                IncludeTax: request.IncludeTax,
                ApplyCredits: false,
                RecordDecision: false,
                Proration: new PricingProrationInput(request.CurrentPlanId, request.NewPlanId, request.PeriodStart, request.PeriodEnd, effectiveDate)),
            cancellationToken).ConfigureAwait(false);

        // Next renewal: the new plan's full-cycle price with the same discounts.
        var nextRenewal = await CalculateAsync(
            new PricingCalculationRequest(
                PlanId: request.NewPlanId,
                UserId: request.UserId,
                BillingCycle: request.BillingCycle,
                Currency: request.Currency,
                EffectiveDate: request.PeriodEnd > effectiveDate ? request.PeriodEnd : effectiveDate,
                PromoCodes: request.PromoCodes,
                TaxInfo: request.TaxInfo,
                IncludeTax: request.IncludeTax,
                ApplyCredits: false,
                RecordDecision: false),
            cancellationToken).ConfigureAwait(false);

        return new PricingPreviewResult
        {
            EffectiveDate = effectiveDate,
            ImmediateCharge = immediate,
            NextRenewal = nextRenewal,
            SavingsFromDiscounts = nextRenewal.TotalDiscounts
        };
    }

    /// <inheritdoc />
    public async Task<Money?> GetPlanPriceAsync(
        Guid planId,
        BillingCycle billingCycle,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var normalizedCurrency = NormalizeCurrency(currency);
        var cacheKey = $"{PlanPriceCacheKeyPrefix}{planId:N}:{billingCycle}:{normalizedCurrency}";

        if (cache.TryGetValue(cacheKey, out Money? cached) && cached is not null)
        {
            return cached;
        }

        var price = await planPricingResolver.GetPlanPriceAsync(planId, billingCycle, cancellationToken).ConfigureAwait(false);
        if (price is null)
        {
            return null;
        }

        // FX through the SharedKernel core: same-currency is exact identity; cross-currency
        // needs a real quotation and fails closed when none is configured (#790 semantics).
        var converted = await currencyConversion.ConvertAsync(price, normalizedCurrency, cancellationToken).ConfigureAwait(false);

        cache.Set(cacheKey, converted, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = PlanPriceCacheDuration,
            Size = 1
        });

        return converted;
    }

    private async Task<(PricingDecisionRecordingStatus Status, PricingDecisionLedgerEntry? Entry)> RecordDecisionAsync(
        PricingCalculationRequest request,
        PricingCalculationResult result,
        CancellationToken cancellationToken)
    {
        var breakdown = PricingDecisionBreakdown.Empty(result.Currency)
            with
            {
                Version = 2,
                Quantity = 1,
                BaseUnitPrice = result.BasePlanPrice.Amount,
                BaseAmount = result.Subtotal.Amount,
                PromoApplications = result.AppliedDiscounts
                    .Select((discount, index) => new PromoApplicationBreakdown(
                        discount.Code,
                        PromoCodeId: null,
                        Type: discount.Type.ToString(),
                        Percentage: discount.Percentage == 0 ? null : discount.Percentage,
                        Amount: discount.Amount.Amount,
                        Sequence: index + 1))
                    .ToList(),
                PromoDiscountTotal = result.TotalDiscounts.Amount,
                // Version 2 extensions (plan-level calculation parts):
                AddOnsAmount = result.AddOnsTotal.Amount,
                UsageAmount = result.UsageCharges.Amount,
                ProrationAdjustment = result.ProrationAdjustment,
                TaxAmount = result.TaxAmount.Amount,
                CreditsApplied = result.CreditsApplied.Amount,
                FinalAmount = result.AmountDue.Amount
            };

        return await ledgerWriter
            .RecordAsync(
                new PricingDecisionRecord(
                    DecisionType: PricingDecisionType.CalculationQuote,
                    TenantId: request.TenantId,
                    ActorUserId: request.UserId,
                    ActorIpAddress: null,
                    Order: request.OrderId,
                    OrderLineItem: null,
                    PaymentId: request.PaymentId,
                    Subscription: request.SubscriptionId,
                    BillingCycleNumber: null,
                    Product: request.PlanId,
                    Breakdown: breakdown,
                    IdempotencyKey: $"pricing-quote:{result.CalculationId:N}"),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Money> ConvertToCalculationCurrencyAsync(Money amount, string currency, CancellationToken cancellationToken)
    {
        return await currencyConversion.ConvertAsync(amount, currency, cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new InvalidOperationException("Calculation currency must be a three-letter ISO 4217 code.");
        }

        return currency.Trim().ToUpperInvariant();
    }

    private static int MinorUnits(string currency) => Iso4217CurrencyCatalog.GetMinorUnitExponent(currency);

    private static Money MoneyOf(decimal amount, string currency) => new(Math.Max(0, amount), currency);

    private static string Format(decimal amount, string currency) => $"{decimal.Round(amount, MinorUnits(currency), MidpointRounding.AwayFromZero):0.##} {currency}";
}
